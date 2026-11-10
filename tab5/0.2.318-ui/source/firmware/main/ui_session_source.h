// Passive source metadata; never grants control or selects an input transport.
LV_FONT_DECLARE(tab5_source_20);
static lv_obj_t *session_source_rows[8],*session_source_compose,*session_source_reply;
// LV_LABEL_LONG_DOT edits the visible label buffer. Compare the full input,
// otherwise a long unchanged title/badge invalidates swipe caches every frame.
static struct {lv_obj_t *object;char *full;} session_source_texts[20];
static void session_source_text(lv_obj_t *object,const char *value) {
    for(unsigned i=0;i<20;i++)if(!session_source_texts[i].object||session_source_texts[i].object==object){
        if(session_source_texts[i].full&&!strcmp(session_source_texts[i].full,value))return;
        char *copy=strdup(value);
        if(copy){free(session_source_texts[i].full);session_source_texts[i].object=object;session_source_texts[i].full=copy;}
        ui_text(object,value);return;
    }
    ui_text(object,value);
}
static void session_source_width(lv_obj_t *o,int w){if(lv_obj_get_style_width(o,0)!=w)lv_obj_set_width(o,w);}
static void session_source_height(lv_obj_t *o,int h){if(lv_obj_get_style_height(o,0)!=h)lv_obj_set_height(o,h);}
static void session_source_pos(lv_obj_t *o,int x,int y){
    if(lv_obj_get_style_x(o,0)!=x)lv_obj_set_x(o,x);
    if(lv_obj_get_style_y(o,0)!=y)lv_obj_set_y(o,y);
}
static const char *session_source_label(cJSON *task) {
    const char *s=text(item(task,"source"),"label");
    if(!strcmp(s,"--")||!s[0]||strlen(s)>80)return "来源未知";
    for(const unsigned char *p=(const unsigned char *)s;*p;p++)if(*p<32||*p==127)return "来源未知";
    return s;
}
static const lv_font_t *session_source_font(const char *s) {
    if(!strcmp(s,"来源未知")||!strcmp(s,"原生终端")||!strcmp(s,"多端打开")||!strcmp(s,"后台会话"))return &tab5_source_20;
    for(const unsigned char *p=(const unsigned char *)s;*p;p++)if(*p>=128)return &tab5_font_24;
    return &tab5_source_20;
}
static lv_obj_t *session_source_create(lv_obj_t *parent) {
    lv_obj_t *box=lv_obj_create(parent);lv_obj_remove_style_all(box);
    lv_obj_set_style_bg_opa(box,LV_OPA_COVER,0);lv_obj_set_style_radius(box,5,0);
    lv_obj_remove_flag(box,LV_OBJ_FLAG_CLICKABLE|LV_OBJ_FLAG_SCROLLABLE|LV_OBJ_FLAG_CLICK_FOCUSABLE);
    lv_obj_t *caption=lv_label_create(box);lv_label_set_long_mode(caption,LV_LABEL_LONG_DOT);
    lv_obj_align(caption,LV_ALIGN_LEFT_MID,8,0);
    lv_obj_remove_flag(caption,LV_OBJ_FLAG_CLICKABLE|LV_OBJ_FLAG_SCROLLABLE|LV_OBJ_FLAG_CLICK_FOCUSABLE);
    visible(box,false);return box;
}
static int session_source_update(lv_obj_t *box,cJSON *task) {
    const char *caption=session_source_label(task),*kind=text(item(task,"source"),"kind");
    const lv_font_t *font=session_source_font(caption);
    int width=LV_MIN(190,lv_text_get_width(caption,(uint32_t)strlen(caption),font,0)+16);
    uint32_t ink=0xBCC7CB,bg=0x3A464A;
    if(!strcmp(kind,"ide")){ink=0xB8DAEF;bg=0x294653;}
    else if(!strcmp(kind,"terminal")){ink=0xD6C8EB;bg=0x493F55;}
    else if(!strcmp(kind,"harness")){ink=0xE6D5AB;bg=0x51482F;}
    session_source_width(box,width);session_source_height(box,font==&tab5_source_20?28:32);
    ui_bg_color(box,lv_color_hex(bg),0);
    lv_obj_t *label=lv_obj_get_child(box,0);ui_text_font(label,font,0);tint(label,ink);
    session_source_width(label,width-16);session_source_text(label,caption);
    visible(box,true);return width;
}
static void session_source_header(lv_obj_t *title,lv_obj_t *badge,cJSON *task,const char *full,int available,int y) {
    if(!workspace_client||!task){visible(badge,false);session_source_width(title,available);session_source_text(title,full);return;}
    int badge_width=session_source_update(badge,task);
    int title_width=LV_MIN(available-badge_width-12,lv_text_get_width(full,(uint32_t)strlen(full),&tab5_font_24,0)+2);
    session_source_width(title,title_width);session_source_text(title,full);
    session_source_pos(badge,title_width+12,y+(32-lv_obj_get_style_height(badge,0))/2);
}
static void session_source_row(int i,cJSON *task) {
    lv_obj_t *row=codex_task_rows[i],*title=codex_task_titles[i],*badge=session_source_rows[i];
    bool small=session_source_font(session_source_label(task))==&tab5_source_20;
    int padding=workspace_client?(small?7:6):10;
    if(lv_obj_get_style_pad_top(row,0)!=padding)lv_obj_set_style_pad_top(row,padding,0);
    if(lv_obj_get_style_pad_bottom(row,0)!=padding)lv_obj_set_style_pad_bottom(row,padding,0);
    session_source_pos(title,0,workspace_client?0:8);session_source_height(title,workspace_client?32:60);
    if(workspace_client){session_source_update(badge,task);session_source_pos(badge,0,small?34:32);}
    else visible(badge,false);
}
