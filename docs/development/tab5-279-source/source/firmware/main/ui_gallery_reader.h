// Included in the LVGL task. Text is already bound to the displayed work/date.
static lv_obj_t *gallery_controls,*gallery_details_button,*gallery_previous,*gallery_next,*gallery_first,*gallery_page_count;
static lv_obj_t *gallery_footer_title,*gallery_footer_museum,*gallery_footer_date;
// LONG_DOT temporarily edits the label's buffer. Compare complete source text,
// rather than that ellipsized display buffer, at repeated gallery ticks.
static struct {char title[512],museum[80];} gallery_caption_text;
static void gallery_caption_set(lv_obj_t *label,char *saved,size_t size,const char *value) {
    if(!strcmp(saved,value))return;
    snprintf(saved,size,"%s",value);lv_label_set_text(label,value);
}
static lv_obj_t *gallery_reader,*gallery_reader_title,*gallery_reader_meta,*gallery_reader_copy,*gallery_reader_body,*gallery_reader_back,*gallery_chapters[3];
static lv_obj_t *gallery_text_previous,*gallery_text_next,*gallery_text_count;
static gallery_guide *gallery_reading;
static bool gallery_details_open;
static int64_t gallery_reader_click_at;
static bool gallery_reader_paint_pending;
static int gallery_reading_chapter;
static int gallery_reading_page;
static int64_t gallery_manual_until;
static bool gallery_return_hold;
static int gallery_reading_category,gallery_reading_frame;
static void gallery_show(void);
static bool gallery_schedule(int category,const char *date,int frame,bool portrait);
static int gallery_shown_slot(int category) {
    const void *shown=lv_image_get_src(gallery_view);
    for(int i=category*2;i<category*2+2;i++)if(shown==&gallery_descriptor[i]&&gallery_cache[i].pixels)return i;
    return -1;
}
static void gallery_reader_layout(void) {
    if(!gallery_reader)return;
    bool portrait=gallery_portrait();int width=portrait?720:1280,height=portrait?1280:720;
    // Portrait JPEGs are pre-rotated 90 degrees counter-clockwise. Match that
    // bitmap and its mounted-device 180-degree counterpart, including text.
    int angle=portrait?(gallery_orientation>0?900:2700):(gallery_orientation==2?1800:0);
    lv_obj_set_size(gallery_reader,width,height);
    lv_obj_set_style_transform_pivot_x(gallery_reader,0,0);lv_obj_set_style_transform_pivot_y(gallery_reader,0,0);
    lv_obj_set_style_transform_rotation(gallery_reader,angle,0);
    lv_obj_set_pos(gallery_reader,angle==900||angle==1800?1280:0,angle==2700||angle==1800?720:0);
    lv_obj_set_width(gallery_reader_title,width-100);lv_obj_update_layout(gallery_reader_title);
    int header=lv_obj_get_height(gallery_reader_title)+72;
    lv_obj_set_pos(gallery_reader_meta,50,header);lv_obj_set_width(gallery_reader_meta,width-100);
    lv_obj_update_layout(gallery_reader_meta);header+=lv_obj_get_height(gallery_reader_meta)+28;
    for(int i=0;i<3;i++)lv_obj_set_pos(gallery_chapters[i],50+i*160,header);
    lv_obj_set_pos(gallery_reader_copy,50,header+76);lv_obj_set_size(gallery_reader_copy,width-100,height-header-174);
    lv_obj_set_width(gallery_reader_body,width-116);
    lv_obj_set_pos(gallery_reader_back,width-226,height-78);
    lv_obj_set_pos(gallery_text_previous,50,height-78);lv_obj_set_pos(gallery_text_count,194,height-60);lv_obj_set_pos(gallery_text_next,310,height-78);
}
static void gallery_controls_layout(const gallery_image *image,bool guide) {
    bool portrait=image->portrait;int width=portrait?720:1280,height=portrait?1280:720;
    bool footer_navigation=guide;
    static bool previous_footer_navigation;
    int rail=portrait?138:98;
    int direction=portrait?(gallery_portrait()?gallery_orientation:gallery_last_portrait):gallery_orientation;
    int angle=portrait?(direction>0?900:2700):(direction==2?1800:0);
    if(lv_obj_get_width(gallery_controls)==width&&lv_obj_get_height(gallery_controls)==rail&&
       lv_obj_get_style_transform_rotation(gallery_controls,0)==angle&&previous_footer_navigation==footer_navigation)return;
    previous_footer_navigation=footer_navigation;
    // Keep legacy title, author and licence pixels untouched. Rotate the
    // bounded caption strip and each small arrow independently; a transparent
    // full-width navigation strip would allocate a needless rotated layer.
    lv_obj_set_size(gallery_controls,width,rail);
    lv_obj_set_style_transform_pivot_x(gallery_controls,0,0);lv_obj_set_style_transform_pivot_y(gallery_controls,0,0);
    lv_obj_set_style_transform_rotation(gallery_controls,angle,0);
    int top=height-rail;
    lv_obj_set_pos(gallery_controls,angle==900?1280-top:angle==1800?1280:angle==2700?top:0,
        angle==2700?720:angle==1800?720-top:angle==0?top:0);
    lv_obj_t *arrows[]={gallery_previous,gallery_next};
    for(int i=0;i<2;i++) {
        lv_obj_t *arrow=arrows[i];
        int x=footer_navigation?width/2+(i?48:-128):(i?width-72:16);
        int y=footer_navigation?height-81:(height-rail-56)/2;
        lv_obj_set_size(arrow,footer_navigation?80:56,footer_navigation?72:56);
        lv_obj_set_style_bg_opa(arrow,footer_navigation?LV_OPA_TRANSP:LV_OPA_80,0);
        lv_obj_set_style_transform_pivot_x(arrow,0,0);lv_obj_set_style_transform_pivot_y(arrow,0,0);
        lv_obj_set_style_transform_rotation(arrow,angle,0);
        lv_obj_set_pos(arrow,angle==900?1280-y:angle==1800?1280-x:angle==2700?y:x,
            angle==900?x:angle==2700?720-x:angle==1800?720-y:y);
    }
    // This font's digit/slash ink sits four pixels below the arrow glyphs
    // when their old label boxes are aligned. Align the visible ink instead.
    lv_obj_set_pos(gallery_page_count,width/2-44,rail-61);lv_obj_set_size(gallery_page_count,88,30);
    lv_obj_set_style_text_align(gallery_page_count,LV_TEXT_ALIGN_CENTER,0);
    lv_obj_set_pos(gallery_details_button,width-(portrait?184:216),rail-73);
    lv_obj_set_width(gallery_details_button,portrait?144:176);
    // Landscape names and period belong to the gallery image's left panel.
    // Preserve the accepted calligraphy layout; portrait keeps its compact
    // title/author row above the existing navigation controls.
    lv_obj_set_pos(gallery_footer_title,40,12);lv_obj_set_size(gallery_footer_title,width-80,30);visible(gallery_footer_title,portrait);
    lv_obj_set_pos(gallery_footer_museum,40,rail-57);lv_obj_set_size(gallery_footer_museum,portrait?184:464,30);
    visible(gallery_footer_date,false);visible(gallery_first,false);
}
static void gallery_reader_text(void) {
    if(!gallery_reading)return;
    const char *copy=gallery_reading_chapter==1?gallery_reading->looking:gallery_reading_chapter==2?gallery_reading->transcription:gallery_reading->overview;
    if(gallery_reading_chapter==2&&!copy[0])ui_text(gallery_reader_body,"这幅作品的原文尚未核实。");
    else if(gallery_reading_chapter==2&&gallery_reading->explanation[0])ui_text_fmt(gallery_reader_body,"%s\n\n说明\n%s",copy,gallery_reading->explanation);
    else ui_text(gallery_reader_body,copy);
    lv_obj_scroll_to_y(gallery_reader_copy,0,LV_ANIM_OFF);
    int pages=gallery_reading->transcription_page_count;
    bool paged=gallery_reading_chapter==2&&pages>1;
    visible(gallery_text_previous,paged);visible(gallery_text_next,paged);visible(gallery_text_count,paged);
    lv_obj_set_state(gallery_text_previous,LV_STATE_DISABLED,gallery_reading_page==0);
    lv_obj_set_state(gallery_text_next,LV_STATE_DISABLED,gallery_reading_page+1>=pages);
    ui_text_fmt(gallery_text_count,"%d / %d",gallery_reading_page+1,pages);
    int category=saver_style-6;
    for(int i=0;i<3;i++) {
        bool selected=i==gallery_reading_chapter;lv_obj_t *tab=gallery_chapters[i];
        uint32_t fill=category?(selected?0x6D5847:0xD9D0BE):(selected?0x52695C:0x222D27);
        uint32_t pressed=category?(selected?0x594838:0xC8BBA4):(selected?0x405548:0x33463B);
        ui_bg_color(tab,lv_color_hex(fill),0);ui_bg_color(tab,lv_color_hex(pressed),LV_STATE_PRESSED);
        ui_border_color(tab,lv_color_hex(category?(selected?0x6D5847:0xC5B89F):(selected?0x647E6D:0x3A4B40)),0);
        tint(lv_obj_get_child(tab,0),selected?0xFFF8EA:category?0x625645:0xB9C6BB);
    }
}
static void gallery_reader_close(void) {
    bool was_open=gallery_details_open;
    gallery_details_open=false;gallery_reader_paint_pending=false;visible(gallery_reader,false);
    gallery_guide_reader_release(gallery_reading);
    // Return retains the exact frame; give the reader a fresh full page interval.
    if(was_open){gallery_manual_until=esp_timer_get_time()/1000+30000;gallery_return_hold=true;gallery_frame[gallery_reading_category]=gallery_reading_frame;gallery_show();}
}
static void gallery_reader_action(lv_event_t *e) {
    lv_event_stop_bubbling(e);int action=(int)(intptr_t)lv_event_get_user_data(e);
    if(action==3){gallery_reader_close();return;}
    if(action==4||action==5) {
        int page=gallery_reading_page+(action==4?-1:1);
        if(page>=0&&page<gallery_reading->transcription_page_count&&gallery_reading->transcription_pages[page]) {
            snprintf(gallery_reading->transcription,sizeof(gallery_reading->transcription),"%s",gallery_reading->transcription_pages[page]);
            gallery_reading_page=page;gallery_reader_text();
        }
        return;
    }
    if(action==2){gallery_reading_page=0;if(gallery_reading->transcription_pages[0])snprintf(gallery_reading->transcription,sizeof(gallery_reading->transcription),"%s",gallery_reading->transcription_pages[0]);}
    gallery_reading_chapter=action;gallery_reader_text();
}
static void gallery_details_clicked(lv_event_t *e) {
    lv_event_stop_bubbling(e);if(!gallery_reading)return;
    int64_t started=esp_timer_get_time();
    gallery_motion_finish();
    int category=saver_style-6,slot=gallery_shown_slot(category);if(slot<0)return;
    gallery_image *image=&gallery_cache[slot];
    if(!gallery_guide_reader_copy(category,image->date,image->work_id,gallery_reading))return;
    int64_t copied=esp_timer_get_time();
    tab5_gallery_reader_guide_us=(unsigned)(copied-started);
    gallery_pending_page=(gallery_image){0};
    gallery_reading_category=category;gallery_reading_frame=image->frame;
    ui_text(gallery_reader_title,gallery_reading->title);
    ui_text_fmt(gallery_reader_meta,"%s  ·  %s\n%s",gallery_reading->author,gallery_reading->period,gallery_reading->museum);
    uint32_t ink=category?0x332E27:0xEDF0E9;
    ui_bg_color(gallery_reader,lv_color_hex(category?0xE7E0D1:0x111516),0);
    tint(gallery_reader_title,ink);tint(gallery_reader_meta,category?0x776E60:0x929A96);tint(gallery_reader_body,ink);
    tint(lv_obj_get_child(gallery_reader_back,0),ink);
    tint(lv_obj_get_child(gallery_text_previous,0),ink);tint(lv_obj_get_child(gallery_text_next,0),ink);tint(gallery_text_count,ink);
    visible(gallery_chapters[2],category==1||gallery_reading->transcription[0]!=0);
    gallery_reading_chapter=0;gallery_reading_page=0;gallery_reader_text();gallery_details_open=true;
    gallery_reader_layout();visible(gallery_reader,true);lv_obj_move_foreground(gallery_reader);
    tab5_gallery_reader_layout_us=(unsigned)(esp_timer_get_time()-copied);
    tab5_gallery_reader_clicks++;tab5_gallery_reader_paint_us=0;
    gallery_reader_click_at=started;gallery_reader_paint_pending=true;
}
static void gallery_reader_painted(lv_event_t *e) {
    (void)e;
    if(gallery_reader_paint_pending&&gallery_details_open) {
        tab5_gallery_reader_paint_us=(unsigned)(esp_timer_get_time()-gallery_reader_click_at);
        gallery_reader_paint_pending=false;
    }
}
static void gallery_frame_clicked(lv_event_t *e) {
    lv_event_stop_bubbling(e);int action=(int)(intptr_t)lv_event_get_user_data(e),category=saver_style-6;
    gallery_motion_finish();gallery_motion_step=action<0?-1:1;
    int slot=gallery_shown_slot(category);if(slot<0)return;
    gallery_image *image=&gallery_cache[slot];if(!image->pixels||image->frames<2)return;
    int64_t now=esp_timer_get_time()/1000;
    bool pending=gallery_pending_page.date[0]&&now<gallery_pending_until&&
        gallery_pending_page.category==category&&!strcmp(gallery_pending_page.date,image->date);
    int base=pending?gallery_pending_page.frame:image->frame;
    int frame=action==0?0:(base+image->frames+action)%image->frames;
    if(gallery_schedule(category,image->date,frame,gallery_portrait())||!gallery_request_ready()){
        // Retain the target across background waits and an in-flight decode.
        // Each tap advances the latest intent, rather than the stale screen.
        gallery_pending_page=(gallery_image){.category=category,.frame=frame};
        snprintf(gallery_pending_page.date,sizeof(gallery_pending_page.date),"%s",image->date);
        gallery_pending_until=now+30000;gallery_manual_until=now+30000;gallery_return_hold=false;
        ui_text(gallery_page_count,"…");
    }
}
static lv_obj_t *gallery_quiet_button(lv_obj_t *parent,const char *caption,int x,int y,int width,lv_event_cb_t cb,int action) {
    lv_obj_t *b=button(parent,caption,x,y,width,cb,action);lv_obj_set_height(b,56);
    lv_obj_set_style_bg_opa(b,LV_OPA_TRANSP,LV_PART_MAIN|LV_STATE_DEFAULT);lv_obj_set_style_bg_opa(b,LV_OPA_TRANSP,LV_PART_MAIN|LV_STATE_PRESSED);
    lv_obj_set_style_border_width(b,0,0);lv_obj_set_style_shadow_width(b,0,0);
    ui_text_font(lv_obj_get_child(b,0),&tab5_font_24,0);
    lv_obj_remove_flag(b,LV_OBJ_FLAG_EVENT_BUBBLE);return b;
}
static void gallery_controls_update(int category,int slot,bool ready) {
    if(!gallery_controls)return;
    if(!ready){visible(gallery_controls,false);visible(gallery_previous,false);visible(gallery_next,false);return;}
    gallery_image *image=&gallery_cache[slot];
    bool guide=gallery_reading&&(gallery_details_open||gallery_guide_copy(category,image->date,image->work_id,gallery_reading));
    // Image paging is independent of optional reading text. Legacy image-only
    // packs still have complete page counts and must remain manually browsable.
    bool pages=image->frames>1;
    visible(gallery_controls,guide);visible(gallery_details_button,guide);
    visible(gallery_date,false);
    if(!guide&&!pages){visible(gallery_previous,false);visible(gallery_next,false);visible(gallery_page_count,false);return;}
    gallery_controls_layout(image,guide);
    ui_bg_color(gallery_controls,lv_color_hex(category?0xE7E0D1:0x111516),0);
    visible(gallery_footer_title,guide&&image->portrait);visible(gallery_footer_museum,guide);
    if(guide){
        char brief[512];snprintf(brief,sizeof(brief),"%s · %s",gallery_reading->title,gallery_reading->author);
        gallery_caption_set(gallery_footer_title,gallery_caption_text.title,sizeof(gallery_caption_text.title),brief);
        gallery_caption_set(gallery_footer_museum,gallery_caption_text.museum,sizeof(gallery_caption_text.museum),gallery_reading->museum);
    }
    ui_text(gallery_footer_date,image->date);
    visible(gallery_previous,pages);visible(gallery_next,pages);visible(gallery_page_count,pages&&guide);
    if(gallery_pending_page.date[0])ui_text(gallery_page_count,"…");
    else ui_text_fmt(gallery_page_count,"%d / %d",image->frame+1,image->frames);
    uint32_t ink=category?0x776E60:0x929A96;
    lv_obj_t *buttons[]={gallery_previous,gallery_next,gallery_first,gallery_details_button};
    for(int i=0;i<4;i++)tint(lv_obj_get_child(buttons[i],0),ink);
    ui_bg_color(gallery_previous,lv_color_hex(category?0xE7E0D1:0x111516),0);
    ui_bg_color(gallery_next,lv_color_hex(category?0xE7E0D1:0x111516),0);
    ui_border_color(gallery_details_button,lv_color_hex(category?0xC4BAAA:0x3A4540),0);
    tint(gallery_page_count,ink);
    tint(gallery_footer_title,category?0x332E27:0xEDF0E9);tint(gallery_footer_museum,ink);tint(gallery_footer_date,ink);
}
static void gallery_reader_create(void) {
    lv_display_add_event_cb(lv_display_get_default(),gallery_reader_painted,LV_EVENT_REFR_READY,NULL);
    gallery_reading=calloc(1,sizeof(*gallery_reading));
    gallery_controls=lv_obj_create(gallery_layer);lv_obj_remove_style_all(gallery_controls);lv_obj_set_size(gallery_controls,1280,720);
    lv_obj_set_style_bg_opa(gallery_controls,LV_OPA_COVER,0);
    // Set once. Reapplying this at every 50 ms gallery tick invalidates the
    // caption strip and competes with input even when nothing has changed.
    lv_obj_set_style_opa(gallery_controls,LV_OPA_COVER,0);
    lv_obj_remove_flag(gallery_controls,LV_OBJ_FLAG_CLICKABLE|LV_OBJ_FLAG_SCROLLABLE);
    gallery_footer_title=label(gallery_controls,"",24,8,1040,false);
    gallery_footer_museum=label(gallery_controls,"",390,43,670,false);
    gallery_footer_date=label(gallery_controls,"",1100,8,160,false);
    memset(&gallery_caption_text,0,sizeof(gallery_caption_text));
    lv_obj_t *metadata[]={gallery_footer_title,gallery_footer_museum,gallery_footer_date};
    for(int i=0;i<3;i++){ui_text_font(metadata[i],&tab5_font_24,0);lv_label_set_long_mode(metadata[i],LV_LABEL_LONG_DOT);}
    gallery_previous=gallery_quiet_button(gallery_layer,LV_SYMBOL_LEFT,16,0,56,gallery_frame_clicked,-1);
    gallery_next=gallery_quiet_button(gallery_layer,LV_SYMBOL_RIGHT,1208,0,56,gallery_frame_clicked,1);
    lv_obj_t *arrows[]={gallery_previous,gallery_next};
    for(int i=0;i<2;i++) {
        lv_obj_set_style_bg_opa(arrows[i],LV_OPA_80,0);lv_obj_set_style_bg_opa(arrows[i],LV_OPA_COVER,LV_STATE_PRESSED);
        lv_obj_set_style_radius(arrows[i],28,0);
    }
    ui_text_font(lv_obj_get_child(gallery_previous,0),&lv_font_montserrat_24,0);
    ui_text_font(lv_obj_get_child(gallery_next,0),&lv_font_montserrat_24,0);
    gallery_first=gallery_quiet_button(gallery_controls,"|‹",156,650,56,gallery_frame_clicked,0);
    gallery_page_count=label(gallery_controls,"",224,665,150,false);
    gallery_details_button=gallery_quiet_button(gallery_controls,"作品详情",1080,650,180,gallery_details_clicked,0);
    lv_obj_set_style_border_width(gallery_details_button,1,0);lv_obj_set_style_radius(gallery_details_button,28,0);
    gallery_reader=lv_obj_create(saver_layer);lv_obj_remove_style_all(gallery_reader);lv_obj_set_size(gallery_reader,1280,720);
    lv_obj_set_style_bg_opa(gallery_reader,LV_OPA_COVER,0);lv_obj_add_flag(gallery_reader,LV_OBJ_FLAG_CLICKABLE);
    lv_obj_remove_flag(gallery_reader,LV_OBJ_FLAG_SCROLLABLE|LV_OBJ_FLAG_EVENT_BUBBLE);
    gallery_reader_title=label(gallery_reader,"",50,40,1180,false);ui_text_font(gallery_reader_title,&tab5_font_24,0);
    gallery_reader_meta=label(gallery_reader,"",50,90,1180,false);ui_text_font(gallery_reader_meta,&tab5_font_24,0);
    const char *chapters[]={"简介","欣赏提示","原文"};
    for(int i=0;i<3;i++) {
        gallery_chapters[i]=gallery_quiet_button(gallery_reader,chapters[i],50+i*160,160,140,gallery_reader_action,i);
        lv_obj_set_style_bg_opa(gallery_chapters[i],LV_OPA_COVER,0);
        lv_obj_set_style_bg_opa(gallery_chapters[i],LV_OPA_COVER,LV_STATE_PRESSED);
        lv_obj_set_style_border_width(gallery_chapters[i],1,0);lv_obj_set_style_radius(gallery_chapters[i],16,0);
    }
    gallery_reader_copy=lv_obj_create(gallery_reader);lv_obj_remove_style_all(gallery_reader_copy);lv_obj_set_size(gallery_reader_copy,1180,380);
    lv_obj_set_scroll_dir(gallery_reader_copy,LV_DIR_VER);lv_obj_set_scrollbar_mode(gallery_reader_copy,LV_SCROLLBAR_MODE_AUTO);
    lv_obj_add_flag(gallery_reader_copy,LV_OBJ_FLAG_CLICKABLE);lv_obj_remove_flag(gallery_reader_copy,LV_OBJ_FLAG_EVENT_BUBBLE);
    gallery_reader_body=label(gallery_reader_copy,"",0,0,1164,false);ui_text_font(gallery_reader_body,&tab5_font_24,0);
    lv_obj_set_style_text_line_space(gallery_reader_body,14,0);
    gallery_reader_back=gallery_quiet_button(gallery_reader,"返回作品",1054,642,176,gallery_reader_action,3);
    gallery_text_previous=gallery_quiet_button(gallery_reader,"上一段",50,642,128,gallery_reader_action,4);
    gallery_text_count=label(gallery_reader,"",194,660,110,false);ui_text_font(gallery_text_count,&tab5_font_24,0);
    gallery_text_next=gallery_quiet_button(gallery_reader,"下一段",310,642,128,gallery_reader_action,5);
    lv_obj_set_style_bg_opa(gallery_text_previous,LV_OPA_TRANSP,LV_STATE_DISABLED);lv_obj_set_style_bg_opa(gallery_text_next,LV_OPA_TRANSP,LV_STATE_DISABLED);
    lv_obj_set_style_opa(gallery_text_previous,LV_OPA_40,LV_STATE_DISABLED);lv_obj_set_style_opa(gallery_text_next,LV_OPA_40,LV_STATE_DISABLED);
    lv_obj_move_foreground(gallery_reader_back); // Keep the return action last in the reader's tab order.
    visible(gallery_reader,false);visible(gallery_controls,false);visible(gallery_previous,false);visible(gallery_next,false);
}
