#undef NDEBUG
#include "tab5_ime.h"
#include <assert.h>
#include <stdio.h>
#include <string.h>
#include <time.h>

typedef struct {const char *py,*text;uint32_t weight,boundaries;} pinyin_entry;
#include "baseline_pinyin_dict.inc"
size_t baseline_pinyin_lookup(const char *,tab5_pinyin_candidate *,size_t);
size_t compact_dictionary_count(void);
bool compact_dictionary_matches(size_t,const char *,const char *,uint32_t,uint32_t);
static unsigned queries;
static void compare(const char *input,size_t capacity) {
    tab5_pinyin_candidate a[TAB5_PINYIN_CANDIDATES]={0},b[TAB5_PINYIN_CANDIDATES]={0};
    size_t na=baseline_pinyin_lookup(input,a,capacity),nb=tab5_pinyin_lookup(input,b,capacity);
    assert(na==nb);
    for(size_t i=0;i<na;i++)assert(!strcmp(a[i].text,b[i].text)&&a[i].weight==b[i].weight&&a[i].consumed==b[i].consumed);
    queries++;
}
int main(void) {
    size_t count=sizeof(pinyin_dictionary)/sizeof(pinyin_dictionary[0]);
    assert(count==compact_dictionary_count());
    clock_t started=clock();
    for(size_t i=0;i<count;i++) {
        const pinyin_entry *e=&pinyin_dictionary[i];
        assert(compact_dictionary_matches(i,e->py,e->text,e->weight,e->boundaries));
        if(!i||strcmp(e->py,pinyin_dictionary[i-1].py)) {
            compare(e->py,1);compare(e->py,4);compare(e->py,TAB5_PINYIN_CANDIDATES);
            char upper[TAB5_PINYIN_MAX+1];size_t len=strlen(e->py);
            for(size_t j=0;j<=len;j++)upper[j]=e->py[j]>='a'&&e->py[j]<='z'?e->py[j]-'a'+'A':e->py[j];
            compare(upper,TAB5_PINYIN_CANDIDATES);
        }
        if(e->boundaries) {
            char spelling[TAB5_PINYIN_MAX*2+1];size_t at=0;
            for(size_t j=0;e->py[j];j++) {
                if(j&&(e->boundaries&(1u<<j)))spelling[at++]='\'';
                spelling[at++]=e->py[j];
            }
            spelling[at]=0;compare(spelling,TAB5_PINYIN_CANDIDATES);
        }
    }
    for(char a='a';a<='z';a++)for(char b=0;b<27;b++) {
        char prefix[]={a,b?(char)('a'+b-1):0,0};compare(prefix,TAB5_PINYIN_CANDIDATES);
    }
    const char *invalid[]={NULL,"","'","ni'","'ni","xi''an","ni?hao","ni hao","123","ni\xE4\xBD\xA0","abcdefghijklmnopqrstuvwxyzabcdefg"};
    for(size_t i=0;i<sizeof(invalid)/sizeof(*invalid);i++)compare(invalid[i],TAB5_PINYIN_CANDIDATES);
    compare("nihao",0);
    assert(!tab5_pinyin_lookup("nihao",NULL,4));
    printf("PINYIN_LOSSLESS_OK %zu exact records, %u queries, all candidates/weights/order/consumed preserved, %.3fs\n",count,queries,(double)(clock()-started)/CLOCKS_PER_SEC);
}
