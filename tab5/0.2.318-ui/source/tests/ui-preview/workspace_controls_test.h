static cJSON *ready_control(const char *task,bool running) {
    cJSON *v=cJSON_CreateObject();cJSON_AddStringToObject(v,"taskId",task);
    cJSON_AddStringToObject(v,"turnId",running?"aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa":"");
    cJSON_AddBoolToObject(v,"running",running);cJSON_AddBoolToObject(v,"waiting",false);
    cJSON_AddBoolToObject(v,"canSend",!running);cJSON_AddBoolToObject(v,"canSteer",running);cJSON_AddBoolToObject(v,"canInterrupt",running);return v;
}
static void check_workspace_controls(void) {
    int sends=preview_submit_count,reads=preview_reads;
    reply_open_state_setup(1);preview_usb_connected=true;last_link=LINK_USB;
    const char *a="11111111-1111-4111-8111-111111111111",*b="22222222-2222-4222-8222-222222222222";
    cJSON *remote=item(shown,"claudeTasks"),*tasks=item(remote,"tasks"),*second=cJSON_Duplicate(cJSON_GetArrayItem(tasks,0),true);
    cJSON_ReplaceItemInObject(second,"id",cJSON_CreateString(b));cJSON_ReplaceItemInObject(second,"title",cJSON_CreateString("Claude Code原生终端"));cJSON_AddItemToArray(tasks,second);
    cJSON *controls=cJSON_AddObjectToObject(remote,"controls");cJSON_AddItemToObject(controls,a,ready_control(a,false));cJSON_AddItemToObject(controls,b,ready_control(b,false));
    // The last read belongs to A; B has never been read and must be ready on selection.
    cJSON_AddItemToObject(remote,"control",ready_control(a,false));render_codex();
    assert(!lv_obj_has_state(codex_send_button,LV_STATE_DISABLED));
    for(int i=0;i<4;i++){
        codex_select_task(i%2?a:b);
        assert(!lv_obj_has_state(codex_send_button,LV_STATE_DISABLED));
        assert(!strcmp(lv_label_get_text(lv_obj_get_child(codex_send_button,0)),"发送"));
    }
    codex_select_task(b);capture("native-session-send-ready");
    codex_select_task(a);capture("vscode-session-send-ready");
    // Explicit unavailable capability must override the old positive viewed control.
    cJSON_ReplaceItemInObject(controls,a,cJSON_CreateNull());codex_control_style();assert(lv_obj_has_state(codex_send_button,LV_STATE_DISABLED));
    cJSON_ReplaceItemInObject(controls,a,ready_control(b,false));codex_control_style();assert(lv_obj_has_state(codex_send_button,LV_STATE_DISABLED));
    cJSON_ReplaceItemInObject(controls,a,ready_control(a,true));codex_control_style();
    assert(codex_running()&&!lv_obj_has_state(codex_send_button,LV_STATE_DISABLED));
    assert(!strcmp(lv_label_get_text(lv_obj_get_child(codex_send_button,0)),"追加"));
    cJSON_ReplaceItemInObject(item(controls,a),"canSteer",cJSON_CreateFalse());codex_control_style();assert(lv_obj_has_state(codex_send_button,LV_STATE_DISABLED));
    cJSON_ReplaceItemInObject(controls,a,ready_control(a,false));
    preview_now_us+=7000000;codex_control_style();assert(lv_obj_has_state(codex_send_button,LV_STATE_DISABLED));
    last_received=preview_now_us/1000;codex_control_style();assert(!lv_obj_has_state(codex_send_button,LV_STATE_DISABLED));
    codex_draft *draft=codex_draft_for(a,true);snprintf(draft->request,sizeof(draft->request),"33333333-3333-4333-8333-333333333333");
    cJSON_ReplaceItemInObject(controls,a,cJSON_CreateNull());codex_control_style();
    assert(!lv_obj_has_state(codex_send_button,LV_STATE_DISABLED)&&!strcmp(lv_label_get_text(lv_obj_get_child(codex_send_button,0)),"核对送达"));draft->request[0]=0;
    // Older producers with no per-task map keep the exact-task legacy path.
    cJSON_DeleteItemFromObject(remote,"controls");codex_control_style();assert(!lv_obj_has_state(codex_send_button,LV_STATE_DISABLED));
    codex_select_task(b);assert(lv_obj_has_state(codex_send_button,LV_STATE_DISABLED));
    assert(preview_reads==reads&&preview_submit_count==sends);
    puts("WORKSPACE_CONTROLS_OK immediate unread-session switches; exact task; unavailable/waiting/stale controls; running turn; receipt recovery; legacy fallback; zero reads/sends");
}
