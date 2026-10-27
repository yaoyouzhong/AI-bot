#ifdef NDEBUG
#undef NDEBUG
#endif
#include <assert.h>
#include <stdbool.h>
#include <stdio.h>
enum {DMA2D_DATA_BURST_LENGTH_16=16,DMA2D_MACRO_BLOCK_SIZE_NONE=0,DMA2D_MACRO_BLOCK_SIZE_8_8=1,
    DMA2D_MACRO_BLOCK_SIZE_8_16=2,DMA2D_MACRO_BLOCK_SIZE_16_16=3,JPEG_DOWN_SAMPLING_YUV444=0,
    JPEG_DOWN_SAMPLING_YUV422=1,JPEG_DOWN_SAMPLING_YUV420=2,JPEG_DOWN_SAMPLING_GRAY=3};
typedef struct {int data_burst_length;bool desc_burst_en;int mb_size;} dma2d_transfer_ability_t;
typedef struct {int sub_sample;} header;
typedef struct {header *header_info;int dma2d_tx_channel,dma2d_rx_channel;} engine;
typedef engine *jpeg_encoder_handle_t;
static unsigned calls;
static dma2d_transfer_ability_t applied[2];
static void dma2d_set_transfer_ability(int channel,const dma2d_transfer_ability_t *cfg){assert(channel==1||channel==2);applied[channel-1]=*cfg;calls++;}
#include "jpeg_burst_under_test.inc"
int main(void){
    const int blocks[]={1,2,3,1,0};
    for(int mode=0;mode<5;mode++){
        header h={mode};engine e={&h,1,2};calls=0;jpeg_enc_config_dma_trans_ability(&e);
        assert(calls==2&&applied[0].data_burst_length==16&&applied[1].data_burst_length==16);
        assert(applied[0].desc_burst_en&&applied[1].desc_burst_en&&applied[0].mb_size==blocks[mode]&&applied[1].mb_size==0);
    }
    puts("JPEG_BURST_OK actual driver TX/RX 16 byte bursts; all macroblock modes retained");
}
