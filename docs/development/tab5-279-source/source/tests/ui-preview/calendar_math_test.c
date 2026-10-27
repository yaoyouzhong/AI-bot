#undef NDEBUG
#include <assert.h>
#include <stdio.h>
#include "../../firmware/main/calendar_math.h"
int main(int argc,char **argv) {
    FILE *out=argc>1?fopen(argv[1],"wb"):NULL;if(argc>1)assert(out);
    unsigned count=0;
    for(int y=1902;y<=2099;y++)for(int m=1;m<=12;m++)for(int d=1;d<=cal_days(y,m);d++) {
        int serial=cal_serial(y,m,d),gy,gm,gd,ly,lm,ld;bool leap;
        cal_gregorian(serial,&gy,&gm,&gd);assert(gy==y&&gm==m&&gd==d);
        assert(cal_lunar(serial,&ly,&lm,&ld,&leap));
        assert(cal_lunar_birthday(ly,lm,ld,leap)==serial);
        if(out)fprintf(out,"%d,%d,%d,%d,%d,%d,%d\n",y,m,d,ly,lm,ld,leap?1:0);
        count++;
    }
    assert(cal_lunar_birthday(2026,1,1,false)==cal_serial(2026,2,17));
    assert(cal_lunar_birthday(2025,6,1,true)==cal_serial(2025,7,25));
    assert(cal_lunar_birthday(2026,6,1,true)==cal_lunar_birthday(2026,6,1,false));
    if(out)fclose(out);
    printf("CALENDAR_MATH_OK %u days; Gregorian and lunar round trips, leap month fallback\n",count);
}
