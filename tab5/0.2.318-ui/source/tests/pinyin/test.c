#undef NDEBUG
#include "tab5_ime.h"
#include "tab5_pinyin_data.h"
#include <assert.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <windows.h>
#include "baseline_dict.inc"
size_t baseline_pinyin_lookup(const char*,tab5_pinyin_candidate*,size_t);
size_t current_count(void);bool current_match(size_t,const char*,const char*,uint32_t,uint32_t);
void pinyin_lifecycle_test(void);unsigned pinyin_allocations(void);
static unsigned queries;
static uint32_t u24(const uint8_t *p){return p[0]|((uint32_t)p[1]<<8)|((uint32_t)p[2]<<16);}
static void compare(const char *s,size_t cap){tab5_pinyin_candidate a[48]={0},b[48]={0};size_t na=baseline_pinyin_lookup(s,a,cap),nb=tab5_pinyin_lookup(s,b,cap);assert(na==nb);for(size_t i=0;i<na;i++)assert(!strcmp(a[i].text,b[i].text)&&a[i].weight==b[i].weight&&a[i].consumed==b[i].consumed);queries++;}
static double bench(bool old){LARGE_INTEGER a,b,f;QueryPerformanceFrequency(&f);QueryPerformanceCounter(&a);const char *q[]={"nihao","zhongguo","shijie","xi'an","zhonghuarenmingongheguo","s","z","x","beijing","shengri","gongzuo","nihaoshijie"};size_t sum=0;for(int r=0;r<300;r++)for(size_t i=0;i<sizeof(q)/sizeof(*q);i++){tab5_pinyin_candidate c[48];sum+=(old?baseline_pinyin_lookup:tab5_pinyin_lookup)(q[i],c,48);}QueryPerformanceCounter(&b);assert(sum);return(double)(b.QuadPart-a.QuadPart)*1000/f.QuadPart;}
int main(void){
 assert(!tab5_pinyin_lookup("nihao",NULL,4));pinyin_lifecycle_test();assert(tab5_pinyin_init());
 tab5_pinyin_data d={0};assert(tab5_pinyin_data_init(&d));size_t n=sizeof(pinyin_dictionary)/sizeof(pinyin_dictionary[0]);assert(n==current_count());
 assert(!memcmp(d.keys,pinyin_keys,sizeof(pinyin_keys))&&!memcmp(d.words,pinyin_words,sizeof(pinyin_words))&&!memcmp(d.rows,pinyin_dictionary,sizeof(pinyin_dictionary)));
 unsigned before=pinyin_allocations();
 for(size_t i=0;i<n;i++){const uint8_t *row=pinyin_dictionary[i];const char *key=pinyin_keys+u24(row),*word=pinyin_words+u24(row+3);uint32_t weight=u24(row+6),bounds=row[9]|((uint32_t)row[10]<<8);assert(current_match(i,key,word,weight,bounds));
  if(!i||strcmp(key,pinyin_keys+u24(pinyin_dictionary[i-1]))){compare(key,1);compare(key,4);compare(key,48);char upper[33];size_t len=strlen(key);for(size_t j=0;j<=len;j++)upper[j]=key[j]>='a'&&key[j]<='z'?key[j]-'a'+'A':key[j];compare(upper,48);}
  if(bounds){char spelling[65];size_t at=0;for(size_t j=0;key[j];j++){if(j&&(bounds&(1u<<j)))spelling[at++]='\'';spelling[at++]=key[j];}spelling[at]=0;compare(spelling,48);}
 }
 for(char a='a';a<='z';a++)for(char b=0;b<27;b++){char p[]={a,b?(char)('a'+b-1):0,0};compare(p,48);}
 const char *invalid[]={NULL,"","'","ni'","'ni","xi''an","ni?hao","ni hao","123","abcdefghijklmnopqrstuvwxyzabcdefg"};for(size_t i=0;i<sizeof(invalid)/sizeof(*invalid);i++)compare(invalid[i],48);compare("nihao",0);
 tab5_pinyin_candidate first[48],second[48];size_t count=tab5_pinyin_lookup("nihao",first,48);char copy[48][32];for(size_t i=0;i<count;i++)strcpy(copy[i],first[i].text);for(int i=0;i<100;i++)tab5_pinyin_lookup("zhongguo",second,48);for(size_t i=0;i<count;i++)assert(!strcmp(copy[i],first[i].text));assert(before==pinyin_allocations());
 double old[5],now[5];for(int i=0;i<5;i++){old[i]=bench(true);now[i]=bench(false);printf("PINYIN_BENCH batch=%d baselineMs=%.3f currentMs=%.3f ratio=%.3f\n",i,old[i],now[i],now[i]/old[i]);}
 printf("PINYIN_LOSSLESS_OK records=%zu queries=%u byte-identical data/candidates/order/weights/consumed; OOM/corrupt/truncated/trailing rejected; stable editor pointers; zero lookup allocations\n",n,queries);
}
