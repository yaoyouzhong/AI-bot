static void check_voice_stop_touch(void) {
 page(8);lv_anim_delete_all();lv_display_set_rotation(lv_display_get_default(),LV_DISPLAY_ROTATION_0);
 strcpy(codex_selected,"00000000-0000-4000-8000-000000000111");
 codex_voice_desktop=quick_visible=false;codex_voice_waiting=true;
 lv_indev_t *touch=lv_indev_create();lv_indev_set_type(touch,LV_INDEV_TYPE_POINTER);
 lv_indev_set_display(touch,lv_display_get_default());lv_indev_set_read_cb(touch,camera_touch_read);
 lv_obj_update_layout(lv_screen_active());
 lv_area_t area;lv_obj_get_coords(codex_voice_button,&area);printf("STOP_HIT bounds=%d,%d,%d,%d hidden=%d\n",area.x1,area.y1,area.x2,area.y2,lv_obj_has_flag(codex_voice_button,LV_OBJ_FLAG_HIDDEN));
 for(int dy=-34;dy<=34;dy+=17)for(int dx=-34;dx<=34;dx+=17) {
  preview_voice=(voice_remote_state){.busy=true,.ready=true};strcpy(preview_voice.phase,"recording");strcpy(preview_voice.source,"dji");strcpy(preview_voice.task_id,codex_selected);codex_voice_tick();
  lv_obj_update_layout(lv_screen_active());unsigned before=preview_voice_commands;
  camera_touch_point=(lv_point_t){(area.x1+area.x2)/2+dx,(area.y1+area.y2)/2+dy};camera_touch_state=LV_INDEV_STATE_PRESSED;lv_indev_read(touch);
  assert(preview_voice_commands==before+1); // Stop must be accepted on press, before release.
  assert(!strcmp(preview_voice.phase,"draining")&&strstr(lv_label_get_text(codex_voice_hint),"结束"));
  codex_voice_tick(); // Actual periodic update can happen before release.
  camera_touch_state=LV_INDEV_STATE_RELEASED;lv_indev_read(touch);
  printf("STOP_HIT dx=%d dy=%d commands=%u disabled=%d\n",dx,dy,preview_voice_commands-before,lv_obj_has_state(codex_voice_button,LV_STATE_DISABLED));
  assert(preview_voice_commands==before+1&&preview_voice_command==1);
 }
 // A fast final reply between press/release must not turn the same gesture
 // into another recording. New start still waits for its separate release.
 if(!shown)shown=cJSON_CreateObject();cJSON *pc=cJSON_CreateObject();cJSON_AddStringToObject(pc,"session","synthetic-session");
 cJSON_DeleteItemFromObjectCaseSensitive(shown,"pcInput");cJSON_AddItemToObject(shown,"pcInput",pc);
 last_received=esp_timer_get_time()/1000;preview_voice_start_enabled=true;
 codex_draft *d=codex_draft_for(codex_selected,true);assert(d);d->loaded=true;
 codex_voice_waiting=true;preview_voice.busy=preview_voice.ready=true;strcpy(preview_voice.phase,"recording");codex_voice_tick();
 unsigned starts=preview_voice_starts,before=preview_voice_commands;
 camera_touch_point=(lv_point_t){(area.x1+area.x2)/2,(area.y1+area.y2)/2};camera_touch_state=LV_INDEV_STATE_PRESSED;lv_indev_read(touch);
 assert(preview_voice_commands==before+1);
 preview_voice.busy=preview_voice.ready=false;strcpy(preview_voice.phase,"review");codex_voice_tick();assert(!codex_voice_waiting);
 camera_touch_state=LV_INDEV_STATE_RELEASED;lv_indev_read(touch);assert(preview_voice_starts==starts);
 codex_reading=codex_syncing=codex_load_waiting=codex_waiting=codex_send_queued=false;
 lv_obj_remove_state(codex_input,LV_STATE_DISABLED);last_received=esp_timer_get_time()/1000;
 camera_touch_state=LV_INDEV_STATE_PRESSED;lv_indev_read(touch);assert(preview_voice_starts==starts);
 camera_touch_state=LV_INDEV_STATE_RELEASED;lv_indev_read(touch);assert(preview_voice_starts==starts+1);
 lv_indev_reset(touch,NULL);lv_indev_delete(touch);puts("VOICE_STOP_TOUCH_OK 25 actual pointer press/tick/release hits; stop on press, 72px effective target, no duplicate/restart on release");
}
