// Reuses the existing offline lunar conversion and bridge calendar snapshot.
// No month controls or clickable children remain in the normal screensaver.
LV_FONT_DECLARE(tab5_calendar_text_20);
LV_FONT_DECLARE(tab5_calendar_month_48);
LV_FONT_DECLARE(tab5_calendar_verse_24);
LV_FONT_DECLARE(tab5_calendar_brush_24);
LV_FONT_DECLARE(tab5_calendar_running_28);
LV_FONT_DECLARE(tab5_calendar_meta_18);
#include "calendar_flowers.h"
#include "calendar_poems.h"
static lv_obj_t *monthly_layer,*monthly_cover,*monthly_name,*monthly_year,*monthly_title;
static lv_obj_t *monthly_cells[42],*monthly_numbers[42],*monthly_notes[42],*monthly_marks[42],*monthly_week[7];
static lv_obj_t *monthly_status,*monthly_lines[4],*monthly_poem_title,*monthly_poem_byline,*monthly_term_seal,*monthly_term_label;
static lv_obj_t *monthly_nav,*monthly_buttons[12],*monthly_flower;
static int monthly_preview_month,monthly_rendered_day,monthly_rendered_month,monthly_rendered_year;
static int monthly_rendered_term=-1;
static bool monthly_rendered_preview;
static int64_t monthly_received=-1;
typedef struct {uint32_t paper,ink;const char *name,*quote;} monthly_theme;
static const monthly_theme monthly_themes[]={
    {0xDCE9F5,0x173B68,"一月","新岁初启，万事可期。"},
    {0xF4DFE3,0x812D4C,"二月","灯火可亲，春意渐近。"},
    {0xE4E8CF,0x455629,"三月","草木萌动，好日生长。"},
    {0xD9EAE6,0x265C55,"四月","雨生百谷，春色正浓。"},
    {0xDEEBCE,0x37572B,"五月","风过新绿，万物向荣。"},
    {0xD4E9EE,0x245664,"六月","昼长夏至，日子从容。"},
    {0xF6E5B5,0x7C4C14,"七月","盛夏有声，心有清凉。"},
    {0xEBDFF2,0x68477D,"八月","晚风轻拂，暑意渐收。"},
    {0xDAE2F3,0x344E83,"九月","清风明月，秋意入怀。"},
    {0xE8DFF0,0x62427C,"十月","岁时有序，日子有光。"},
    {0xE1E6DC,0x4B5B45,"十一月","山色渐静，日子渐暖。"},
    {0xDCE7EF,0x34566E,"十二月","岁末将至，静候新章。"}
};
// The painting sits behind the right-to-left poem, with its natural pigment.
// Keep complete branches at a modest scale, with offsets for each flower shape.
static const struct {int x,y,scale;} monthly_flower_layout[]={
    {64,364,289},{72,367,281},{68,361,289},{58,361,289},
    {62,355,289},{68,363,281},{84,368,289},{76,361,281},
    {66,357,289},{72,373,289},{70,352,289},{82,367,281}
};
// Use the bridge's dated table, never fixed Gregorian approximations. Before
// this year's first term, the active term is the previous winter solstice.
static int monthly_term_at(int year,int month,int day) {
    cJSON *table=NULL;
    cJSON_ArrayForEach(table,item(calendar_detail(),"terms"))if((int)num(table,"year")==year) {
        cJSON *dates=item(table,"dates");if(cJSON_GetArraySize(dates)!=24)return -1;
        int active=23,previous=0,md=month*100+day;
        for(int i=0;i<24;i++) {
            cJSON *v=cJSON_GetArrayItem(dates,i);if(!cJSON_IsNumber(v))return -1;
            int date=v->valueint,m=date/100,d=date%100;
            if(v->valuedouble!=date||m!=i/2+1||d<1||d>cal_days(year,m)||date<=previous)return -1;
            previous=date;if(date<=md)active=i;
        }
        return active;
    }
    return -1;
}
static void monthly_vertical(lv_obj_t *label,const char *text) {
    char lines[64];size_t used=0;uint32_t at=0;
    while(text[at]&&strncmp(text+at,"，",3)&&strncmp(text+at,"。",3)) {
        uint32_t start=at;lv_text_encoded_next(text,&at);size_t bytes=at-start;
        if(used+bytes+2>=sizeof(lines))break;
        if(used)lines[used++]='\n';
        memcpy(lines+used,text+start,bytes);used+=bytes;
    }
    lines[used]=0;ui_text(label,lines);
}
static void monthly_update(const struct tm *local);
static void monthly_choose(lv_event_t *e) {
    if(!saver_preview||saver_style!=4||!saver_has_time)return;
    monthly_preview_month=(int)(intptr_t)lv_event_get_user_data(e);
    monthly_update(&saver_local_time);
    lv_event_stop_bubbling(e);
}
static void monthly_update(const struct tm *local) {
    if(!monthly_layer||local->tm_year<2||local->tm_year>199)return;
    int y=local->tm_year+1900,m=saver_preview&&monthly_preview_month?monthly_preview_month:local->tm_mon+1;
    int today=cal_serial(y,local->tm_mon+1,local->tm_mday);
    if(monthly_rendered_day==today&&monthly_rendered_month==m&&monthly_rendered_year==y&&monthly_received==last_received&&monthly_rendered_preview==saver_preview)return;
    monthly_rendered_day=today;monthly_rendered_month=m;monthly_rendered_year=y;monthly_received=last_received;monthly_rendered_preview=saver_preview;
    const monthly_theme *t=&monthly_themes[m-1];
    ui_bg_color(monthly_cover,lv_color_mix(lv_color_hex(t->paper),lv_color_hex(0xF7F2E8),LV_OPA_30),0);
    monthly_vertical(monthly_name,t->name);
    // Other-month previews use mid-month; the actual month always shows today.
    monthly_rendered_term=monthly_term_at(y,m,m==local->tm_mon+1?local->tm_mday:15);
    bool has_poem=monthly_rendered_term>=0;
    visible(monthly_term_seal,has_poem);
    if(has_poem) {
        const monthly_poem *poem=&monthly_poems[monthly_rendered_term];
        for(int i=0;i<4;i++) {
            ui_text_font(monthly_lines[i],poem->font,0);
            lv_obj_set_style_text_line_space(monthly_lines[i],33-poem->font->line_height,0);
            monthly_vertical(monthly_lines[i],poem->lines[i]);
        }
        monthly_vertical(monthly_term_label,calendar_terms[monthly_rendered_term]);
        ui_text(monthly_poem_title,poem->title);ui_text(monthly_poem_byline,poem->byline);
    } else {
        for(int i=0;i<4;i++){ui_text_font(monthly_lines[i],&tab5_calendar_verse_24,0);lv_obj_set_style_text_line_space(monthly_lines[i],9,0);}
        monthly_vertical(monthly_lines[0],t->quote);
        const char *second=strstr(t->quote,"，");monthly_vertical(monthly_lines[1],second?second+3:"");
        ui_text(monthly_poem_title,"");ui_text(monthly_poem_byline,"");
    }
    for(int i=0;i<4;i++)visible(monthly_lines[i],has_poem||i<2);
    ui_text_fmt(monthly_year,"%d 年",y);ui_text(monthly_title,t->name);
    if(lv_image_get_src(monthly_flower)!=&monthly_flower_images[m-1]) {
        lv_image_set_src(monthly_flower,&monthly_flower_images[m-1]);
        lv_obj_set_pos(monthly_flower,monthly_flower_layout[m-1].x,monthly_flower_layout[m-1].y);
        lv_image_set_scale(monthly_flower,monthly_flower_layout[m-1].scale);
    }
    visible(monthly_nav,saver_preview);
    for(int i=0;i<12;i++){ui_bg_color(monthly_buttons[i],lv_color_hex(i+1==m?t->ink:0xE8EBEF),0);tint(lv_obj_get_child(monthly_buttons[i],0),i+1==m?0xFBFCFD:0x68727B);}
    for(int i=0;i<7;i++)tint(monthly_week[i],i>=5?t->ink:0x68727B);
    int first=cal_serial(y,m,1),offset=first%7,days=cal_days(y,m),rows=(offset+days+6)/7;
    bool has_schedule=calendar_schedule(y)!=NULL,has_terms=false;
    for(int d=1;d<=days;d++)if(calendar_term(y,m*100+d)[0]){has_terms=true;break;}
    bool missing_data=!has_schedule||!has_terms;
    // Reclaim the former description area. Preview controls and exceptional
    // data warnings reserve space only when visible; center each date pair.
    int bottom=saver_preview?641:686;
    if(missing_data)bottom-=32;
    int pitch=(bottom-177)/rows;
    int number_y=(pitch-70)/2;
    lv_obj_set_y(monthly_status,bottom+5);
    for(int i=0;i<42;i++) {
        int d=i-offset+1;bool valid=d>=1&&d<=days;visible(monthly_cells[i],valid);if(!valid)continue;
        int serial=first+d-1,kind=0,ly,lm,ld;bool leap=false;calendar_holiday(y,m*100+d,&kind);
        lv_obj_t *cell=monthly_cells[i];lv_obj_set_pos(cell,444+(i%7)*112,177+(i/7)*pitch);lv_obj_set_height(cell,pitch);
        ui_bg_color(cell,lv_color_hex(kind==1?t->paper:0xFBFCFD),0);ui_bg_opa(cell,kind==1?LV_OPA_30:LV_OPA_COVER,0);
        ui_text_fmt(monthly_numbers[i],"%d",d);tint(monthly_numbers[i],serial==today?0xFBFCFD:i%7>=5&&kind!=2?t->ink:0x263037);
        ui_bg_color(monthly_numbers[i],lv_color_hex(t->ink),0);ui_bg_opa(monthly_numbers[i],serial==today?LV_OPA_COVER:LV_OPA_TRANSP,0);
        ui_text(monthly_marks[i],kind==1?"休":kind==2?"班":"");tint(monthly_marks[i],kind==1?t->ink:0x68727B);
        const char *note="--",*term=calendar_term(y,m*100+d),*festival=calendar_festival(serial);
        if(cal_lunar(serial,&ly,&lm,&ld,&leap))note=ld==1?calendar_month_names[lm-1]:calendar_day_names[ld-1];
        if(festival[0])note=festival;
        if(term[0])note=term;
        ui_text(monthly_notes[i],note);tint(monthly_notes[i],term[0]||festival[0]?t->ink:0x68727B);
        lv_obj_set_y(monthly_numbers[i],number_y);lv_obj_set_y(monthly_notes[i],number_y+46);
        lv_obj_set_y(monthly_marks[i],number_y+2);
    }
    // Dates, lunar notes, terms and holiday/workday marks live in the grid.
    // Only missing official data needs a footer; avoid repeating the month.
    ui_text(monthly_status,!has_schedule&&!has_terms?"放假、节气待更新":!has_schedule?"放假安排待更新":!has_terms?"节气日期待更新":"");
    visible(monthly_status,missing_data);
}
static lv_obj_t *monthly_label(lv_obj_t *p,const char *s,int x,int y,int w,const lv_font_t *font,uint32_t color) {
    lv_obj_t *o=label(p,s,x,y,w,false);ui_text_font(o,font,0);tint(o,color);lv_label_set_long_mode(o,LV_LABEL_LONG_CLIP);return o;
}
static UI_INIT_SMALL void monthly_create(void) {
    monthly_layer=shape(saver_layer,0,0,1280,720,0xFBFCFD,0);
    monthly_cover=shape(monthly_layer,0,0,400,720,0xE8DFF0,0);
    monthly_flower=lv_image_create(monthly_cover);lv_image_set_pivot(monthly_flower,0,0);
    lv_obj_set_style_image_opa(monthly_flower,LV_OPA_90,0);
    monthly_name=monthly_label(monthly_cover,"",310,50,56,&tab5_calendar_month_48,0x453F37);
    lv_obj_set_style_text_line_space(monthly_name,6,0);
    for(int i=0;i<4;i++) {
        monthly_lines[i]=monthly_label(monthly_cover,i?"":"等\n待\n校\n时",220-i*47,132,36,&tab5_calendar_verse_24,0x302F29);
        lv_obj_set_style_text_line_space(monthly_lines[i],9,0);
    }
    monthly_term_seal=shape(monthly_cover,315,244,42,70,0x9B5145,2);
    monthly_term_label=monthly_label(monthly_term_seal,"",7,7,28,&tab5_calendar_verse_24,0xFFF7EA);
    lv_obj_set_style_text_line_space(monthly_term_label,3,0);visible(monthly_term_seal,false);
    monthly_poem_title=monthly_label(monthly_cover,"",38,653,330,&tab5_calendar_meta_18,0x625C51);
    monthly_poem_byline=monthly_label(monthly_cover,"",38,681,330,&tab5_calendar_meta_18,0x7C7367);
    monthly_year=monthly_label(monthly_layer,"",448,39,420,&tab5_font_24,0x68727B);
    monthly_title=monthly_label(monthly_layer,"",446,80,480,&tab5_font_30,0x263037);
    for(int i=0;i<7;i++) {
        monthly_week[i]=monthly_label(monthly_layer,(const char*[]){"一","二","三","四","五","六","日"}[i],444+i*112,140,112,&tab5_font_24,0x68727B);
        lv_obj_set_style_text_align(monthly_week[i],LV_TEXT_ALIGN_CENTER,0);
    }
    for(int i=0;i<42;i++) {
        lv_obj_t *cell=monthly_cells[i]=shape(monthly_layer,0,0,112,72,0xFBFCFD,0);
        lv_obj_set_style_border_side(cell,LV_BORDER_SIDE_TOP,0);lv_obj_set_style_border_width(cell,1,0);ui_border_color(cell,lv_color_hex(0xDCE2E7),0);
        monthly_numbers[i]=monthly_label(cell,"",29,4,54,&lv_font_montserrat_40,0x263037);lv_obj_set_height(monthly_numbers[i],46);lv_obj_set_style_text_align(monthly_numbers[i],LV_TEXT_ALIGN_CENTER,0);lv_obj_set_style_radius(monthly_numbers[i],24,0);
        monthly_notes[i]=monthly_label(cell,"",0,50,110,&tab5_calendar_text_20,0x68727B);lv_obj_set_style_text_align(monthly_notes[i],LV_TEXT_ALIGN_CENTER,0);
        monthly_marks[i]=monthly_label(cell,"",90,4,22,&tab5_calendar_text_20,0x62427C);
    }
    monthly_status=monthly_label(monthly_layer,"",448,615,780,&tab5_calendar_text_20,0x68727B);
    monthly_nav=shape(monthly_layer,438,663,798,45,0xFBFCFD,0);
    for(int i=0;i<12;i++) {
        char name[16];snprintf(name,sizeof(name),"%d月",i+1);
        monthly_buttons[i]=button(monthly_nav,name,i*66,0,62,monthly_choose,i+1);lv_obj_set_height(monthly_buttons[i],42);
        ui_text_font(lv_obj_get_child(monthly_buttons[i],0),&tab5_calendar_text_20,0);lv_obj_center(lv_obj_get_child(monthly_buttons[i],0));
        lv_obj_remove_flag(monthly_buttons[i],LV_OBJ_FLAG_EVENT_BUBBLE);
    }
    clock_touch_target(monthly_layer); // Ordinary taps pass through to saver_layer.
    for(int i=0;i<12;i++)lv_obj_add_flag(monthly_buttons[i],LV_OBJ_FLAG_CLICKABLE);
    visible(monthly_nav,false);visible(monthly_layer,false);
}
