#include "mbedtls/sha256.h"

// Project pagination IDs are SHA-256(root + ":page:" + zeroBasedPage).
// Merge only a fully verifiable page ID; names alone never prove identity.
typedef struct { char id[17],root[17],name[160]; } workspace_project_group;
static workspace_project_group workspace_project_groups[24];
static int workspace_project_groups_count;

static void workspace_group_copy(char *out,size_t capacity,const char *input) {
    size_t length=strnlen(input,capacity-1);
    if(input[length])while(length&&((unsigned char)input[length]&0xc0)==0x80)length--;
    memcpy(out,input,length);out[length]=0;
}
static bool workspace_page_name(const char *input,int *page,int *total,char *name,size_t capacity) {
    int position=0,used=0,n=0,whole=0;
    if(sscanf(input,"%d/%d · %n",&n,&whole,&used)==2&&used>0&&input[used]) {
        position=used;snprintf(name,capacity,"%s",input+position);
    } else {
        const char *separator=strstr(input," · ");const char *last=NULL;
        while(separator){last=separator;separator=strstr(separator+3," · ");}
        if(!last||sscanf(last," · %d/%d%n",&n,&whole,&used)!=2||last[used])return false;
        size_t length=(size_t)(last-input);if(!length||length>=capacity)return false;
        memcpy(name,input,length);name[length]=0;
    }
    if(n<1||whole<2||whole>24||n>whole)return false;
    *page=n;*total=whole;return true;
}
static bool workspace_page_id(const char *root,int page,const char *id) {
    char input[40],expected[17];uint8_t digest[32];
    snprintf(input,sizeof(input),"%.16s:page:%d",root,page-1);
    if(mbedtls_sha256((const uint8_t *)input,strlen(input),digest,0))return false;
    for(int i=0;i<8;i++)snprintf(expected+2*i,3,"%02x",digest[i]);
    return !strcmp(expected,id);
}
static void workspace_groups_build(cJSON *projects) {
    workspace_project_groups_count=0;
    cJSON *entry=NULL;
    cJSON_ArrayForEach(entry,projects) {
        if(workspace_project_groups_count>=24)break;
        const char *id=text(entry,"id"),*label=text(entry,"name");
        if(strlen(id)!=16||!strcmp(label,"--"))continue;
        workspace_project_group *g=&workspace_project_groups[workspace_project_groups_count++];
        snprintf(g->id,sizeof(g->id),"%s",id);snprintf(g->root,sizeof(g->root),"%s",id);
        workspace_group_copy(g->name,sizeof(g->name),label);
    }
    for(int i=0;i<workspace_project_groups_count;i++) {
        int page,total;char name[160];
        workspace_project_group *g=&workspace_project_groups[i];
        if(!workspace_page_name(g->name,&page,&total,name,sizeof(name))||page!=1)continue;
        for(int j=0;j<workspace_project_groups_count;j++) {
            int otherPage,otherTotal;char otherName[160];
            workspace_project_group *other=&workspace_project_groups[j];
            if(j==i||!workspace_page_name(other->name,&otherPage,&otherTotal,otherName,sizeof(otherName))||
               otherPage<=1||otherTotal!=total||strcmp(name,otherName)||!workspace_page_id(g->id,otherPage,other->id))continue;
            memcpy(other->root,g->id,sizeof(other->root));
        }
        // Only change the label when at least one page was verified.
        bool merged=false;
        for(int j=0;j<workspace_project_groups_count;j++)if(j!=i&&!strcmp(workspace_project_groups[j].root,g->id))merged=true;
        if(merged)snprintf(g->name,sizeof(g->name),"%s",name);
    }
}
static const char *workspace_project_root(const char *id) {
    for(int i=0;i<workspace_project_groups_count;i++)
        if(!strcmp(workspace_project_groups[i].id,id))return workspace_project_groups[i].root;
    return id;
}
