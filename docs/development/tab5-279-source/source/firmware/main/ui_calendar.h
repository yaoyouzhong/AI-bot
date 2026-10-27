// Calendar detail is a local, offline-browsable view. Only official holiday
// arrangements, solar-term tables and personal birthdays come from the bridge.
#include "calendar_math.h"
static lv_obj_t *calendar_title,*calendar_cells[42],*calendar_numbers[42],*calendar_notes[42],*calendar_marks[42];
static lv_obj_t *calendar_day_number,*calendar_selected_title,*calendar_lunar_label,*calendar_day_note,*calendar_upcoming;
static lv_obj_t *calendar_divider,*calendar_birthday_caption,*calendar_upcoming_box;
static lv_obj_t *calendar_previous,*calendar_next;
static int calendar_year,calendar_month,calendar_selected,calendar_today_serial,calendar_cell_dates[42];
static bool calendar_has_birthday;
static const char *calendar_terms[]={"小寒","大寒","立春","雨水","惊蛰","春分","清明","谷雨","立夏","小满","芒种","夏至","小暑","大暑","立秋","处暑","白露","秋分","寒露","霜降","立冬","小雪","大雪","冬至"};
static const char *calendar_month_names[]={"正月","二月","三月","四月","五月","六月","七月","八月","九月","十月","冬月","腊月"};
static const char *calendar_day_names[]={"初一","初二","初三","初四","初五","初六","初七","初八","初九","初十","十一","十二","十三","十四","十五","十六","十七","十八","十九","二十","廿一","廿二","廿三","廿四","廿五","廿六","廿七","廿八","廿九","三十"};
static cJSON *calendar_detail(void){return item(item(shown,"calendar"),"detail");}
static int calendar_occurrence(cJSON *birthday,int year) {
    int month=(int)num(birthday,"month"),day=(int)num(birthday,"day");
    if(month<1||month>12||day<1||day>31)return -1;
    if(yes(birthday,"lunar"))return cal_lunar_birthday(year,month,day,yes(birthday,"leap"));
    return cal_serial(year,month,LV_MIN(day,cal_days(year,month)));
}
static bool calendar_birthday_on(cJSON *b,int serial) {
    int y,m,d;cal_gregorian(serial,&y,&m,&d);
    return calendar_occurrence(b,y)==serial||(yes(b,"lunar")&&calendar_occurrence(b,y-1)==serial);
}
static int calendar_birthday_next(cJSON *b,int serial) {
    int y,m,d;cal_gregorian(serial,&y,&m,&d);int nearest=-1;
    for(int year=y-1;year<=y+1;year++) {
        int candidate=calendar_occurrence(b,year);
        if(candidate>=serial&&(nearest<0||candidate<nearest))nearest=candidate;
    }
    return nearest;
}
static const char *calendar_term(int year,int md) {
    cJSON *table=NULL;cJSON_ArrayForEach(table,item(calendar_detail(),"terms"))if((int)num(table,"year")==year) {
        for(int i=0;i<24;i++) {cJSON *value=cJSON_GetArrayItem(item(table,"dates"),i);if(cJSON_IsNumber(value)&&value->valueint==md)return calendar_terms[i];}
    }
    return "";
}
static cJSON *calendar_schedule(int year) {
    cJSON *entry=NULL;cJSON_ArrayForEach(entry,item(calendar_detail(),"holidayYears"))if((int)num(entry,"year")==year)return entry;
    return (int)num(calendar_detail(),"holidayYear")==year?calendar_detail():NULL;
}
static const char *calendar_holiday(int year,int md,int *kind) {
    *kind=0;
    cJSON *schedule=calendar_schedule(year);if(!schedule)return "";
    cJSON *entry=NULL;cJSON_ArrayForEach(entry,item(schedule,"workdays"))if(cJSON_IsNumber(entry)&&entry->valueint==md){*kind=2;return "调休补班";}
    cJSON_ArrayForEach(entry,item(schedule,"holidays"))if(md>=(int)num(entry,"start")&&md<=(int)num(entry,"end")){*kind=1;return text(entry,"name");}
    return "";
}
static const char *calendar_festival(int serial) {
    int y,m,d;bool leap;
    if(cal_lunar(serial,&y,&m,&d,&leap)&&!leap) {
        if(m==1&&d==1)return "春节";
        if(m==1&&d==15)return "元宵";
        if(m==5&&d==5)return "端午";
        if(m==7&&d==7)return "七夕";
        if(m==8&&d==15)return "中秋";
        if(m==9&&d==9)return "重阳";
        if(cal_lunar(serial+1,&y,&m,&d,&leap)&&m==1&&d==1)return "除夕";
    }
    cal_gregorian(serial,&y,&m,&d);
    if(m==1&&d==1)return "元旦";
    if(m==5&&d==1)return "劳动节";
    if(m==10&&d==1)return "国庆节";
    return "";
}
static void calendar_render(void) {
    if(!calendar_title||!calendar_year)return;
    ui_text_fmt(calendar_title,"%d 年 %d 月",calendar_year,calendar_month);
    if(calendar_year<=1902&&calendar_month==1)lv_obj_add_state(calendar_previous,LV_STATE_DISABLED);else lv_obj_remove_state(calendar_previous,LV_STATE_DISABLED);
    if(calendar_year>=2099&&calendar_month==12)lv_obj_add_state(calendar_next,LV_STATE_DISABLED);else lv_obj_remove_state(calendar_next,LV_STATE_DISABLED);
    int first=cal_serial(calendar_year,calendar_month,1),start=first-first%7;
    int weeks=(first%7+cal_days(calendar_year,calendar_month)+6)/7;
    int pitch=384/weeks;
    for(int i=0;i<42;i++) {
        visible(calendar_cells[i],i<weeks*7);
        if(i>=weeks*7)continue;
        lv_obj_set_y(calendar_cells[i],92+(i/7)*pitch);lv_obj_set_height(calendar_cells[i],pitch-4);
        lv_obj_set_y(calendar_numbers[i],weeks<6?4:0);lv_obj_set_y(calendar_notes[i],pitch-36);
        int serial=start+i,y,m,d,ly,lm,ld;bool leap=false;calendar_cell_dates[i]=serial;
        cal_gregorian(serial,&y,&m,&d);ui_text_fmt(calendar_numbers[i],"%d",d);
        const char *note="";if(cal_lunar(serial,&ly,&lm,&ld,&leap))note=ld==1?calendar_month_names[lm-1]:calendar_day_names[ld-1];
        const char *term=calendar_term(y,m*100+d),*festival=calendar_festival(serial);
        if(festival[0])note=festival;
        if(term[0])note=term;
        ui_text(calendar_notes[i],note);int kind;calendar_holiday(y,m*100+d,&kind);
        bool birthday=false;cJSON *b=NULL;cJSON_ArrayForEach(b,item(calendar_detail(),"birthdays"))if(calendar_birthday_on(b,serial)){birthday=true;break;}
        ui_text(calendar_marks[i],birthday?"生":kind==1?"休":kind==2?"班":"");
        tint(calendar_marks[i],birthday?PLAN_GOLD:kind==1?GREEN:CORAL);
        bool selected=serial==calendar_selected,today=serial==calendar_today_serial;
        ui_bg_color(calendar_cells[i],lv_color_hex(selected?0x355660:0x293236),0);
        ui_border_color(calendar_cells[i],lv_color_hex(today?PLAN_GOLD:0x293236),0);
        tint(calendar_numbers[i],m!=calendar_month?0x657275:selected?0xE2F5F6:INK);
        tint(calendar_notes[i],m!=calendar_month?0x657275:term[0]?BLUE:festival[0]?PLAN_GOLD:MUTED);
    }
    int y,m,d,ly,lm,ld;bool leap;cal_gregorian(calendar_selected,&y,&m,&d);
    const char *week[]={"星期一","星期二","星期三","星期四","星期五","星期六","星期日"};
    ui_text_fmt(calendar_selected_title,"%d 月 · %s",m,week[calendar_selected%7]);ui_text_fmt(calendar_day_number,"%02d",d);
    if(cal_lunar(calendar_selected,&ly,&lm,&ld,&leap))ui_text_fmt(calendar_lunar_label,"农历%s%s%s",leap?"闰":"",calendar_month_names[lm-1],calendar_day_names[ld-1]);
    int kind;const char *holiday=calendar_holiday(y,m*100+d,&kind),*term=calendar_term(y,m*100+d),*festival=calendar_festival(calendar_selected);
    const char *schedule=kind==1?"假期休息":kind==2?"调休补班":calendar_selected%7>=5?"周末":"工作日";
    if(!calendar_schedule(y))schedule="放假安排待更新";
    // Makeup status is already in schedule; retain any distinct festival.
    if(kind==2)holiday="";
    char day_note[256];snprintf(day_note,sizeof(day_note),"%s%s%s%s%s",holiday[0]?holiday:festival,(holiday[0]||festival[0])?" · ":"",schedule,term[0]?"\n节气 · ":"",term);
    cJSON *b=NULL;unsigned n=0;cJSON_ArrayForEach(b,item(calendar_detail(),"birthdays"))if(calendar_birthday_on(b,calendar_selected)&&n++<2) {
        size_t at=strlen(day_note);snprintf(day_note+at,sizeof(day_note)-at,"\n%s的生日",text(b,"name"));
    }
    ui_text(calendar_day_note,day_note);
    lv_obj_update_layout(calendar_day_note);
    int birthday_y=LV_CLAMP(176,126+lv_obj_get_height(calendar_day_note)+20,280);
    lv_obj_set_y(calendar_divider,birthday_y);
    lv_obj_set_y(calendar_birthday_caption,birthday_y+16);
    lv_obj_set_y(calendar_upcoming_box,birthday_y+58);
    lv_obj_set_height(calendar_upcoming_box,476-birthday_y-58);
    char upcoming[512]="";int cursor=calendar_today_serial-1;unsigned shown_count=0;
    for(int pass=0;pass<3;pass++) {
        int nearest=-1;cJSON_ArrayForEach(b,item(calendar_detail(),"birthdays")) {int next=calendar_birthday_next(b,calendar_today_serial);if(next>cursor&&(nearest<0||next<nearest))nearest=next;}
        if(nearest<0)break;
        cJSON_ArrayForEach(b,item(calendar_detail(),"birthdays"))if(calendar_birthday_next(b,calendar_today_serial)==nearest&&shown_count<3) {
            int by,bm,bd;cal_gregorian(nearest,&by,&bm,&bd);int delta=nearest-calendar_today_serial;
            size_t at=strlen(upcoming);snprintf(upcoming+at,sizeof(upcoming)-at,"%s%s · %s\n%02d-%02d  %s",shown_count?"\n":"",text(b,"name"),yes(b,"lunar")?"农历":"公历",bm,bd,delta?"":"今天生日");
            if(delta){at=strlen(upcoming);snprintf(upcoming+at,sizeof(upcoming)-at,"还有 %d 天",delta);}shown_count++;
        }
        cursor=nearest;
    }
    ui_text(calendar_upcoming,shown_count?upcoming:"无");
}
static void calendar_choose(lv_event_t *event) {
    int slot=(int)(intptr_t)lv_event_get_user_data(event);calendar_selected=calendar_cell_dates[slot];
    int d;cal_gregorian(calendar_selected,&calendar_year,&calendar_month,&d);calendar_render();
}
static void calendar_move(lv_event_t *event) {
    int delta=(int)(intptr_t)lv_event_get_user_data(event),day;
    if(!delta){calendar_selected=calendar_today_serial;cal_gregorian(calendar_selected,&calendar_year,&calendar_month,&day);}
    else {int month=calendar_year*12+calendar_month-1+delta;calendar_year=month/12;calendar_month=month%12+1;calendar_selected=cal_serial(calendar_year,calendar_month,1);}
    calendar_render();
}
static void calendar_set_today(int year,int month,int day) {
    if(year<1902||year>2099)return;
    int serial=cal_serial(year,month,day);bool changed=serial!=calendar_today_serial;
    bool following=!calendar_year||calendar_selected==calendar_today_serial;
    calendar_today_serial=serial;
    if(following&&changed){calendar_year=year;calendar_month=month;calendar_selected=serial;}
    if(current_page==12&&changed)calendar_render();
    cJSON *b=NULL,*nearest=NULL;int delta=31;
    cJSON_ArrayForEach(b,item(calendar_detail(),"birthdays")) {int next=calendar_birthday_next(b,serial),days=next-serial;if(next>=serial&&days<delta&&days<=num(b,"remind")){nearest=b;delta=days;}}
    calendar_has_birthday=nearest!=NULL;
    if(nearest) {if(!delta)ui_text_fmt(home_holiday,"今天 · %s的生日",text(nearest,"name"));else ui_text_fmt(home_holiday,"%s的生日 · 还有 %d 天",text(nearest,"name"),delta);tint(home_holiday,PLAN_GOLD);}
    else tint(home_holiday,MUTED);
    static int notified_day;
    int64_t now=esp_timer_get_time()/1000;
    if(nearest&&prefs.alerts&&notified_day!=serial&&now>=alert_until&&!home_task_unread&&!saver) {
        notified_day=serial;alert_until=now+10000;alert_started=now;alert_attention=false;alert_phase=-1;codex_alert_task[0]=0;
        ui_text(banner,"生日提醒 · 点击查看日历");
    }
}
static UI_INIT_SMALL void calendar_create(lv_obj_t *parent) {
    lv_obj_t *grid=card(parent,0,0,836,516),*detail=card(parent,854,0,366,516);
    calendar_title=label(grid,"日历",8,0,400,false);ui_text_font(calendar_title,&tab5_font_30,0);
    calendar_previous=button(grid,"<",506,0,70,calendar_move,-1);
    lv_obj_t *today=button(grid,"今天",588,0,112,calendar_move,0);
    calendar_next=button(grid,">",712,0,70,calendar_move,1);
    lv_obj_t *actions[]={calendar_previous,today,calendar_next};for(int i=0;i<3;i++){lv_obj_set_height(actions[i],42);lv_obj_center(lv_obj_get_child(actions[i],0));}
    const char *week[]={"一","二","三","四","五","六","日"};
    for(int i=0;i<7;i++){lv_obj_t *w=muted(grid,week[i],i*114+6,54,108);lv_obj_set_style_text_align(w,LV_TEXT_ALIGN_CENTER,0);if(i>=5)tint(w,CORAL);}
    for(int i=0;i<42;i++) {
        lv_obj_t *cell=calendar_cells[i]=button(grid,"",6+(i%7)*114,92+(i/7)*64,108,calendar_choose,i);
        lv_obj_set_height(cell,60);lv_obj_set_style_radius(cell,10,0);lv_obj_set_style_pad_all(cell,0,0);lv_obj_set_style_border_width(cell,1,0);
        calendar_numbers[i]=lv_obj_get_child(cell,0);lv_obj_set_align(calendar_numbers[i],LV_ALIGN_TOP_LEFT);lv_obj_set_pos(calendar_numbers[i],12,0);lv_obj_set_width(calendar_numbers[i],56);ui_text_font(calendar_numbers[i],&lv_font_montserrat_24,0);
        calendar_notes[i]=label(cell,"",4,28,100,false);lv_obj_set_height(calendar_notes[i],32);lv_obj_set_style_text_align(calendar_notes[i],LV_TEXT_ALIGN_CENTER,0);lv_label_set_long_mode(calendar_notes[i],LV_LABEL_LONG_CLIP);
        calendar_marks[i]=label(cell,"",77,2,28,false);
    }
    calendar_selected_title=muted(detail,"今天",6,0,312);
    calendar_day_number=label(detail,"--",4,40,106,true);ui_text_font(calendar_day_number,&tab5_digits_72,0);tint(calendar_day_number,PLAN_GOLD);
    calendar_lunar_label=label(detail,"农历",126,62,190,false);
    calendar_day_note=muted(detail,"",6,126,314);lv_obj_set_style_max_height(calendar_day_note,128,0);lv_label_set_long_mode(calendar_day_note,LV_LABEL_LONG_WRAP);
    calendar_divider=shape(detail,6,176,312,1,TRACK,0);calendar_birthday_caption=label(detail,"近期生日",6,192,310,false);tint(calendar_birthday_caption,PLAN_GOLD);
    lv_obj_t *upcoming=calendar_upcoming_box=lv_obj_create(detail);lv_obj_remove_style_all(upcoming);lv_obj_set_pos(upcoming,6,234);lv_obj_set_size(upcoming,318,242);lv_obj_set_scroll_dir(upcoming,LV_DIR_VER);
    calendar_upcoming=muted(upcoming,"",0,0,298);lv_obj_set_style_text_line_space(calendar_upcoming,4,0);
}
