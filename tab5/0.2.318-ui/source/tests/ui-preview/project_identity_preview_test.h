static void check_project_identity_preview(void) {
    const char *fixture="{\"epochMilliseconds\":1793758000000,\"pcInput\":{\"session\":\"identity-preview\"},\"codexTasks\":{\"projects\":[{\"id\":\"1111111111111111\",\"name\":\"1/3 · AI-bot\"},{\"id\":\"2222222222222222\",\"name\":\"2/3 · AI-bot\"},{\"id\":\"3333333333333333\",\"name\":\"3/3 · AI-bot\"},{\"id\":\"4444444444444444\",\"name\":\"Personal/AI-bot\"}],\"tasks\":[{\"id\":\"11111111-1111-4111-8111-111111111111\",\"projectId\":\"1111111111111111\",\"title\":\"当前项目会话\"}]}}";
    cJSON_Delete(shown);shown=cJSON_Parse(fixture);assert(shown);
    workspace_client=0;codex_selected[0]=codex_project_selected[0]=0;codex_default_pending=codex_default_all=true;
    page(8);render_codex();lv_obj_update_layout(lv_screen_active());
    assert(!strcmp(lv_label_get_text(codex_project_caption),"1/3 · AI-bot"));
    assert(strstr(lv_dropdown_get_options(codex_project_picker),"2/3 · AI-bot"));
    capture("project-identity-closed");
    lv_dropdown_open(codex_project_picker);assert(lv_dropdown_is_open(codex_project_picker));
    capture("project-identity-open");
    puts("PROJECT_IDENTITY_PREVIEW_OK distinct projects and page labels");
}
