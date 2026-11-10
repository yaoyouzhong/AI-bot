// Real LVGL objects and production UI/cache transfer functions; no device I/O.
static void check_preview_gallery_loan(void) {
#if TAB5_LOCAL_ART
    assert(gallery_view&&!gallery_motion_active);
    for(int i=0;i<4;i++)assert(!gallery_cache[i].pixels);
    uint8_t *shown=malloc(1280u*720u*2u),*unused=malloc(1280u*720u*2u);
    assert(shown&&unused);memset(shown,0x3c,1280u*720u*2u);memset(unused,0x5a,1280u*720u*2u);
    gallery_cache[0]=(gallery_image){.pixels=shown,.frame=0,.category=0};
    gallery_cache[1]=(gallery_image){.pixels=unused,.frame=1,.category=0};
    for(int i=0;i<2;i++)gallery_descriptor[i]=(lv_image_dsc_t){.header={.magic=LV_IMAGE_HEADER_MAGIC,
        .cf=LV_COLOR_FORMAT_RGB565,.w=1280,.h=720,.stride=2560},.data_size=1280u*720u*2u,.data=gallery_cache[i].pixels};
    gallery_presented=gallery_cache[0];visible(gallery_view,true);lv_image_set_src(gallery_view,&gallery_descriptor[0]);
    gallery_retry[1]=12345;
    // Neither a transition frame nor the presented image may be borrowed.
    gallery_motion_active=true;assert(!ui_screen_preview_take_buffer());gallery_motion_active=false;
    gallery_old_image=gallery_cache[1];assert(!ui_screen_preview_take_buffer());gallery_old_image=(gallery_image){0};
    uint8_t *loan=ui_screen_preview_take_buffer();assert(loan==unused);
    assert(!gallery_cache[1].pixels&&!gallery_descriptor[1].data&&!gallery_retry[1]);
    assert(lv_image_get_src(gallery_view)==&gallery_descriptor[0]&&gallery_presented.pixels==shown);
    assert(!ui_screen_preview_take_buffer()); // Cannot lend twice or overwrite the sole displayed frame.
    visible(gallery_view,false);assert(!ui_screen_preview_take_buffer()); // Hidden source still referenced by LVGL.
    memset(loan,0x77,1280u*720u*2u);
    for(size_t i=0;i<1280u*720u*2u;i++)assert(shown[i]==0x3c);
    gallery_recycle(loan);assert(gallery_take_buffer()==loan&&!gallery_take_buffer());
    gallery_recycle(loan);visible(gallery_view,true);
    // Foreground decode waits for a running capture, then reclaims idle scratch
    // and reuses the returned spare instead of allocating a third full frame.
    preview_gallery_ready=preview_gallery_download=true;preview_screen_busy=true;
    unsigned requests=preview_gallery_requests,reclaims=preview_scratch_reclaims;
    assert(!gallery_schedule(0,"2026-10-29",2,false));
    assert(preview_gallery_requests==requests&&preview_scratch_reclaims==reclaims);
    preview_screen_busy=false;assert(gallery_schedule(0,"2026-10-29",2,false));
    assert(preview_gallery_requests==requests+1&&preview_scratch_reclaims==reclaims+1&&preview_gallery_spare==loan);
    gallery_image result={0};assert(gallery_take(&result)&&result.pixels==loan);gallery_release_buffer(result.pixels);
    assert(shown[0]==0x3c&&shown[1280u*720u*2u-1]==0x3c);
    visible(gallery_view,false);lv_image_set_src(gallery_view,NULL);gallery_presented=(gallery_image){0};gallery_drop(0);
    gallery_release_buffer(gallery_take_buffer());
    puts("PREVIEW_GALLERY_LOAN_OK real LVGL: unused cache transfers once, shown/hidden sources and transition protected, no displayed pixel mutation, foreground reclaim and reuse");
#else
    assert(!ui_screen_preview_take_buffer());
#endif
}
