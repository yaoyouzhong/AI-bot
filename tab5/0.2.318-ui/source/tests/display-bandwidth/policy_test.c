#include "display_bandwidth.h"
// Use the pinned SDK register definition, not a duplicate of its bit layout.
#include "soc/icm_sys_struct.h"
#include <stdint.h>
#include <stdio.h>
#include <string.h>

axi_icm_dev_t AXI_ICM;
unsigned test_lock_depth, test_lock_errors;
#define CHECK(test, message) do { if(!(test)) { fprintf(stderr,"FAIL: %s\n",message); return 3; } } while(0)
static const uint32_t field_mask=0x000f0000u;
static const uint32_t arb_seed=0xa5a05a5au, read_seed=0x5a50a5a5u, write_seed=0x12345678u;
static int protected(void) {
    return AXI_ICM.mst_arb_priority_reg0.reg_gdma_mst2_priority==15 &&
           AXI_ICM.mst_arqos_reg0.reg_gdma_mst2_arqos==15;
}
static int other_fields_unchanged(void) {
    return (AXI_ICM.mst_arb_priority_reg0.val&~field_mask)==(arb_seed&~field_mask) &&
           (AXI_ICM.mst_arqos_reg0.val&~field_mask)==(read_seed&~field_mask) &&
           AXI_ICM.mst_awqos_reg0.val==write_seed;
}
int main(void) {
    char status[96];
    AXI_ICM.mst_arb_priority_reg0.val=arb_seed;
    AXI_ICM.mst_arqos_reg0.val=read_seed;
    AXI_ICM.mst_awqos_reg0.val=write_seed;
#ifndef TEST_BASELINE
    display_bandwidth_init();
    CHECK(protected(),"ordinary pages need protection before any photo lease");
    CHECK(other_fields_unchanged(),"startup must preserve other masters and write QoS");
    display_bandwidth_init();
    display_bandwidth_photo_end();
    display_bandwidth_format(status,sizeof status);
    CHECK(strcmp(status,"bw,1,0,0,0,15,15")==0,"idle/init/end must not invent photo leases");
#endif
    display_bandwidth_photo_begin();
    display_bandwidth_photo_begin();
    CHECK(protected(),"a frozen photo must retain scanout priority");
    display_bandwidth_format(status,sizeof status);
    CHECK(strcmp(status,"bw,1,1,1,0,15,15")==0,"duplicate begin must be idempotent");
    display_bandwidth_photo_end();
    CHECK(protected(),"photo release must preserve baseline scanout protection");
    display_bandwidth_photo_end();
    CHECK(other_fields_unchanged(),"release must preserve other masters and write QoS");
    display_bandwidth_format(status,sizeof status);
    CHECK(strcmp(status,"bw,1,0,1,1,15,15")==0,"duplicate end must be idempotent");
    display_bandwidth_photo_begin();
    display_bandwidth_photo_end();
    display_bandwidth_format(status,sizeof status);
    CHECK(strcmp(status,"bw,1,0,2,2,15,15")==0,"a later photo must preserve the same baseline");
#ifndef TEST_BASELINE
    // A diagnostic must report MMIO readback, even if another component changes it.
    AXI_ICM.mst_arb_priority_reg0.reg_gdma_mst2_priority=3;
    AXI_ICM.mst_arqos_reg0.reg_gdma_mst2_arqos=4;
    display_bandwidth_format(status,sizeof status);
    CHECK(strcmp(status,"bw,1,0,2,2,3,4")==0,"diagnostic must read registers rather than cached values");
    display_bandwidth_photo_end();
    CHECK(protected(),"idle release must reassert protection without adding a lease");
    CHECK(other_fields_unchanged(),"reassertion must preserve unrelated register bits");
#endif
    CHECK(test_lock_depth==0 && test_lock_errors==0,"all register access must use balanced critical sections");
    puts("DISPLAY_BANDWIDTH_POLICY_OK (host register policy only; hardware acceptance required)");
    return 0;
}
