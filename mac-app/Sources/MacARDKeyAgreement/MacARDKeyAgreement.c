#include "MacARDKeyAgreement.h"
#include <Security/Security.h>
#include <pthread.h>
#include <stdlib.h>
#include <string.h>

/* ARD type 30 uses finite-field DH, absent from modern SecKey/CryptoKit APIs.
 * These documented Security CSP APIs remain public in the macOS SDK. Keep
 * their deprecated C interface isolated; do not use private crypto symbols. */
#pragma clang diagnostic push
#pragma clang diagnostic ignored "-Wdeprecated-declarations"
static pthread_once_t once = PTHREAD_ONCE_INIT;
static CSSM_RETURN provider_status;
static void *allocate(CSSM_SIZE n, void *context) { return malloc(n); }
static void release(void *p, void *context) { free(p); }
static void *resize(void *p, CSSM_SIZE n, void *context) { return realloc(p,n); }
static void *zero_allocate(uint32 n, CSSM_SIZE size, void *context) { return calloc(n,size); }
static void initialize_provider(void) {
    CSSM_VERSION version = {2,0};
    CSSM_GUID caller = {0x7a5a0db9,0x5327,0x4af6,{0xa0,0x1c,0xd3,0x42,0x21,0x60,0x73,0x61}};
    CSSM_PVC_MODE policy = CSSM_PVC_NONE;
    provider_status = CSSM_Init(&version,CSSM_PRIVILEGE_SCOPE_PROCESS,&caller,CSSM_KEY_HIERARCHY_NONE,&policy,NULL);
    if(!provider_status) provider_status = CSSM_ModuleLoad(&gGuidAppleCSP,CSSM_KEY_HIERARCHY_NONE,NULL,NULL);
}
static size_t der_length(uint8_t *out,size_t n) {
    if(n<128) { out[0]=(uint8_t)n; return 1; }
    if(n<256) { out[0]=0x81; out[1]=(uint8_t)n; return 2; }
    out[0]=0x82; out[1]=(uint8_t)(n>>8); out[2]=(uint8_t)n; return 3;
}
int32_t aibot_ard_agree(const uint8_t *prime,size_t length,uint16_t generator,
                      const uint8_t *peer,uint8_t *public_value,uint8_t *shared) {
    if(!prime||!peer||!public_value||!shared||length<256||length>512||length%128||generator<2||!(prime[0]&128)||!(prime[length-1]&1)) return -1;
    uint8_t limit[512]; memcpy(limit,prime,length); limit[length-1]--;
    size_t leading=0; while(leading<length&&peer[leading]==0)leading++;
    if(leading==length||(leading==length-1&&peer[leading]<2)||memcmp(peer,limit,length)>=0)return -1;
    pthread_once(&once,initialize_provider);
    if(provider_status)return provider_status;
    CSSM_VERSION version={2,0}; CSSM_API_MEMORY_FUNCS memory={allocate,release,resize,zero_allocate,NULL};
    CSSM_CSP_HANDLE csp=0;
    CSSM_RETURN result=CSSM_ModuleAttach(&gGuidAppleCSP,&version,&memory,0,CSSM_SERVICE_CSP,0,CSSM_KEY_HIERARCHY_NONE,NULL,0,NULL,&csp);
    if(result)return result;
    uint8_t body[540],params[544]; size_t b=0;
    body[b++]=2; b+=der_length(body+b,length+1); body[b++]=0; memcpy(body+b,prime,length); b+=length;
    uint8_t g[3]; size_t gn=0;
    if(generator>255)g[gn++]=(uint8_t)(generator>>8);
    g[gn++]=(uint8_t)generator;
    if(g[0]&128) { memmove(g+1,g,gn); g[0]=0; gn++; }
    body[b++]=2; b+=der_length(body+b,gn); memcpy(body+b,g,gn); b+=gn;
    size_t n=0; params[n++]=0x30; n+=der_length(params+n,b); memcpy(params+n,body,b); n+=b;
    CSSM_DATA parameters={n,params}; CSSM_CC_HANDLE gen=0,derive=0;
    CSSM_KEY pub={0},priv={0},secret={0};
    result=CSSM_CSP_CreateKeyGenContext(csp,CSSM_ALGID_DH,(uint32)(length*8),NULL,NULL,NULL,NULL,&parameters,&gen);
    if(!result)result=CSSM_GenerateKeyPair(gen,CSSM_KEYUSE_DERIVE,CSSM_KEYATTR_RETURN_DATA|CSSM_KEYATTR_EXTRACTABLE,NULL,&pub,CSSM_KEYUSE_DERIVE,CSSM_KEYATTR_RETURN_REF,NULL,NULL,&priv);
    /* The MAC algorithm is only an exportable byte-container type required by
     * CSP, not an HMAC operation. ARD needs the full DH secret for its own KDF. */
    if(!result)result=CSSM_CSP_CreateDeriveKeyContext(csp,CSSM_ALGID_DH,CSSM_ALGID_MD5HMAC,(uint32)(length*8),NULL,&priv,0,NULL,NULL,&derive);
    CSSM_DATA peer_data={length,(uint8_t *)peer};
    if(!result)result=CSSM_DeriveKey(derive,&peer_data,CSSM_KEYUSE_ANY,CSSM_KEYATTR_RETURN_DATA|CSSM_KEYATTR_EXTRACTABLE,NULL,NULL,&secret);
    if(!result) {
        if(pub.KeyHeader.Format!=CSSM_KEYBLOB_RAW_FORMAT_PKCS3||pub.KeyData.Length>length||secret.KeyData.Length>length||!pub.KeyData.Length||!secret.KeyData.Length)result=-1;
        else {
            memset(public_value,0,length); memset(shared,0,length);
            memcpy(public_value+length-pub.KeyData.Length,pub.KeyData.Data,pub.KeyData.Length);
            memcpy(shared+length-secret.KeyData.Length,secret.KeyData.Data,secret.KeyData.Length);
        }
    }
    if(secret.KeyData.Data) { memset_s(secret.KeyData.Data,secret.KeyData.Length,0,secret.KeyData.Length); CSSM_FreeKey(csp,NULL,&secret,CSSM_FALSE); }
    if(priv.KeyData.Data)CSSM_FreeKey(csp,NULL,&priv,CSSM_FALSE);
    if(pub.KeyData.Data)CSSM_FreeKey(csp,NULL,&pub,CSSM_FALSE);
    if(derive)CSSM_DeleteContext(derive);
    if(gen)CSSM_DeleteContext(gen);
    CSSM_ModuleDetach(csp);
    if(result) { memset_s(public_value,length,0,length); memset_s(shared,length,0,length); }
    return result;
}
#pragma clang diagnostic pop
