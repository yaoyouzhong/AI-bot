static void project_page_id(char out[17],const char *root,int page) {
    char input[40];uint8_t digest[32];snprintf(input,sizeof(input),"%s:page:%d",root,page);
    assert(!mbedtls_sha256((const uint8_t *)input,strlen(input),digest,0));
    for(int i=0;i<8;i++)snprintf(out+i*2,3,"%02x",digest[i]);
}
static void check_project_virtual_list(void) {
    reply_open_state_setup(1);
    cJSON *remote=item(shown,"claudeTasks"),*projects=item(remote,"projects"),*tasks=item(remote,"tasks");
    cJSON_DeleteItemFromArray(projects,0);cJSON_DeleteItemFromArray(tasks,0);
    const char *root="1111111111111111";char ids[8][17];snprintf(ids[0],17,"%s",root);
    for(int i=1;i<8;i++)project_page_id(ids[i],root,i);
    for(int i=0;i<8;i++) {
        cJSON *project=cJSON_CreateObject();char caption[80];snprintf(caption,sizeof(caption),"%d/8 · AI-bot",i+1);
        cJSON_AddStringToObject(project,"id",ids[i]);cJSON_AddStringToObject(project,"name",caption);cJSON_AddItemToArray(projects,project);
    }
    // An unrelated project with an identical-looking name must remain separate.
    cJSON *other=cJSON_CreateObject();cJSON_AddStringToObject(other,"id","4444444444444444");
    cJSON_AddStringToObject(other,"name","2/8 · AI-bot");cJSON_AddItemToArray(projects,other);
    for(int i=0;i<64;i++) {
        char id[37],title[40];snprintf(id,sizeof(id),"aaaaaaaa-aaaa-4aaa-8aaa-%012d",i);
        if(i==0)snprintf(title,sizeof(title),"AI-bot-原生终端");
        else snprintf(title,sizeof(title),"会话 %02d",i+1);
        cJSON *task=cJSON_CreateObject();cJSON_AddStringToObject(task,"id",id);cJSON_AddStringToObject(task,"title",title);
        cJSON_AddStringToObject(task,"projectId",ids[i/8]);cJSON_AddNumberToObject(task,"updatedAt",100-i);
        cJSON_AddItemToArray(tasks,task);
    }
    if(!codex_notices)codex_notices=calloc(80,sizeof(*codex_notices));
    memset(codex_notices,0,80*sizeof(*codex_notices));
    codex_notice_sequence=0;codex_list_unread_client=-1;codex_list_unread_dirty=false;
    const char *head="aaaaaaaa-aaaa-4aaa-8aaa-000000000000",*tail="aaaaaaaa-aaaa-4aaa-8aaa-000000000063";
    const char *other_task="bbbbbbbb-bbbb-4bbb-8bbb-000000000001";
    cJSON *unrelated=cJSON_CreateObject();cJSON_AddStringToObject(unrelated,"id",other_task);
    cJSON_AddStringToObject(unrelated,"title","其他项目会话");cJSON_AddStringToObject(unrelated,"projectId","4444444444444444");
    cJSON_AddItemToArray(tasks,unrelated);
    workspace_client=1;codex_selected[0]=codex_project_selected[0]=0;codex_default_pending=codex_default_all=true;
    render_codex();render_codex();lv_obj_update_layout(lv_screen_active());
    assert(codex_project_count==2);
    assert(strstr(lv_dropdown_get_options(codex_project_picker),"AI-bot\n2/8 · AI-bot"));
    assert(!strcmp(lv_label_get_text(codex_project_caption),"AI-bot"));
    assert(!strcmp(codex_task_ids[0],"aaaaaaaa-aaaa-4aaa-8aaa-000000000000"));
    capture("project-virtual-top");
    lv_indev_t *touch=lv_indev_create();lv_indev_set_type(touch,LV_INDEV_TYPE_POINTER);
    lv_indev_set_display(touch,lv_display_get_default());lv_indev_set_read_cb(touch,camera_touch_read);
    lv_area_t area;lv_obj_get_coords(codex_list,&area);
    camera_touch_point=(lv_point_t){area.x1+100,area.y1+260};
    camera_touch_state=LV_INDEV_STATE_PRESSED;lv_indev_read(touch);
    for(int i=0;i<8;i++){camera_touch_point.y-=20;lv_tick_inc(18);lv_indev_read(touch);}
    camera_touch_state=LV_INDEV_STATE_RELEASED;lv_indev_read(touch);
    assert(lv_obj_get_scroll_y(codex_list)>0&&!strcmp(codex_selected,"aaaaaaaa-aaaa-4aaa-8aaa-000000000000"));
    lv_obj_scroll_to_y(codex_list,12*88,LV_ANIM_OFF);lv_obj_update_layout(lv_screen_active());
    assert(!strcmp(codex_task_ids[0],"aaaaaaaa-aaaa-4aaa-8aaa-000000000011"));
    assert(!strcmp(codex_selected,"aaaaaaaa-aaaa-4aaa-8aaa-000000000000"));
    capture("project-virtual-middle");
    // Selecting an older visible row uses its exact session ID.
    lv_obj_get_coords(codex_task_rows[2],&area);
    reply_pointer_tap(touch,(lv_point_t){area.x1+40,(area.y1+area.y2)/2});
    assert(!strcmp(codex_selected,"aaaaaaaa-aaaa-4aaa-8aaa-000000000013"));
    assert(codex_draft_for(codex_selected,false));
    lv_obj_scroll_to_y(codex_list,LV_COORD_MAX,LV_ANIM_OFF);lv_obj_update_layout(lv_screen_active());
    assert(!strcmp(codex_task_ids[4],"aaaaaaaa-aaaa-4aaa-8aaa-000000000063"));
    assert(!strcmp(codex_selected,"aaaaaaaa-aaaa-4aaa-8aaa-000000000013"));
    capture("project-virtual-bottom");
    cJSON_DeleteItemFromObject(remote,"repliesReady");cJSON *ready=cJSON_AddObjectToObject(remote,"repliesReady");
    cJSON_DeleteItemFromObject(remote,"repliesRead");cJSON *read=cJSON_AddObjectToObject(remote,"repliesRead");
    codex_notices_ready=true;
    cJSON_AddStringToObject(ready,tail,"tail-turn");workspace_notice_tick(100,true,1);render_codex();
    assert(lv_obj_get_scroll_y(codex_list)==0&&!strcmp(codex_task_ids[0],tail));
    assert(!lv_obj_has_flag(session_unread_rows[0],LV_OBJ_FLAG_HIDDEN));
    lv_obj_scroll_to_y(codex_list,LV_COORD_MAX,LV_ANIM_OFF);lv_obj_update_layout(lv_screen_active());
    int bottom=lv_obj_get_scroll_y(codex_list);
    assert(bottom>0);
    // Hold the list's scrollbar gutter. A new reply must not move a row under
    // a finger, or reselect the current conversation on release.
    lv_obj_get_coords(codex_list,&area);camera_touch_point=(lv_point_t){area.x2-1,area.y1+32};
    camera_touch_state=LV_INDEV_STATE_PRESSED;lv_indev_read(touch);
    cJSON_AddStringToObject(ready,head,"head-turn");workspace_notice_tick(110,true,1);render_codex();
    assert(lv_obj_get_scroll_y(codex_list)==bottom&&codex_list_unread_dirty);
    camera_touch_state=LV_INDEV_STATE_RELEASED;lv_indev_read(touch);render_codex();
    assert(lv_obj_get_scroll_y(codex_list)==0&&!strcmp(codex_task_ids[0],head)&&!strcmp(codex_task_ids[1],tail));
    assert(!lv_obj_has_flag(session_unread_rows[0],LV_OBJ_FLAG_HIDDEN)&&
           !lv_obj_has_flag(session_unread_rows[1],LV_OBJ_FLAG_HIDDEN));
    assert(!strcmp(codex_selected,"aaaaaaaa-aaaa-4aaa-8aaa-000000000013"));
    capture("project-virtual-unread-first");
    lv_obj_scroll_to_y(codex_list,LV_COORD_MAX,LV_ANIM_OFF);lv_obj_update_layout(lv_screen_active());
    bottom=lv_obj_get_scroll_y(codex_list);
    workspace_notice_tick(120,true,1);render_codex();
    assert(bottom>0&&lv_obj_get_scroll_y(codex_list)==bottom);
    cJSON_AddStringToObject(ready,other_task,"unrelated-turn");workspace_notice_tick(130,true,1);render_codex();
    assert(!strcmp(codex_project_selected,root)&&lv_obj_get_scroll_y(codex_list)==bottom);
    cJSON_AddStringToObject(read,head,"head-turn");workspace_notice_tick(140,true,1);render_codex();
    lv_obj_scroll_to_y(codex_list,0,LV_ANIM_OFF);
    assert(!strcmp(codex_task_ids[0],tail)&&!session_unread_task(head));
    // Opening a prioritized unread item from Overview keeps its exact target.
    lv_obj_scroll_to_y(codex_list,0,LV_ANIM_OFF);page(0);
    workspace_open_task(1,tail,0);
    assert(current_page==8&&!strcmp(codex_selected,tail)&&!strcmp(codex_task_ids[0],tail));
    assert(lv_obj_get_scroll_y(codex_list)==0);
    // Older Windows Claude labels use the suffix form, with the same IDs.
    for(int i=0;i<8;i++) {
        char label[80];snprintf(label,sizeof(label),"AI-bot · %d/8",i+1);
        cJSON_ReplaceItemInObject(cJSON_GetArrayItem(projects,i),"name",cJSON_CreateString(label));
    }
    workspace_groups_build(projects);
    for(int i=0;i<8;i++)assert(!strcmp(workspace_project_root(ids[i]),root));
    lv_indev_delete(touch);
    puts("PROJECT_VIRTUAL_LIST_OK 64 exact sessions; unread priority and one reveal; touch deferral; stable selection; unrelated project isolated");
}
