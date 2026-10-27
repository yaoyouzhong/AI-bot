#undef NDEBUG
#include <assert.h>
#include <stdio.h>
#include <string.h>
#ifdef _WIN32
#include <fcntl.h>
#include <io.h>
#endif
#include "ota_usb_append_under_test.inc"

// Exercise the extracted production helper on a complete input frame. Binary
// stdio keeps Windows from rewriting the newline or changing byte capacities.
static int append_cli(const char *suffix) {
#ifdef _WIN32
    if (_setmode(_fileno(stdin), _O_BINARY) < 0 ||
        _setmode(_fileno(stdout), _O_BINARY) < 0) return 2;
#endif
    char frame[4096];
    size_t used=fread(frame,1,sizeof(frame)-1,stdin);
    if(ferror(stdin)||fgetc(stdin)!=EOF||ferror(stdin)||memchr(frame,0,used)) {
        fputs("Expected one complete frame of at most 4095 bytes without NUL.\n",stderr);
        return 2;
    }
    frame[used]=0;
    size_t result=append_ota_performance(frame,sizeof(frame),used,suffix);
    if(fwrite(frame,1,result,stdout)!=result||fflush(stdout)==EOF)return 2;
    return 0;
}

int main(int argc,char **argv) {
    if(argc==3&&!strcmp(argv[1],"--append"))return append_cli(argv[2]);
    if(argc!=1){fputs("Usage: ota-usb-diagnostic-test [--append suffix]\n",stderr);return 2;}
    const char *base="@AIBOT {\"displayDiag\":\"d,1;flash,2,3\",\"otaDiag\":\"1,2,3,0\",\"hostNonce\":\"nonce\",\"currentHostId\":\"host\",\"usbHostAuthenticated\":true}\n";
    const char *suffix=";op,0,1,1,7340032,49152,1,2,7184304,147,0,0,0,0,0,0,0,0,7,400000,0,0,1,0,0,500000,0,0";
    char frame[4096],before[4096];size_t used=strlen(base),extra=strlen(suffix);
    for(size_t spare=0;spare<extra+2;spare++) {
        memset(frame,0x5a,sizeof(frame));memcpy(frame,base,used+1);
        size_t capacity=used+spare;size_t result=append_ota_performance(frame,capacity,used,suffix);
        if(spare<=extra){assert(result==used&&!strcmp(frame,base));}
        else {
            assert(result==used+extra&&frame[result]==0);
            assert(strstr(frame,"d,1;flash,2,3;op,0,1,"));
            assert(strstr(frame,"\"otaDiag\":\"1,2,3,0\""));
            assert(strstr(frame,"\"hostNonce\":\"nonce\",\"currentHostId\":\"host\",\"usbHostAuthenticated\":true}\n"));
        }
        if(capacity>used)assert((unsigned char)frame[capacity]==0x5a);
    }
    // Enforce the live bridge's per-diagnostic 1024-character limit,
    // independently of whole-frame room. Exact boundary stays readable.
    for(size_t chars=1024-extra;chars<=1025-extra;chars++) {
        strcpy(frame,"@AIBOT {\"displayDiag\":\"");size_t prefix=strlen(frame);
        memset(frame+prefix,'1',chars);strcpy(frame+prefix+chars,"\",\"otaDiag\":\"1,2,3,0\",\"hostNonce\":\"nonce\"}\n");
        used=strlen(frame);strcpy(before,frame);
        size_t result=append_ota_performance(frame,sizeof(frame),used,suffix);
        if(chars+extra==1024)assert(result==used+extra&&strstr(frame,suffix));
        else assert(result==used&&!strcmp(frame,before));
    }
    strcpy(frame,base);used=strlen(frame);
    assert(append_ota_performance(frame,sizeof(frame),used,"")==used&&!strcmp(frame,base));
    assert(append_ota_performance(frame,sizeof(frame),used,NULL)==used&&!strcmp(frame,base));
    strcpy(frame,"@AIBOT {\"otaDiag\":\"1,2,3,0\"}\n");used=strlen(frame);strcpy(before,frame);
    assert(append_ota_performance(frame,sizeof(frame),used,suffix)==used&&!strcmp(frame,before));
    // A full legal packet cannot lose its binding or become truncated JSON.
    strcpy(frame,base);memset(frame+strlen(base)-2,' ',4095-(strlen(base)-2));frame[4095]=0;
    strcpy(before,frame);
    assert(append_ota_performance(frame,sizeof(frame),4095,suffix)==4095&&!strcmp(frame,before));
    puts("OTA_USB_DIAGNOSTIC_OK actual appendix; Stop fields unchanged; nonce retained; exact frame and diagnostic boundaries; optional omission");
    return 0;
}
