// Visible markers share the existing exact-turn read acknowledgement queue.
// A reduced catalog hides unreachable markers without discarding that queue.
static lv_obj_t *session_unread_rows[8],*session_unread_clients[2],*session_project_label;
static void workspace_open_task(unsigned client,const char *task,int return_page);
static unsigned session_unread_order(const char *id) {
    if(!codex_notices||!id||strlen(id)!=36)return 0;
    for(int i=0;i<80;i++)if(codex_notices[i].unread&&!strcmp(codex_notices[i].task,id))
        return LV_MAX(1u,codex_notices[i].arrived);
    return 0;
}
static bool session_unread_task(const char *id) {return session_unread_order(id)!=0;}
static unsigned session_unread_count(unsigned client,const char *project) {
    unsigned count=0;cJSON *task=NULL;
    cJSON_ArrayForEach(task,item(item(shown,client?"claudeTasks":"codexTasks"),"tasks"))
        if((!project||!strcmp(workspace_project_root(text(task,"projectId")),project))&&session_unread_task(text(task,"id")))count++;
    return count;
}
static void session_unread_center(lv_obj_t *label) {
    if(lv_obj_get_style_align(label,0)!=LV_ALIGN_CENTER||lv_obj_get_style_x(label,0)||lv_obj_get_style_y(label,0))lv_obj_center(label);
}
static lv_obj_t *session_unread_marker(lv_obj_t *parent,const char *caption,int width,int height) {
    lv_obj_t *marker=lv_obj_create(parent);lv_obj_remove_style_all(marker);lv_obj_set_size(marker,width,height);
    lv_obj_set_style_bg_opa(marker,LV_OPA_COVER,0);lv_obj_set_style_bg_color(marker,lv_color_hex(0xF1BB57),0);
    lv_obj_set_style_radius(marker,6,0);
    lv_obj_remove_flag(marker,LV_OBJ_FLAG_CLICKABLE|LV_OBJ_FLAG_SCROLLABLE|LV_OBJ_FLAG_CLICK_FOCUSABLE);
    lv_obj_t *value=lv_label_create(marker);lv_obj_set_style_text_font(value,&tab5_font_24,0);
    lv_obj_set_style_text_color(value,lv_color_hex(0x283438),0);lv_label_set_text(value,caption);lv_obj_center(value);
    lv_obj_remove_flag(value,LV_OBJ_FLAG_CLICKABLE|LV_OBJ_FLAG_SCROLLABLE|LV_OBJ_FLAG_CLICK_FOCUSABLE);
    visible(marker,false);return marker;
}
static void session_unread_style(void) {
    for(unsigned client=0;client<2;client++) {
        unsigned count=session_unread_count(client,NULL);
        lv_obj_t *marker=session_unread_clients[client];if(!marker)continue;
        lv_obj_t *value=lv_obj_get_child(marker,0);
        if(count>9)ui_text(value,"9+");else ui_text_fmt(value,"%u",count);
        session_unread_center(value);visible(marker,count>0);
    }
    for(int i=0;i<8;i++) {
        if(!session_unread_rows[i])continue;
        bool unread=session_unread_task(codex_task_ids[i]),active=!strcmp(codex_task_ids[i],codex_selected);
        visible(session_unread_rows[i],unread);
        session_source_width(codex_task_titles[i],unread?240:308);
        session_source_pos(session_unread_rows[i],248,workspace_client?0:10);
        ui_bg_color(codex_task_rows[i],lv_color_hex(active?0x465C65:unread?0x41483E:0x354247),0);
        ui_border_opa(codex_task_rows[i],active||unread?LV_OPA_COVER:LV_OPA_TRANSP,0);
        ui_border_color(codex_task_rows[i],lv_color_hex(active?BLUE:0xF1BB57),0);
    }
}
