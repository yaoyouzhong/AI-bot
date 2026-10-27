#undef NDEBUG
#include "lvgl.h"
#include "src/draw/lv_image_decoder_private.h"
#include "brand_logos.h"
#include <assert.h>
#include <stdio.h>
#include <string.h>
extern const lv_image_dsc_t *baseline_brand_images[];
static const lv_image_dsc_t *images[]={
    &brand_openai_image,&brand_deepseek_image,&brand_zhipu_image,&brand_codex_image,
    &brand_codex_key_image,&brand_doubao_image,&brand_claude_image,&brand_alibaba_image,
    &brand_kimi_image,&brand_minimax_image,&brand_stepfun_image,&brand_baidu_image,
    &brand_xiaomi_image,&brand_volcengine_image,&brand_openai_option_image
};
int main(void) {
    lv_init();unsigned count=sizeof(images)/sizeof(*images);
    for(unsigned pass=0;pass<16;pass++)for(unsigned i=0;i<count;i++) {
        lv_image_decoder_dsc_t before={0},after={0};
        assert(lv_image_decoder_open(&before,baseline_brand_images[i],NULL)==LV_RESULT_OK);
        assert(lv_image_decoder_open(&after,images[i],NULL)==LV_RESULT_OK);
        assert(before.decoded&&after.decoded);
        const lv_draw_buf_t *a=before.decoded,*b=after.decoded;
        assert(a->header.w==b->header.w&&a->header.h==b->header.h&&a->header.cf==b->header.cf&&a->header.stride==b->header.stride);
        assert(a->data_size==b->data_size&&!memcmp(a->data,b->data,a->data_size));
        lv_image_decoder_close(&after);lv_image_decoder_close(&before);
    }
    printf("RESOURCE_LOSSLESS_OK %u images, 16 decoder open/close cycles, exact dimensions/colors/alpha/pixels\n",count);
}
