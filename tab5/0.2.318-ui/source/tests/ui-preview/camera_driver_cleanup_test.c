// Exercise the pinned driver's real destructor for partial constructor states.
#include <assert.h>
#include <stdbool.h>
#include <stdio.h>
#define CONFIG_CAM_CTLR_MIPI_CSI_ISR_CACHE_SAFE 0
#define CONFIG_DW_GDMA_ISR_IRAM_SAFE 1
#define MALLOC_CAP_INTERNAL 2048
#define MALLOC_CAP_8BIT 4
#define MALLOC_CAP_DEFAULT 4096
#define SOC_ISP_SHARE_CSI_BRG 1
#define CSI_FSM_INIT 1
#define ESP_OK 0
#define ESP_ERR_INVALID_ARG 1
#define ESP_ERR_INVALID_STATE 2
#define PERIPH_RCC_ATOMIC()
#define ESP_RETURN_ON_FALSE(cond,err,...) do{if(!(cond))return (err);}while(0)
#define ESP_RETURN_ON_ERROR(call,...) do{int r=(call);if(r)return r;}while(0)
typedef int esp_err_t;
typedef struct {
    int csi_fsm,csi_id,csi_brg_id;
    bool csi_brg_in_use,bk_buffer_dis;
    void *dma_chan,*trans_que,*backup_buffer;
} csi_controller_t;
static unsigned dma_deletes,controller_releases,bridge_releases,queue_deletes,frees;
static int dw_gdma_del_channel(void *p){assert(p);dma_deletes++;return 0;}
static int s_csi_declaim_controller(csi_controller_t *p){assert(p);controller_releases++;return 0;}
static int mipi_csi_brg_declaim(int id){assert(id==0);bridge_releases++;return 0;}
static void mipi_csi_ll_enable_phy_config_clock(int id,int value){assert(id==0&&value==0);}
static void vQueueDeleteWithCaps(void *p){assert(p);queue_deletes++;}
static void fake_free(void *p){if(p)frees++;}
#define free fake_free
#include "camera_driver_cleanup_under_test.inc"
_Static_assert((CSI_MEM_ALLOC_CAPS&MALLOC_CAP_INTERNAL)!=0,"DMA ISR context must be internal");
_Static_assert((CSI_MEM_ALLOC_CAPS&MALLOC_CAP_DEFAULT)==0,"DEFAULT must not bypass DMA reserve");
int main(void){
    for(unsigned stage=0;stage<5;stage++){
        csi_controller_t c={.csi_fsm=CSI_FSM_INIT};
        if(stage>=1)c.trans_que=(void*)1;
        if(stage>=2)c.csi_brg_in_use=true;
        if(stage>=3)c.backup_buffer=(void*)2;
        if(stage>=4)c.dma_chan=(void*)3;
        dma_deletes=controller_releases=bridge_releases=queue_deletes=frees=0;
        assert(s_del_csi_ctlr(&c)==ESP_OK);
        assert(controller_releases==1);
        assert(queue_deletes==(stage>=1));
        assert(bridge_releases==(stage>=2));
        assert(frees==1+(stage>=3));
        assert(dma_deletes==(stage>=4));
    }
    assert(s_del_csi_ctlr(NULL)==ESP_ERR_INVALID_ARG);
    csi_controller_t running={.csi_fsm=3};
    assert(s_del_csi_ctlr(&running)==ESP_ERR_INVALID_STATE);
    puts("CAMERA_DRIVER_CLEANUP_OK: five partial-init stages release resources; active controller protected; DMA context internal");
}
