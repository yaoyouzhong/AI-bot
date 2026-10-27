#include "display_bandwidth.h"
#include "soc/icm_sys_struct.h"
#include "hal/dw_gdma_ll.h"
#include "freertos/FreeRTOS.h"
#include <stdbool.h>
#include <stdio.h>

// The pinned DPI driver reads PSRAM through DW-GDMA memory master 1
// (axi_icm_ll_set_dw_gdma_qos_arbiter_prio maps it to gdma_mst2).
// DW channel priority only arbitrates inside DW-GDMA. ARQOS arbitrates
// against CPU/cache and other masters at the system interconnect; higher
// ARQOS values have higher priority (ESP32-P4 TRM, AXI Matrix).
_Static_assert(DW_GDMA_LL_MASTER_PORT_MEMORY == 1, "Recheck LCD memory master");
static portMUX_TYPE bandwidth_lock=portMUX_INITIALIZER_UNLOCKED;
static bool active;
static unsigned saved_priority,saved_read_qos,leases,releases,applied_priority,applied_read_qos;

void display_bandwidth_photo_begin(void) {
    portENTER_CRITICAL(&bandwidth_lock);
    if(!active) {
        saved_priority=AXI_ICM.mst_arb_priority_reg0.reg_gdma_mst2_priority;
        saved_read_qos=AXI_ICM.mst_arqos_reg0.reg_gdma_mst2_arqos;
        // Give the deadline-bound scanout precedence while the still is
        // prepared, encoded, encrypted and uploaded. The still viewport owns this
        // lease until unfreeze succeeds, including failures and retries.
        // Leave every other master untouched.
        AXI_ICM.mst_arb_priority_reg0.reg_gdma_mst2_priority=15;
        AXI_ICM.mst_arqos_reg0.reg_gdma_mst2_arqos=15;
        applied_priority=AXI_ICM.mst_arb_priority_reg0.reg_gdma_mst2_priority;
        applied_read_qos=AXI_ICM.mst_arqos_reg0.reg_gdma_mst2_arqos;
        active=true;leases++;
    }
    portEXIT_CRITICAL(&bandwidth_lock);
}

void display_bandwidth_photo_end(void) {
    portENTER_CRITICAL(&bandwidth_lock);
    if(active) {
        AXI_ICM.mst_arb_priority_reg0.reg_gdma_mst2_priority=saved_priority;
        AXI_ICM.mst_arqos_reg0.reg_gdma_mst2_arqos=saved_read_qos;
        active=false;releases++;
    }
    portEXIT_CRITICAL(&bandwidth_lock);
}

void display_bandwidth_format(char *out,unsigned size) {
    unsigned a,l,r,p,q;
    portENTER_CRITICAL(&bandwidth_lock);
    a=active;l=leases;r=releases;p=applied_priority;q=applied_read_qos;
    portEXIT_CRITICAL(&bandwidth_lock);
    snprintf(out,size,"bw,1,%u,%u,%u,%u,%u",a,l,r,p,q);
}
