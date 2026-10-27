// Original vector dials. Existing style IDs 0..4 retain their meaning.
LV_FONT_DECLARE(tab5_timer_digits_192);
#include "ui_clock_solar.h"
typedef struct {uint32_t face,ink,muted,accent,rim;} clock_palette;
static const char *clock_style_names[UI_CLOCK_STYLES]={
    "经典圆盘","纯数字","轨道圆盘","奶油刻度","方形表盘","昼夜环","几何玩色","星轨","液晶时光"};
static const clock_palette clock_palettes[UI_CLOCK_STYLES]={
    {0x253136,0xEAF0ED,0x7B939C,0xA5D6E9,0x657D88},
    {UI_BACKGROUND,INK,MUTED,BLUE,TRACK},
    {0x20353B,0xE2EEE8,0x638C94,0xA8D6BB,0x466E79},
    {0xEEE5D3,0x36444A,0xA09581,0xB66351,0xD0C1A4},
    {0x292F35,0xE7EBE9,0x7A8994,0xC8BAD7,0x5E6F7A},
    {0x243139,0xF0E7CF,0x718995,0xE9C48C,0x49616C},
    {0x29373A,0xE9EEDE,0x607B80,0xE8BD73,0x4C676B},
    {0x202E38,0xE1EAEF,0x60798A,0xB9DCEB,0x3B5362},
    {0x2C3336,0x263520,0x98AD78,0x263520,0x78857B}};
static lv_obj_t *clock_seconds,*clock_lunar,*clock_holiday,*clock_rule,*clock_controls,*clock_style_index;
static lv_obj_t *clock_satellites[3],*clock_inlays[2],*clock_picker,*clock_picker_hint,*clock_choices[UI_CLOCK_STYLES],*clock_thumbnails[UI_CLOCK_STYLES];
static struct tm clock_face_time;
static bool clock_face_has_time;
static clock_solar_state clock_solar={.rise=360,.set=1080,.start=360,.end=1080};
static lv_obj_t *clock_solar_times[2],*clock_solar_icons[2],*clock_solar_hours[5],*clock_lcd_labels[3];
static int clock_solar_minute;
static void clock_layout(void);

static lv_point_t clock_polar(int cx,int cy,int radius,int angle) {
    return (lv_point_t){cx+lv_trigo_sin(angle)*radius/32768,cy-lv_trigo_cos(angle)*radius/32768};
}
static void clock_line(lv_layer_t *layer,int x1,int y1,int x2,int y2,int width,uint32_t color,lv_opa_t opa) {
    lv_draw_line_dsc_t d;lv_draw_line_dsc_init(&d);
    d.p1=(lv_point_precise_t){x1,y1};d.p2=(lv_point_precise_t){x2,y2};
    d.width=width;d.color=lv_color_hex(color);d.opa=opa;d.round_start=d.round_end=1;
    lv_draw_line(layer,&d);
}
static void clock_disk(lv_layer_t *layer,int x,int y,int radius,uint32_t color,lv_opa_t opa) {
    lv_draw_rect_dsc_t d;lv_draw_rect_dsc_init(&d);d.radius=LV_RADIUS_CIRCLE;
    d.bg_color=lv_color_hex(color);d.bg_opa=opa;
    lv_area_t a={x-radius,y-radius,x+radius-1,y+radius-1};lv_draw_rect(layer,&d,&a);
}
static void clock_arc(lv_layer_t *layer,int x,int y,int radius,int start,int end,int width,uint32_t color,lv_opa_t opa) {
    lv_draw_arc_dsc_t d;lv_draw_arc_dsc_init(&d);d.center=(lv_point_t){x,y};
    d.radius=radius;d.start_angle=start;d.end_angle=end;d.width=width;
    d.color=lv_color_hex(color);d.opa=opa;d.rounded=1;lv_draw_arc(layer,&d);
}
static void clock_solar_symbol(lv_layer_t *layer,int x,int y,int radius,bool moon,uint32_t color,uint32_t background) {
    if(moon) {
        clock_disk(layer,x,y,radius,color,LV_OPA_COVER);
        clock_disk(layer,x+radius/2,y-radius/3,radius*7/8,background,LV_OPA_COVER);
    } else {
        clock_disk(layer,x,y,LV_MAX(1,radius/2),color,LV_OPA_COVER);
        if(radius>3)for(int ray=0;ray<8;ray++) {
            lv_point_t from=clock_polar(x,y,radius*3/4,ray*45),to=clock_polar(x,y,radius,ray*45);
            clock_line(layer,from.x,from.y,to.x,to.y,1,color,LV_OPA_COVER);
        }
    }
}
static void clock_solar_icon_draw(lv_event_t *e) {
    lv_obj_t *o=lv_event_get_target(e);lv_area_t a;lv_obj_get_coords(o,&a);
    bool moon=(intptr_t)lv_event_get_user_data(e)!=0;
    clock_solar_symbol(lv_event_get_layer(e),a.x1+14,a.y1+14,11,moon,moon?0xDFE9F3:0xDDB97D,UI_BACKGROUND);
}
static void clock_rect(lv_layer_t *layer,int x,int y,int w,int h,int radius,uint32_t color) {
    lv_draw_rect_dsc_t d;lv_draw_rect_dsc_init(&d);d.radius=radius;d.bg_color=lv_color_hex(color);d.bg_opa=LV_OPA_COVER;
    lv_area_t a={x,y,x+w-1,y+h-1};lv_draw_rect(layer,&d,&a);
}
static void clock_triangle(lv_layer_t *layer,int x1,int y1,int x2,int y2,int x3,int y3,uint32_t color) {
    lv_draw_triangle_dsc_t d;lv_draw_triangle_dsc_init(&d);d.bg_color=lv_color_hex(color);d.bg_opa=LV_OPA_COVER;
    d.p[0]=(lv_point_precise_t){x1,y1};d.p[1]=(lv_point_precise_t){x2,y2};d.p[2]=(lv_point_precise_t){x3,y3};lv_draw_triangle(layer,&d);
}
static void clock_lcd_segment(lv_layer_t *layer,int x,int y,int w,int h,int thickness,bool vertical,uint32_t color) {
    int t=thickness;
    if(vertical) {
        clock_rect(layer,x,y+t,w,h-2*t,0,color);
        clock_triangle(layer,x,y+t,x+w-1,y+t,x+w/2,y+t/2,color);
        clock_triangle(layer,x,y+h-t-1,x+w-1,y+h-t-1,x+w/2,y+h-t/2-1,color);
    } else {
        clock_rect(layer,x+t,y,w-2*t,h,0,color);
        clock_triangle(layer,x+t,y,x+t,y+h-1,x+t/2,y+h/2,color);
        clock_triangle(layer,x+w-t-1,y,x+w-t-1,y+h-1,x+w-t/2-1,y+h/2,color);
    }
}
static uint8_t clock_lcd_mask(int digit) {
    static const uint8_t masks[]={0x3F,0x06,0x5B,0x4F,0x66,0x6D,0x7D,0x07,0x7F,0x6F};
    return digit>=0&&digit<10?masks[digit]:0x40;
}
static void clock_lcd_digit(lv_layer_t *layer,int digit,int x,int y,int w,int h,int t) {
    uint8_t mask=clock_lcd_mask(digit);
    int segment[7][5]={{x,y,w,t,0},{x+w-t,y,t,h/2,1},{x+w-t,y+h/2,t,h/2,1},
        {x,y+h-t,w,t,0},{x,y+h/2,t,h/2,1},{x,y,t,h/2,1},{x,y+h/2-t/2,w,t,0}};
    for(int i=0;i<7;i++)clock_lcd_segment(layer,segment[i][0],segment[i][1],segment[i][2],segment[i][3],t,segment[i][4],mask&(1<<i)?0x263520:0x98AD78);
}
static void clock_lcd_draw(lv_layer_t *layer,const lv_area_t *a,int size) {
    bool thumb=size<200;int x=a->x1,y=a->y1;
    clock_rect(layer,x+size*20/428,y+size*74/428,size*388/428,size*276/428,thumb?4:14,0x151E20);
    clock_rect(layer,x+size*26/428,y+size*80/428,size*376/428,size*264/428,thumb?3:10,0xAFC28D);
    int digits[]={clock_face_time.tm_hour/10,clock_face_time.tm_hour%10,clock_face_time.tm_min/10,clock_face_time.tm_min%10};
    int w=thumb?13:60,h=thumb?24:112,t=thumb?2:8,gap=thumb?3:12,colon=thumb?4:24;
    int total=w*4+gap*3+colon,start=x+(size-total)/2,top=y+size*146/428;
    for(int i=0;i<4;i++)clock_lcd_digit(layer,clock_face_has_time?digits[i]:-1,start+i*(w+gap)+(i>=2?colon:0),top,w,h,t);
    int middle=start+2*w+gap+(gap+colon)/2;
    clock_rect(layer,middle-t/2,top+h/3,t,t,thumb?0:1,0x263520);clock_rect(layer,middle-t/2,top+h*2/3,t,t,thumb?0:1,0x263520);
    if(!thumb) {
        clock_lcd_digit(layer,clock_face_has_time?clock_face_time.tm_sec/10:-1,x+303,y+284,21,40,3);
        clock_lcd_digit(layer,clock_face_has_time?clock_face_time.tm_sec%10:-1,x+333,y+284,21,40,3);
        clock_line(layer,x+48,y+132,x+378,y+132,1,0x7E9461,LV_OPA_70);
        clock_line(layer,x+42,y+372,x+386,y+372,1,0x68746B,LV_OPA_50);
    }
}
static void clock_solar_refresh(time_t display_time) {
    cJSON *weather=item(shown,"weather");clock_solar_state next=clock_solar_for(weather,display_time,(int)num(shown,"utcOffsetSeconds"));
    struct tm now;gmtime_r(&display_time,&now);
    int minute=now.tm_hour*60+now.tm_min;
    bool changed=next.mode!=clock_solar.mode||next.rise!=clock_solar.rise||next.set!=clock_solar.set||next.start!=clock_solar.start||next.end!=clock_solar.end||minute!=clock_solar_minute;
    clock_solar=next;
    clock_solar_minute=minute;
    const char *suffix=next.mode==CLOCK_SOLAR_DEFAULT?" · 默认":next.cached?" · 缓存":"";
    if(next.mode==CLOCK_SOLAR_DAY||next.mode==CLOCK_SOLAR_NIGHT) {ui_text_fmt(clock_solar_times[0],"%s · 日出 --:--",next.mode==CLOCK_SOLAR_DAY?"极昼":"极夜");ui_text(clock_solar_times[1],"日落 --:--");}
    else {ui_text_fmt(clock_solar_times[0],"日出 %02d:%02d%s",next.rise/60,next.rise%60,suffix);ui_text_fmt(clock_solar_times[1],"日落 %02d:%02d",next.set/60,next.set%60);}
    if(changed&&clock_style==5)lv_obj_invalidate(clock_face);
    if(changed&&clock_thumbnails[5])lv_obj_invalidate(clock_thumbnails[5]);
}
static int clock_tick_radius(int style,int angle,int radius) {
    if(style!=4)return radius;
    int s=LV_ABS(lv_trigo_sin(angle)),c=LV_ABS(lv_trigo_cos(angle));
    return radius*32768/LV_MAX(s,c);
}
static void clock_dial_draw(lv_event_t *e) {
    lv_obj_t *o=lv_event_get_target(e);lv_layer_t *layer=lv_event_get_layer(e);lv_area_t a;lv_obj_get_coords(o,&a);
    int style=(int)(intptr_t)lv_obj_get_user_data(o),size=lv_area_get_width(&a),cx=a.x1+size/2,cy=a.y1+size/2;
    const clock_palette *p=&clock_palettes[style];int r=size/2-12;
    bool thumb=size<200;
    if(style==8){clock_lcd_draw(layer,&a,size);return;}
    if(style!=4&&style!=5)clock_arc(layer,cx,cy,r,0,360,thumb?1:2,p->rim,LV_OPA_COVER);
    if(style==0||style==3) {
        clock_arc(layer,cx,cy,r-6,0,360,1,p->rim,LV_OPA_40);
        clock_arc(layer,cx,cy,r-33,0,360,1,p->rim,LV_OPA_20);
    }
    if(style==2||style==7) {
        int rings[]={size*187/428,size*144/428,size*105/428};
        for(int i=0;i<3;i++)clock_arc(layer,cx,cy,rings[i],0,360,1,p->rim,i?LV_OPA_60:LV_OPA_COVER);
    }
    if(style==5) {
        int outer=size*206/428,band=size*194/428,width=thumb?6:24,span=clock_solar_ring_span(clock_solar);
        int start=clock_solar_ring_angle(clock_solar,clock_solar.start),end=(start+span)%360;
        clock_arc(layer,cx,cy,size*207/428,0,360,1,p->rim,LV_OPA_60);
        clock_arc(layer,cx,cy,outer,0,360,width,0x425C77,LV_OPA_COVER);
        if(span==360)clock_arc(layer,cx,cy,outer,0,360,width,0xDDB97D,LV_OPA_COVER);
        else if(span) {
            int from=(start+270)%360,to=(end+270)%360;
            if(to>from)clock_arc(layer,cx,cy,outer,from,to,width,0xDDB97D,LV_OPA_COVER);
            else {clock_arc(layer,cx,cy,outer,from,360,width,0xDDB97D,LV_OPA_COVER);if(to)clock_arc(layer,cx,cy,outer,0,to,width,0xDDB97D,LV_OPA_COVER);}
        }
        clock_disk(layer,cx,cy,size*162/428,0x202D34,LV_OPA_COVER);
        clock_arc(layer,cx,cy,size*164/428,0,360,1,p->rim,LV_OPA_60);
        if(span&&span<360)for(int edge=0;edge<2;edge++) {
            lv_point_t at=clock_polar(cx,cy,band,edge?end:start);
            clock_disk(layer,at.x,at.y,thumb?4:13,p->face,LV_OPA_COVER);
            clock_solar_symbol(layer,at.x,at.y,thumb?3:10,edge,edge?0xDFE9F3:0xDDB97D,p->face);
        }
        if(clock_face_has_time) {
            lv_point_t at=clock_polar(cx,cy,band,clock_solar_ring_angle(clock_solar,clock_solar_minute));
            clock_disk(layer,at.x,at.y,thumb?3:9,0xF4F7F5,LV_OPA_COVER);clock_disk(layer,at.x,at.y,thumb?1:5,p->face,LV_OPA_COVER);
        }
    }
    if(style==6) {
        clock_disk(layer,cx-size/7,cy-size/8,size/9,0xB8786B,LV_OPA_50);
        clock_disk(layer,cx+size/7,cy+size/9,size/8,0x79A5AE,LV_OPA_40);
        clock_line(layer,cx-size/4,cy+size/7,cx+size/5,cy-size/4,thumb?2:9,0x7B8970,LV_OPA_50);
    }
    if(style==7&&!thumb) {
        static const int stars[][3]={{74,91,2},{116,58,1},{308,86,2},{355,139,1},{332,284,2},{278,350,1},{90,296,1},{149,338,2}};
        for(unsigned i=0;i<sizeof(stars)/sizeof(stars[0]);i++)clock_disk(layer,a.x1+stars[i][0],a.y1+stars[i][1],stars[i][2],p->ink,LV_OPA_50);
        clock_line(layer,a.x1+74,a.y1+91,a.x1+116,a.y1+58,1,p->muted,LV_OPA_40);
        clock_line(layer,a.x1+308,a.y1+86,a.x1+355,a.y1+139,1,p->muted,LV_OPA_40);
        clock_line(layer,cx-10,cy,cx+10,cy,1,p->accent,LV_OPA_80);
        clock_line(layer,cx,cy-10,cx,cy+10,1,p->accent,LV_OPA_80);
    }
    int ticks=60;
    for(int i=0;i<ticks;i++) {
        bool major=i%5==0;int angle=i*360/ticks;
        if((style==7||style==6)&&!major)continue;
        int outer=clock_tick_radius(style,angle,style==5?size*150/428:r-13);
        int length=thumb?(major?4:2):major?(style==6?17:style==5?10:12):4;
        lv_point_t from=clock_polar(cx,cy,outer,angle),to=clock_polar(cx,cy,outer-length,angle);
        clock_line(layer,from.x,from.y,to.x,to.y,thumb?1:major?3:1,major?p->ink:p->muted,major?LV_OPA_COVER:LV_OPA_70);
    }
    if(thumb) {
        int hour=(clock_face_time.tm_hour%12)*30+clock_face_time.tm_min/2;
        lv_point_t h=clock_polar(cx,cy,style==5?size*90/428:size/4,hour),m=clock_polar(cx,cy,style==5?size*130/428:size/3,clock_face_time.tm_min*6);
        if(style==7) {
            clock_disk(layer,h.x,h.y,3,p->ink,LV_OPA_COVER);clock_disk(layer,m.x,m.y,2,p->accent,LV_OPA_COVER);
            lv_point_t s=clock_polar(cx,cy,size*187/428,clock_face_time.tm_sec*6);clock_disk(layer,s.x,s.y,1,p->accent,LV_OPA_COVER);
        } else {
            clock_line(layer,cx,cy,h.x,h.y,3,p->ink,LV_OPA_COVER);
            clock_line(layer,cx,cy,m.x,m.y,2,p->accent,LV_OPA_COVER);
            clock_disk(layer,cx,cy,2,p->accent,LV_OPA_COVER);
        }
    }
}
static lv_obj_t *clock_thumbnail(lv_obj_t *parent,int style) {
    lv_obj_t *o=shape(parent,18,18,96,96,clock_palettes[style].face,style==4||style==8?16:LV_RADIUS_CIRCLE);
    lv_obj_set_user_data(o,(void *)(intptr_t)style);
    if(style==1) {
        lv_obj_t *time=label(o,"--:--",0,0,96,false);ui_text_font(time,&lv_font_montserrat_24,0);lv_obj_center(time);
    } else lv_obj_add_event_cb(o,clock_dial_draw,LV_EVENT_DRAW_MAIN,NULL);
    return o;
}
static UI_INIT_SMALL void analog_create(lv_obj_t *p) {
    clock_face=shape(p,46,36,428,428,clock_palettes[0].face,LV_RADIUS_CIRCLE);
    lv_obj_add_event_cb(clock_face,clock_dial_draw,LV_EVENT_DRAW_MAIN,NULL);
    for(int i=0;i<12;i++) {
        clock_numbers[i]=label(clock_face,"",0,0,52,false);ui_text_font(clock_numbers[i],&lv_font_montserrat_24,0);
        lv_obj_set_style_text_align(clock_numbers[i],LV_TEXT_ALIGN_CENTER,0);
    }
    int lengths[]={105,152,170},widths[]={8,5,2};
    for(int i=0;i<3;i++) {
        clock_hands[i]=shape(clock_face,214-widths[i]/2,214-lengths[i],widths[i],lengths[i]+18,INK,widths[i]/2);
        lv_obj_set_style_transform_pivot_x(clock_hands[i],widths[i]/2,0);lv_obj_set_style_transform_pivot_y(clock_hands[i],lengths[i],0);
        if(i<2)clock_inlays[i]=shape(clock_hands[i],widths[i]/2-1,8,2,lengths[i]-42,clock_palettes[0].face,1);
        clock_satellites[i]=shape(clock_face,0,0,12,12,BLUE,LV_RADIUS_CIRCLE);
    }
    clock_cap=shape(clock_face,207,207,14,14,BLUE,LV_RADIUS_CIRCLE);shape(clock_cap,5,5,4,4,INK,LV_RADIUS_CIRCLE);
    clock_lcd_labels[0]=label(clock_face,"DIGITAL",104,25,220,false);ui_text_font(clock_lcd_labels[0],&lv_font_montserrat_24,0);lv_obj_set_style_text_align(clock_lcd_labels[0],LV_TEXT_ALIGN_CENTER,0);
    clock_lcd_labels[1]=label(clock_face,"24H",48,96,100,false);ui_text_font(clock_lcd_labels[1],&lv_font_montserrat_24,0);
    clock_lcd_labels[2]=label(clock_face,"SEC",231,294,64,false);ui_text_font(clock_lcd_labels[2],&lv_font_montserrat_14,0);
    tint(clock_lcd_labels[0],0xD0D9CE);tint(clock_lcd_labels[1],0x263520);tint(clock_lcd_labels[2],0x263520);
    clock_solar_times[0]=muted(p,"",602,196,260);clock_solar_times[1]=muted(p,"",962,196,182);
    for(int i=0;i<2;i++) {
        clock_solar_icons[i]=shape(p,i?930:570,196,28,28,UI_BACKGROUND,0);
        lv_obj_add_event_cb(clock_solar_icons[i],clock_solar_icon_draw,LV_EVENT_DRAW_MAIN,(void *)(intptr_t)i);
    }
    const char *hours[]={"12","18","00","06"};
    for(int i=0;i<4;i++) {
        lv_point_t at=clock_polar(214,214,173,i*90);
        clock_solar_hours[i]=label(clock_face,hours[i],at.x-17,at.y-8,34,false);
        ui_text_font(clock_solar_hours[i],&lv_font_montserrat_14,0);tint(clock_solar_hours[i],0xBACAD1);
        lv_obj_set_style_text_align(clock_solar_hours[i],LV_TEXT_ALIGN_CENTER,0);
    }
    clock_solar_hours[4]=label(p,"24H",218,6,84,false);ui_text_font(clock_solar_hours[4],&lv_font_montserrat_14,0);
    lv_obj_set_style_text_align(clock_solar_hours[4],LV_TEXT_ALIGN_CENTER,0);tint(clock_solar_hours[4],MUTED);
}
static void analog_style(void) {
    const clock_palette *p=&clock_palettes[clock_style];
    for(int i=0;i<3;i++)visible(clock_lcd_labels[i],clock_style==8);
    for(int i=0;i<2;i++){visible(clock_solar_times[i],clock_style==5);visible(clock_solar_icons[i],clock_style==5);}
    for(int i=0;i<5;i++)visible(clock_solar_hours[i],clock_style==5);
    lv_obj_set_user_data(clock_face,(void *)(intptr_t)clock_style);ui_bg_color(clock_face,lv_color_hex(p->face),0);
    lv_obj_set_style_radius(clock_face,clock_style==4?38:clock_style==8?28:LV_RADIUS_CIRCLE,0);
    lv_obj_set_style_border_width(clock_face,clock_style==4||clock_style==8?2:0,0);ui_border_color(clock_face,lv_color_hex(p->rim),0);
    for(int i=0;i<12;i++) {
        bool numbers=clock_style==3||(i%3==0&&clock_style!=7&&clock_style!=8);
        visible(clock_numbers[i],numbers);ui_text_fmt(clock_numbers[i],"%d",i?i:12);
        lv_point_t at=clock_polar(214,214,clock_style==5?120:151,i*30);
        lv_obj_set_pos(clock_numbers[i],at.x-26,at.y-15);tint(clock_numbers[i],p->ink);
    }
    for(int i=0;i<3;i++) {
        const int lengths[]={105,152,170},solar_lengths[]={90,130,145},widths[]={8,5,2};int length=clock_style==5?solar_lengths[i]:lengths[i];
        lv_obj_set_pos(clock_hands[i],214-widths[i]/2,214-length);lv_obj_set_size(clock_hands[i],widths[i],length+18);lv_obj_set_style_transform_pivot_y(clock_hands[i],length,0);
        visible(clock_hands[i],clock_style!=7&&clock_style!=8&&(i<2||clock_style==0||clock_style==3||clock_style==4||clock_style==5));
        ui_bg_color(clock_hands[i],lv_color_hex(i==2?p->accent:clock_style==6&&i==0?p->accent:p->ink),0);
        visible(clock_satellites[i],clock_style==7||(i==2&&(clock_style==2||clock_style==6)));
        ui_bg_color(clock_satellites[i],lv_color_hex(i==0?p->ink:i==1?BLUE:p->accent),0);
        int dot=clock_style==7?(i==0?18:i==1?12:7):8;lv_obj_set_size(clock_satellites[i],dot,dot);
        if(i<2) {visible(clock_inlays[i],clock_style==0||clock_style==4);ui_bg_color(clock_inlays[i],lv_color_hex(p->face),0);}
    }
    visible(clock_cap,clock_style!=7&&clock_style!=8);ui_bg_color(clock_cap,lv_color_hex(p->accent),0);
    lv_obj_invalidate(clock_face);
}
static void analog_update(struct tm *t,int milliseconds) {
    bool minute_changed=!clock_face_has_time||t->tm_min!=clock_face_time.tm_min||t->tm_hour!=clock_face_time.tm_hour;
    bool progress_changed=!clock_face_has_time||t->tm_sec!=clock_face_time.tm_sec;
    clock_face_time=*t;clock_face_has_time=true;
    if(clock_style==1||clock_style==8) {if(clock_style==8&&(minute_changed||progress_changed))lv_obj_invalidate(clock_face);return;}
    int angles[]={(t->tm_hour%12)*300+t->tm_min*5,t->tm_min*60+t->tm_sec,t->tm_sec*60+milliseconds*60/1000};
    for(int i=0;i<3;i++) {
        if(!lv_obj_has_flag(clock_hands[i],LV_OBJ_FLAG_HIDDEN))lv_obj_set_style_transform_rotation(clock_hands[i],angles[i],0);
        if(!lv_obj_has_flag(clock_satellites[i],LV_OBJ_FLAG_HIDDEN)) {
            int radius=clock_style==7?(i==0?103:i==1?144:187):187;
            // Integer degrees are sufficient for tiny satellites; hands retain 0.1 degree precision.
            lv_point_t at=clock_polar(214,214,radius,angles[i]/10);int size=lv_obj_get_width(clock_satellites[i]);
            if(lv_obj_get_style_x(clock_satellites[i],0)!=at.x-size/2||lv_obj_get_style_y(clock_satellites[i],0)!=at.y-size/2)
                lv_obj_set_pos(clock_satellites[i],at.x-size/2,at.y-size/2);
        }
    }
}
static bool clock_select_style(int style) {
    if(style<0||style>=UI_CLOCK_STYLES)return false;
    if(!ui_clock_style_save(style)) {clock_style_error=true;if(clock_picker_hint)ui_text(clock_picker_hint,"保存失败，请重试");return false;}
    clock_style=style;clock_style_error=false;clock_layout();
    saver_leave();
    last_change=esp_timer_get_time()/1000;return true;
}
static void clock_style_clicked(lv_event_t *e) {(void)e;clock_select_style((clock_style+1)%UI_CLOCK_STYLES);}
static void clock_previous(lv_event_t *e) {(void)e;clock_select_style((clock_style+UI_CLOCK_STYLES-1)%UI_CLOCK_STYLES);}
static void clock_picker_close(lv_event_t *e) {
    (void)e;if(!clock_picker||lv_obj_has_flag(clock_picker,LV_OBJ_FLAG_HIDDEN))return;
    visible(clock_picker,false);lv_display_trigger_activity(NULL);last_change=esp_timer_get_time()/1000;
}
static bool clock_picker_is_open(void){return clock_picker&&!lv_obj_has_flag(clock_picker,LV_OBJ_FLAG_HIDDEN);}
static void clock_picker_choose(lv_event_t *e) {
    if(clock_select_style((int)(intptr_t)lv_event_get_user_data(e)))clock_picker_close(NULL);
}
static void clock_picker_open(lv_event_t *e) {
    (void)e;ui_text(clock_picker_hint,"");
    for(int i=0;i<UI_CLOCK_STYLES;i++) {
        ui_border_color(clock_choices[i],lv_color_hex(i==clock_style?BLUE:TRACK),0);
        lv_obj_set_style_border_width(clock_choices[i],i==clock_style?2:0,0);
        lv_obj_invalidate(clock_choices[i]);
        if(i==1) {
            lv_obj_t *time=lv_obj_get_child(clock_thumbnails[i],0);
            if(clock_face_has_time)ui_text_fmt(time,"%02d:%02d",clock_face_time.tm_hour,clock_face_time.tm_min);else ui_text(time,"--:--");
            lv_obj_center(time);
        }
    }
    visible(clock_picker,true);lv_obj_move_foreground(clock_picker);
}
static void clock_controls_create(lv_obj_t *p) {
    clock_controls=shape(p,566,426,510,48,UI_BACKGROUND,0);
    lv_obj_t *prev=button(clock_controls,LV_SYMBOL_LEFT,0,0,48,clock_previous,0);
    ui_text_font(lv_obj_get_child(prev,0),&lv_font_montserrat_24,0);
    lv_obj_t *pick=button(clock_controls,"",60,0,272,clock_picker_open,0);
    clock_name=lv_obj_get_child(pick,0);
    lv_obj_t *arrow=label(pick,LV_SYMBOL_DOWN,238,12,24,false);ui_text_font(arrow,&lv_font_montserrat_24,0);
    lv_obj_t *next=button(clock_controls,LV_SYMBOL_RIGHT,344,0,48,clock_style_clicked,0);
    ui_text_font(lv_obj_get_child(next,0),&lv_font_montserrat_24,0);
    clock_style_index=muted(clock_controls,"",414,10,92);ui_text_font(clock_style_index,&lv_font_montserrat_24,0);
}
static void clock_picker_create(lv_obj_t *screen) {
    clock_picker=shape(screen,0,0,1280,720,UI_BACKGROUND,0);
    lv_obj_t *title=label(clock_picker,"选择表盘",64,28,700,false);ui_text_font(title,&tab5_font_30,0);
    lv_obj_t *back=button(clock_picker,"返回",1056,12,160,clock_picker_close,0);lv_obj_set_height(back,56);lv_obj_center(lv_obj_get_child(back,0));
    for(int i=0;i<UI_CLOCK_STYLES;i++) {
        lv_obj_t *b=button(clock_picker,"",64+(i%3)*388,112+(i/3)*162,376,clock_picker_choose,i);
        clock_choices[i]=b;lv_obj_set_height(b,132);lv_obj_set_style_radius(b,22,0);ui_bg_color(b,lv_color_hex(SURFACE),0);
        clock_thumbnails[i]=clock_thumbnail(b,i);lv_obj_t *name=label(b,clock_style_names[i],130,49,230,false);
        ui_text_font(name,&tab5_font_24,0);
    }
    clock_picker_hint=muted(clock_picker,"",64,632,1000);visible(clock_picker,false);
}
static void clock_time_align(void) {
    lv_obj_update_layout(clock_big);
    const lv_font_t *big=lv_obj_get_style_text_font(clock_big,0),*small=lv_obj_get_style_text_font(clock_seconds,0);
    lv_obj_set_pos(clock_seconds,lv_obj_get_x(clock_big)+lv_obj_get_width(clock_big)+16,
        lv_obj_get_y(clock_big)+big->line_height-big->base_line-small->line_height+small->base_line);
}
static void clock_layout(void) {
    bool digital=clock_style==1;visible(clock_face,!digital);analog_style();
    ui_text_font(clock_big,digital?&tab5_timer_digits_192:&tab5_digits_96,0);
    ui_text_font(date_label,&tab5_font_30,0);tint(clock_big,INK);
    lv_obj_update_layout(clock_big);
    if(digital)lv_obj_align(clock_big,LV_ALIGN_TOP_MID,-26,78);
    else {lv_obj_set_style_align(clock_big,LV_ALIGN_TOP_LEFT,0);lv_obj_set_pos(clock_big,566,114);}
    clock_time_align();
    lv_obj_set_pos(date_label,digital?140:570,digital?294:256);lv_obj_set_width(date_label,digital?940:612);
    lv_obj_set_pos(clock_lunar,digital?140:570,digital?350:315);lv_obj_set_width(clock_lunar,digital?940:612);
    lv_obj_set_pos(clock_holiday,digital?140:570,digital?390:357);lv_obj_set_width(clock_holiday,digital?940:612);
    lv_obj_set_style_text_align(date_label,digital?LV_TEXT_ALIGN_CENTER:LV_TEXT_ALIGN_LEFT,0);
    lv_obj_set_style_text_align(clock_lunar,digital?LV_TEXT_ALIGN_CENTER:LV_TEXT_ALIGN_LEFT,0);
    lv_obj_set_style_text_align(clock_holiday,digital?LV_TEXT_ALIGN_CENTER:LV_TEXT_ALIGN_LEFT,0);
    visible(clock_rule,!digital);
    if(clock_controls) {
        lv_obj_set_pos(clock_controls,digital?355:566,digital?452:426);ui_text(clock_name,clock_style_names[clock_style]);lv_obj_align(clock_name,LV_ALIGN_CENTER,-10,0);
        ui_text_fmt(clock_style_index,"%d / %d",clock_style+1,UI_CLOCK_STYLES);
    }
    if(clock_face_has_time)analog_update(&clock_face_time,0);
}
static void clock_touch_target(lv_obj_t *parent) {
    for(uint32_t i=0;i<lv_obj_get_child_count(parent);i++) {lv_obj_t *child=lv_obj_get_child(parent,i);lv_obj_remove_flag(child,LV_OBJ_FLAG_CLICKABLE);clock_touch_target(child);}
}
