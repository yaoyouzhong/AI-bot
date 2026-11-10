#pragma once
#include <stdbool.h>
#include <stdint.h>

/* Relative timers use monotonic milliseconds; date countdowns use civil days. */
typedef enum {COUNTDOWN_READY,COUNTDOWN_RUNNING,COUNTDOWN_PAUSED,COUNTDOWN_DONE} countdown_phase;
typedef struct {int64_t remaining_ms,deadline_ms;uint32_t seconds;countdown_phase phase;} countdown_timer;
static inline void countdown_reset(countdown_timer *t,uint32_t seconds) {
    *t=(countdown_timer){.seconds=seconds,.remaining_ms=(int64_t)seconds*1000};
}
static inline bool countdown_tick(countdown_timer *t,int64_t now) {
    if(t->phase!=COUNTDOWN_RUNNING)return false;
    t->remaining_ms=t->deadline_ms>now?t->deadline_ms-now:0;
    if(t->remaining_ms)return false;
    t->phase=COUNTDOWN_DONE;return true;
}
static inline void countdown_start(countdown_timer *t,int64_t now) {
    if(t->phase==COUNTDOWN_RUNNING||!t->seconds)return;
    if(t->phase==COUNTDOWN_DONE)t->remaining_ms=(int64_t)t->seconds*1000;
    t->deadline_ms=now+t->remaining_ms;t->phase=COUNTDOWN_RUNNING;
}
static inline void countdown_pause(countdown_timer *t,int64_t now) {
    if(t->phase==COUNTDOWN_RUNNING&&!countdown_tick(t,now))t->phase=COUNTDOWN_PAUSED;
}
static inline int countdown_month_days(int year,int month) {
    static const uint8_t days[]={31,28,31,30,31,30,31,31,30,31,30,31};
    if(month<1||month>12)return 0;
    return days[month-1]+(month==2&&year%4==0&&(year%100!=0||year%400==0));
}
static inline bool countdown_date_valid(int year,int month,int day) {
    return year>=2000&&year<=2199&&day>=1&&day<=countdown_month_days(year,month);
}
static inline int countdown_civil_day(int year,int month,int day) {
    year-=month<=2;
    int era=year/400,yoe=year-era*400;
    int doy=(153*(month+(month>2?-3:9))+2)/5+day-1;
    return era*146097+yoe*365+yoe/4-yoe/100+doy;
}
