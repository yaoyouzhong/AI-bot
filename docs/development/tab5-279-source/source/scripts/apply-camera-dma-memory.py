"""Adapt pinned IDF 5.4.2 CSI ownership to the shared DW-GDMA ISR contract.

Keep large frame buffers in PSRAM. Only the ISR controller context must be
internal. Initialize cleanup state before any fallible allocation after claim.
"""
from pathlib import Path
import sys

CHANGES = [
    ('#if CONFIG_CAM_CTLR_MIPI_CSI_ISR_CACHE_SAFE\n#define CSI_MEM_ALLOC_CAPS',
     '#if CONFIG_CAM_CTLR_MIPI_CSI_ISR_CACHE_SAFE || CONFIG_DW_GDMA_ISR_IRAM_SAFE\n#define CSI_MEM_ALLOC_CAPS'),
    ('    ESP_LOGD(TAG, "config->queue_items: %d", config->queue_items);',
     '    // TAB5: constructor failures must release the claimed CSI controller.\n'
     '    ctlr->csi_fsm = CSI_FSM_INIT;\n'
     '    ESP_LOGD(TAG, "config->queue_items: %d", config->queue_items);'),
    ('    vQueueDeleteWithCaps(ctlr->trans_que);',
     '    if (ctlr->trans_que) vQueueDeleteWithCaps(ctlr->trans_que);'),
]

def patch(source: str) -> str:
    for old, new in CHANGES:
        if source.count(new) == 1:
            continue
        if source.count(old) != 1:
            raise RuntimeError('Unknown CSI driver source; inspect pinned SDK before patching')
        source = source.replace(old, new)
    return source

if __name__ == '__main__':
    path = Path(sys.argv[1]) / 'components/esp_driver_cam/csi/src/esp_cam_ctlr_csi.c'
    source = path.read_text(encoding='utf-8')
    updated = patch(source)
    if updated != source:
        path.write_text(updated, encoding='utf-8')
    print('CAMERA_DMA_MEMORY_PATCH_READY: internal ISR context and partial-init cleanup')
