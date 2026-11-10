static bool simulated_snap;
static int snap_hold(const lv_obj_t *obj){return obj==pages[8]&&simulated_snap?2:0;}
static unsigned text_differences(const uint16_t *a,const uint16_t *b) {
    lv_area_t area;lv_obj_get_coords(codex_input,&area);unsigned count=0;
    for(int y=area.y1+4;y<area.y2-4;y++)for(int x=area.x1+22;x<area.x2-4;x++)
        count+=a[y*1280+x]!=b[y*1280+x];
    return count;
}
static void check_cache_publication_refresh(void) {
    page(8);codex_keyboard_done(NULL);lv_anim_delete_all();
    strcpy(codex_selected,"00000000-0000-4000-8000-000000000111");
    codex_voice_waiting=codex_waiting=codex_send_queued=false;
    lv_obj_remove_state(codex_input,LV_STATE_DISABLED);
    codex_set_text("待清空文字：松手后仍应为空。");assert(codex_save_draft());codex_control_style();
    codex_draft *draft=codex_draft_for(codex_selected,true);assert(draft);draft->loaded=true;
    lv_display_t *display=lv_display_get_default();
    camera_partial_pixels=calloc(1,sizeof(preview_buffer));assert(camera_partial_pixels);
    uint16_t *empty=malloc(sizeof(preview_buffer));assert(empty);
    lv_display_set_flush_cb(display,camera_partial_flush);
    lv_display_set_buffers(display,preview_buffer,NULL,1280u*24u*2u,LV_DISPLAY_RENDER_MODE_PARTIAL);
    tab5_draw_cache_drop();tab5_draw_cache_interaction=snap_hold;
    lv_obj_update_layout(lv_screen_active());lv_obj_invalidate(lv_screen_active());lv_refr_now(display);
    for(unsigned i=0;i<20&&!tab5_draw_cache_is_ready(pages[8]);i++)tab5_draw_cache_step(pages[8]);
    assert(tab5_draw_cache_is_ready(pages[8]));
    lv_obj_send_event(codex_clear_button,LV_EVENT_LONG_PRESSED,NULL);lv_refr_now(display);
    assert(!lv_textarea_get_text(codex_input)[0]&&(!draft->text||!draft->text[0]));
    memcpy(empty,camera_partial_pixels,sizeof(preview_buffer));
    // During a real horizontal snap the cache intentionally retains a complete
    // prior image. Emulate only that documented cache boundary, then release it.
    simulated_snap=true;unsigned hits=tab5_cache_hits;
    lv_obj_invalidate(pages[8]);lv_refr_now(display);
    unsigned stale=text_differences(empty,camera_partial_pixels);
    assert(tab5_cache_hits>hits&&stale>0);
    simulated_snap=false;
    // The visible page must recover immediately on release, even when a cold
    // neighbour or continuous updates delay rebuilding the snapshot.
    lv_tick_inc(500);cache_tick_work(NULL);lv_refr_now(display);
    unsigned release_differences=text_differences(empty,camera_partial_pixels);
    printf("STALE_ON_RELEASE differences=%u\n",release_differences);
    assert(release_differences==0);
    for(unsigned i=0;i<20&&!tab5_draw_cache_is_ready(pages[8]);i++)tab5_draw_cache_step(pages[8]);
    assert(tab5_draw_cache_is_ready(pages[8]));
    // No forced invalidation, capture(), or navigation: idle publication must
    // schedule the repaint needed to replace any stale pixels it displayed.
    lv_refr_now(display);
    unsigned after=text_differences(empty,camera_partial_pixels);
    printf("CLEAR_CACHE_PUBLICATION logicalEmpty=%d stalePixels=%u afterIdleRepair=%u\n",!lv_textarea_get_text(codex_input)[0],stale,after);
    assert(!after);
    assert(tab5_draw_cache_is_ready(pages[8]));
    unsigned builds=tab5_cache_builds;
    for(unsigned i=0;i<20;i++)tab5_draw_cache_step(pages[8]);
    assert(tab5_cache_builds==builds);
    puts("CLEAR_CACHE_REPAINT_OK 24-row incremental framebuffer; no forced redraw after repair");
    free(empty);free(camera_partial_pixels);camera_partial_pixels=NULL;
}
