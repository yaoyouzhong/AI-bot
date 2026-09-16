#include <cstdio>
#include "../firmware/include/LunarCalendar.h"
#include "../firmware/include/LunarText.h"
#include "../firmware/include/ScreenSaverGeometry.h"
int main(int argc, char** argv) {
    if(argc != 2) return 2;
    FILE* file = fopen(argv[1],"r"); if(!file) return 3;
    int y,m,d,lm,ld,leap,count=0;
    while(fscanf(file,"%d,%d,%d,%d,%d,%d",&y,&m,&d,&lm,&ld,&leap)==6) {
        auto value=LunarCalendar::convert(y,m,d);
        if(value.month!=lm || value.day!=ld || value.leap!=bool(leap)) {
            printf("MISMATCH %d-%d-%d\n",y,m,d); return 4;
        }
        count++;
    }
    fclose(file);
    if(count<73000 || LunarCalendar::convert(1901,2,18).month || LunarCalendar::convert(2101,1,29).month || LunarCalendar::convert(2026,2,30).month) return 5;
    for(int tick=0;tick<1000;tick++) {
        int x=6+ScreenSaverGeometry::bounce(tick,2,24),y=12+ScreenSaverGeometry::bounce(tick,1,ScreenSaverGeometry::VerticalTravel);
        if(x<6 || x+204>234 || y+ScreenSaverGeometry::GroupHeight>216) return 6;
        bool clipped=false;
        LunarText::draw("农历闰腊月廿九",x+(204+33)/2,y+115,[&](int a,int b,int c,int d){if(a<0||c>=240||b<0||d>=219)clipped=true;});
        if(clipped) return 7;
    }
    printf("FIRMWARE_LUNAR_OK %d dates vs .NET; leap months, bounds and glyph geometry\n",count);
}
