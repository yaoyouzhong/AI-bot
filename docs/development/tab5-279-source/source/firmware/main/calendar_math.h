#pragma once
#include <stdbool.h>
#include "calendar_data.h"

static int cal_days(int y,int m) {
    const int days[]={31,28,31,30,31,30,31,31,30,31,30,31};
    return days[m-1]+(m==2&&y%4==0&&(y%100!=0||y%400==0));
}
static int cal_serial(int y,int m,int d) {
    int n=y-1,result=365*n+n/4-n/100+n/400+d-1;
    for(int i=1;i<m;i++)result+=cal_days(y,i);
    return result;
}
static void cal_gregorian(int serial,int *year,int *month,int *day) {
    int y=serial/366+1;
    while(cal_serial(y+1,1,1)<=serial)y++;
    int m=1,left=serial-cal_serial(y,1,1);
    while(left>=cal_days(y,m)){left-=cal_days(y,m);m++;}
    *year=y;*month=m;*day=left+1;
}
static bool cal_lunar(int serial,int *year,int *month,int *day,bool *leap) {
    for(int i=199;i>=0;i--)if(serial>=cal_years[i].start) {
        int left=serial-cal_years[i].start,count=cal_years[i].leap?13:12;
        for(int m=1;m<=count;m++) {
            int length=29+((cal_years[i].months>>(m-1))&1);
            if(left<length) {
                *year=1901+i;*month=m-(cal_years[i].leap&&m>=cal_years[i].leap);
                *day=left+1;*leap=m==cal_years[i].leap;return true;
            }
            left-=length;
        }
        return false;
    }
    return false;
}
// Lunar birthdays use the ordinary month when their leap month is absent;
// a 30th birthday falls on the last day of a 29-day month.
static int cal_lunar_birthday(int year,int month,int day,bool leap) {
    if(year<1901||year>2100||month<1||month>12||day<1||day>30)return -1;
    int i=year-1901,slot=month;
    if(cal_years[i].leap&&month>=cal_years[i].leap)slot++;
    if(leap&&cal_years[i].leap==month+1)slot=month+1;
    int result=cal_years[i].start;
    for(int m=1;m<slot;m++)result+=29+((cal_years[i].months>>(m-1))&1);
    int length=29+((cal_years[i].months>>(slot-1))&1);
    return result+(day>length?length:day)-1;
}
