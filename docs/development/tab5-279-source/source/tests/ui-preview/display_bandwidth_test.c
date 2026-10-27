#undef NDEBUG
#include <assert.h>
#include <stdbool.h>
#include <stdint.h>
#include <stdio.h>
#include <string.h>
// Compile production lease against real pinned register definitions.
#include "soc/icm_sys_struct.h"
#define DW_GDMA_LL_MASTER_PORT_MEMORY 1
typedef int portMUX_TYPE;
#define portMUX_INITIALIZER_UNLOCKED 0
static bool locked;
#define portENTER_CRITICAL(p) do{assert(!locked);locked=true;(void)(p);}while(0)
#define portEXIT_CRITICAL(p) do{assert(locked);locked=false;(void)(p);}while(0)
axi_icm_dev_t AXI_ICM;
#include "display_bandwidth_under_test.inc"
int main(void) {
    AXI_ICM.mst_arb_priority_reg0.val=0x87654321;
    AXI_ICM.mst_arqos_reg0.val=0x12345678;
    AXI_ICM.mst_awqos_reg0.val=0xabcdef01;
    for(unsigned n=0;n<128;n++) {
        display_bandwidth_photo_end();
        display_bandwidth_photo_begin();display_bandwidth_photo_begin();
        assert(AXI_ICM.mst_arb_priority_reg0.val==0x876f4321);
        assert(AXI_ICM.mst_arqos_reg0.val==0x123f5678);
        assert(AXI_ICM.mst_awqos_reg0.val==0xabcdef01);
        display_bandwidth_photo_end();display_bandwidth_photo_end();
        assert(AXI_ICM.mst_arb_priority_reg0.val==0x87654321);
        assert(AXI_ICM.mst_arqos_reg0.val==0x12345678);
    }
    char text[80];display_bandwidth_format(text,sizeof text);
    assert(!strcmp(text,"bw,1,0,128,128,15,15"));
    assert(!locked);
    puts("DISPLAY_BANDWIDTH_OK actual registers; 128 leases; restore nonzero prior values; other masters and writes unchanged");
}
