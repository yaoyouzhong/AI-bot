// City-local provider times, projected onto the clock's existing timezone.
typedef struct {int mode,rise,set,start,end;bool cached;} clock_solar_state;
enum {CLOCK_SOLAR_DEFAULT,CLOCK_SOLAR_NORMAL,CLOCK_SOLAR_DAY,CLOCK_SOLAR_NIGHT};
static int clock_solar_minutes(const char *s) {
    if(strlen(s)!=5||s[2]!=':'||s[0]<'0'||s[0]>'9'||s[1]<'0'||s[1]>'9'||s[3]<'0'||s[3]>'9'||s[4]<'0'||s[4]>'9')return -1;
    int h=(s[0]-'0')*10+s[1]-'0',m=(s[3]-'0')*10+s[4]-'0';return h<24&&m<60?h*60+m:-1;
}
static int clock_solar_wrap(int minutes){return (minutes%1440+1440)%1440;}
// Progress through sunrise-to-sunset, independently of the ordinary clock dial.
// Negative means night; polar day remains daylight without inventing events.
static inline int clock_solar_progress(clock_solar_state state,int minute) {
    if(state.mode==CLOCK_SOLAR_DAY)return 1000;
    if(state.mode==CLOCK_SOLAR_NIGHT)return -1;
    int span=clock_solar_wrap(state.end-state.start),elapsed=clock_solar_wrap(minute-state.start);
    return span&&elapsed<span?elapsed*1000/span:-1;
}
// City-local 24-hour ring: noon above the clock, midnight below it.
// Its scale is independent of the inner dial's ordinary twelve-hour hands.
static int clock_solar_ring_span(clock_solar_state state) {
    if(state.mode==CLOCK_SOLAR_DAY)return 360;
    if(state.mode==CLOCK_SOLAR_NIGHT)return 0;
    int degrees=(clock_solar_wrap(state.end-state.start)+2)/4;
    return degrees<1?1:degrees>359?359:degrees;
}
static int clock_solar_ring_angle(clock_solar_state state,int minute) {
    // Rise/start retain the city-to-clock offset, including default and polar
    // states. Use it for the current point as well as the event positions.
    return clock_solar_wrap(minute+state.rise-state.start-720)/4;
}
static clock_solar_state clock_solar_for(cJSON *weather,time_t display_time,int display_offset) {
    clock_solar_state state={0};cJSON *offset=item(weather,"utcOffsetSeconds");
    int city_offset=display_offset;
    if(cJSON_IsNumber(offset)&&offset->valuedouble>=-50400&&offset->valuedouble<=50400)city_offset=(int)offset->valuedouble;
    int difference=(display_offset-city_offset)/60;
    state.rise=360;state.set=1080;state.start=clock_solar_wrap(360+difference);state.end=clock_solar_wrap(1080+difference);
    time_t city_time=display_time-display_offset+city_offset;struct tm local;gmtime_r(&city_time,&local);
    char date[11];strftime(date,sizeof(date),"%Y-%m-%d",&local);
    cJSON *day=NULL,*entry=NULL;cJSON_ArrayForEach(entry,item(weather,"daily"))if(!strcmp(text(entry,"date"),date)){day=entry;break;}
    if(!day)return state;
    state.cached=cJSON_IsTrue(item(weather,"stale"))||cJSON_IsTrue(item(weather,"dailyStale"));
    cJSON *duration=item(day,"daylightSeconds");
    if(cJSON_IsNumber(duration)&&duration->valuedouble==86400){state.mode=CLOCK_SOLAR_DAY;return state;}
    if(cJSON_IsNumber(duration)&&duration->valuedouble==0){state.mode=CLOCK_SOLAR_NIGHT;return state;}
    int rise=clock_solar_minutes(text(day,"sunrise")),set=clock_solar_minutes(text(day,"sunset"));
    if(rise<0||set<0||rise==set)return state;
    state.mode=CLOCK_SOLAR_NORMAL;state.rise=rise;state.set=set;
    state.start=clock_solar_wrap(rise+difference);state.end=clock_solar_wrap(set+difference);return state;
}
