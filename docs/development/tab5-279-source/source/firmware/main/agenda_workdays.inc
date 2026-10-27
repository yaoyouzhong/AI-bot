// Same official-year data as the bridge calendar. A separately persisted cache
// keeps offline recurrence independent of the display's current status frame.
static agenda_workday_calendar workday_calendar;
#ifdef ESP_PLATFORM
#include "freertos/FreeRTOS.h"
#include "freertos/portmacro.h"
static portMUX_TYPE workday_gate=portMUX_INITIALIZER_UNLOCKED;
static void workday_lock(void){portENTER_CRITICAL(&workday_gate);}
static void workday_unlock(void){portEXIT_CRITICAL(&workday_gate);}
#else
static atomic_flag workday_gate=ATOMIC_FLAG_INIT;
static void workday_lock(void){while(atomic_flag_test_and_set_explicit(&workday_gate,memory_order_acquire)){};}
static void workday_unlock(void){atomic_flag_clear_explicit(&workday_gate,memory_order_release);}
#endif
static int workday_index(int year,int md) {
    static const int months[]={31,28,31,30,31,30,31,31,30,31,30,31};
    int m=md/100,d=md%100,day=0;if(m<1||m>12||d<1)return -1;
    for(int n=1;n<=m;n++){int length=months[n-1]+(n==2&&year%4==0&&(year%100!=0||year%400==0));if(n==m){if(d>length)return -1;}else day+=length;}
    return day+d-1;
}
bool agenda_workdays_valid(const agenda_workday_calendar *v) {
    if(!v||v->version!=1||v->count>3)return false;
    for(unsigned n=0;n<v->count;n++) {
        if(v->years[n].year<2000||v->years[n].year>2099||(v->years[n].working[45]&0xC0))return false;
        for(unsigned j=0;j<n;j++)if(v->years[n].year==v->years[j].year)return false;
    }
    return true;
}
void agenda_workdays_set(const agenda_workday_calendar *value) {
    if(!agenda_workdays_valid(value))return;
    workday_lock();workday_calendar=*value;workday_unlock();
}
static bool workday_number(const cJSON *value,int *out) {
    if(!cJSON_IsNumber(value)||!isfinite(value->valuedouble)||value->valuedouble<0||value->valuedouble>9999||floor(value->valuedouble)!=value->valuedouble)return false;
    *out=value->valueint;return true;
}
static void workday_bit(agenda_workday_year *year,int day,bool working) {
    uint8_t mask=(uint8_t)(1u<<(day%8));if(working)year->working[day/8]|=mask;else year->working[day/8]&=(uint8_t)~mask;
}
bool agenda_workdays_parse(const cJSON *years,agenda_workday_calendar *out) {
    if(!out||!cJSON_IsArray(years)||cJSON_GetArraySize(years)>3)return false;
    memset(out,0,sizeof(*out));out->version=1;out->count=cJSON_GetArraySize(years);
    for(unsigned n=0;n<out->count;n++) {
        const cJSON *source=cJSON_GetArrayItem(years,n),*holidays=cJSON_GetObjectItemCaseSensitive(source,"holidays"),*workdays=cJSON_GetObjectItemCaseSensitive(source,"workdays");int year;
        int ranges=cJSON_GetArraySize(holidays);
        if(!workday_number(cJSON_GetObjectItemCaseSensitive(source,"year"),&year)||year<2000||year>2099||
            !cJSON_IsArray(holidays)||ranges<6||ranges>16||!cJSON_IsArray(workdays)||cJSON_GetArraySize(workdays)>16)return false;
        // A combined National Day/Mid-Autumn break can encode all seven
        // holidays in six ranges. Require all names before accepting that form.
        if(ranges==6) {
            const char *required[]={"元旦","春节","清明","劳动","端午","中秋","国庆"};
            for(unsigned holiday=0;holiday<7;holiday++) {
                bool found=false;const cJSON *range=NULL;cJSON_ArrayForEach(range,holidays) {
                    const cJSON *name=cJSON_GetObjectItemCaseSensitive(range,"name");
                    if(cJSON_IsString(name)&&strstr(name->valuestring,required[holiday]))found=true;
                }
                if(!found)return false;
            }
        }
        agenda_workday_year *target=&out->years[n];target->year=year;
        int previous=year-1,start_weekday=(365*previous+previous/4-previous/100+previous/400+1)%7;
        for(int day=0;day<=workday_index(year,1231);day++)workday_bit(target,day,(start_weekday+day)%7!=0&&(start_weekday+day)%7!=6);
        const cJSON *entry=NULL;cJSON_ArrayForEach(entry,holidays) {
            int first,last;if(!workday_number(cJSON_GetObjectItemCaseSensitive(entry,"start"),&first)||!workday_number(cJSON_GetObjectItemCaseSensitive(entry,"end"),&last))return false;
            first=workday_index(year,first);last=workday_index(year,last);if(first<0||last<first||last-first>15)return false;
            for(int day=first;day<=last;day++)workday_bit(target,day,false);
        }
        cJSON_ArrayForEach(entry,workdays){int md;if(!workday_number(entry,&md))return false;int day=workday_index(year,md);if(day<0)return false;workday_bit(target,day,true);}
    }
    return agenda_workdays_valid(out);
}
int agenda_workday_at(int64_t occurrence) {
    time_t stamp=(time_t)occurrence;struct tm date;
#ifdef _WIN32
    if(gmtime_s(&date,&stamp))return -1;
#else
    if(!gmtime_r(&stamp,&date))return -1;
#endif
    int year=date.tm_year+1900,result=-1;
    workday_lock();for(unsigned n=0;n<workday_calendar.count;n++)if(workday_calendar.years[n].year==(uint32_t)year){result=(workday_calendar.years[n].working[date.tm_yday/8]>>(date.tm_yday%8))&1;break;}workday_unlock();
    if(result>=0)return result;
    // State Council notice 国办发明电〔2025〕7号, 2025-11-04.
    // https://www.gov.cn/zhengce/zhengceku/202511/content_7047091.htm
    if(year!=2026)return -1;
    int md=(date.tm_mon+1)*100+date.tm_mday;
    const int on[]={104,214,228,509,920,1010};for(unsigned n=0;n<sizeof(on)/sizeof(on[0]);n++)if(md==on[n])return 1;
    const int off[][2]={{101,103},{215,223},{404,406},{501,505},{619,621},{925,927},{1001,1007}};
    for(unsigned n=0;n<sizeof(off)/sizeof(off[0]);n++)if(md>=off[n][0]&&md<=off[n][1])return 0;
    return date.tm_wday!=0&&date.tm_wday!=6;
}
