// Native regression only: compare every captured pixel with the reference
// renderer without retaining hundreds of uncompressed image copies.
static FILE *render_audit_stream;
static bool render_audit_compare;
static unsigned render_audit_frames;
static uint64_t render_audit_pixels,render_audit_ticks;
static void render_audit_close(void) {
    if(!render_audit_stream)return;
    if(render_audit_compare)assert(fgetc(render_audit_stream)==EOF);
    assert(!fclose(render_audit_stream));render_audit_stream=NULL;
    LARGE_INTEGER frequency;QueryPerformanceFrequency(&frequency);
    printf("RENDER_BACKEND_AUDIT_OK frames=%u pixels=%llu renderUs=%llu mode=%s\n",
        render_audit_frames,(unsigned long long)render_audit_pixels,
        (unsigned long long)(render_audit_ticks*1000000/frequency.QuadPart),
        render_audit_compare?"pixel-exact comparison":"reference");
}
static bool render_audit_capture(const char *name,const lv_draw_buf_t *snap,const uint16_t *display,LONGLONG elapsed) {
    const char *path=getenv("TAB5_RENDER_AUDIT_STREAM");if(!path||!*path)return true;
    if(!render_audit_stream){
        render_audit_compare=getenv("TAB5_RENDER_AUDIT_COMPARE")!=NULL;
        render_audit_stream=fopen(path,render_audit_compare?"rb":"wb");assert(render_audit_stream);
        assert(!atexit(render_audit_close));
    }
    uint32_t header[3]={(uint32_t)strlen(name),snap->header.w,snap->header.h};
    assert(header[0]<160&&header[1]<=1280&&header[2]<=1280);
    if(render_audit_compare){
        uint32_t expected[3];char expected_name[160];
        assert(fread(expected,sizeof(expected),1,render_audit_stream)==1&&!memcmp(header,expected,sizeof(header)));
        assert(fread(expected_name,1,header[0],render_audit_stream)==header[0]&&!memcmp(name,expected_name,header[0]));
        uint8_t row[2560];
        for(unsigned y=0;y<header[2];y++){
            size_t bytes=header[1]*2;assert(fread(row,1,bytes,render_audit_stream)==bytes);
            if(memcmp(row,snap->data+y*snap->header.stride,bytes)){
                fprintf(stderr,"RENDER_PIXEL_MISMATCH name=%s row=%u\n",name,y);assert(false);
            }
        }
        for(unsigned y=0;y<720;y++){
            assert(fread(row,1,2560,render_audit_stream)==2560);
            if(memcmp(row,display+y*1280,2560)){
                fprintf(stderr,"RENDER_DISPLAY_MISMATCH name=%s row=%u\n",name,y);assert(false);
            }
        }
    }else{
        assert(fwrite(header,sizeof(header),1,render_audit_stream)==1);
        assert(fwrite(name,1,header[0],render_audit_stream)==header[0]);
        for(unsigned y=0;y<header[2];y++)assert(fwrite(snap->data+y*snap->header.stride,2,header[1],render_audit_stream)==header[1]);
        assert(fwrite(display,2,1280*720,render_audit_stream)==1280*720);
    }
    render_audit_frames++;render_audit_pixels+=(uint64_t)header[1]*header[2]+1280*720;
    render_audit_ticks+=(uint64_t)elapsed;
    const char *keep=getenv("TAB5_RENDER_AUDIT_KEEP");
    if(!keep)return false;
    char token[164];snprintf(token,sizeof(token),",%s,",name);return strstr(keep,token)!=NULL;
}
