#pragma once
#include <stdbool.h>
#include <stdint.h>
#include <time.h>
#include "countdown.h"

// RX8130CE application manual ETM50E-10: calendar 10h..16h,
// VLF=bit1 at 1Dh, STOP=bit6 at 1Eh; years are 2000..2099.
static inline int gallery_clock_bcd(uint8_t value) {
    if((value&15)>9||(value>>4)>9)return -1;
    return (value>>4)*10+(value&15);
}
static inline uint8_t gallery_clock_encode_bcd(int value){return (uint8_t)((value/10<<4)|(value%10));}
static inline bool gallery_clock_decode(const uint8_t data[7],uint8_t flags,uint8_t control,int64_t *ms) {
    if(!data||!ms||(flags&2)||(control&0xc0))return false;
    int second=gallery_clock_bcd(data[0]),minute=gallery_clock_bcd(data[1]),hour=gallery_clock_bcd(data[2]);
    int day=gallery_clock_bcd(data[4]),month=gallery_clock_bcd(data[5]),year=gallery_clock_bcd(data[6]);
    if(second<0||second>59||minute<0||minute>59||hour<0||hour>23||year<0||
       !countdown_date_valid(year+2000,month,day))return false;
    // Do not depend on the process TZ or accept malformed weekday bits.
    if(!data[3]||(data[3]&0x80)||(data[3]&(data[3]-1)))return false;
    *ms=((int64_t)(countdown_civil_day(year+2000,month,day)-countdown_civil_day(1970,1,1))*86400+hour*3600+minute*60+second)*1000;
    return true;
}
static inline bool gallery_clock_encode(int64_t ms,uint8_t data[7]) {
    time_t second=(time_t)(ms/1000);struct tm local;
    if(ms<0||!data||!gmtime_r(&second,&local)||local.tm_year<100||local.tm_year>199)return false;
    data[0]=gallery_clock_encode_bcd(local.tm_sec);data[1]=gallery_clock_encode_bcd(local.tm_min);
    data[2]=gallery_clock_encode_bcd(local.tm_hour);data[3]=(uint8_t)(1u<<local.tm_wday);
    data[4]=gallery_clock_encode_bcd(local.tm_mday);data[5]=gallery_clock_encode_bcd(local.tm_mon+1);
    data[6]=gallery_clock_encode_bcd(local.tm_year-100);return true;
}
