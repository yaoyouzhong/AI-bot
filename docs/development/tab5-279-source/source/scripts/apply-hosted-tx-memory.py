"""Keep P4 SDIO packet pools in DMA-aligned PSRAM; report TX pressure.

The 0.2.27 crash at transport_drv_sta_tx(copy_buff) proves that moving RX
stream buffers alone was insufficient. Preserve pool ownership/reuse and
the IDF SDMMC cache synchronization path. Other transports/targets retain
their vendor allocator. Fail closed when the dependency layout changes.
"""
from pathlib import Path
import sys

HELPER = '''// TAB5_SDIO_PACKET_POOL_MEMORY
static void *tab5_packet_pool_alloc(size_t size) {
#if CONFIG_IDF_TARGET_ESP32P4 && CONFIG_SPIRAM && CONFIG_ESP_HOSTED_SDIO_HOST_INTERFACE && SOC_SDMMC_PSRAM_DMA_CAPABLE
    esp_dma_mem_info_t memory = {
        .extra_heap_caps = MALLOC_CAP_SPIRAM | MALLOC_CAP_8BIT,
        .dma_alignment_bytes = 64,
    };
    void *buffer = NULL;
    return esp_dma_capable_malloc(size, &memory, &buffer, NULL) == ESP_OK ? buffer : NULL;
#else
    return MEM_ALLOC(size);
#endif
}
// TAB5_SDIO_PACKET_POOL_MEMORY_END

'''

GUARD = '''\t// TAB5_TX_PRESSURE: let lwIP retry instead of aborting the device.
\tif (len > MAX_TRANSPORT_BUFFER_SIZE - H_ESP_PAYLOAD_HEADER_OFFSET)
\t\treturn ESP_ERR_INVALID_ARG;
'''

def pool_patch(text):
    if '// TAB5_SDIO_PACKET_POOL_MEMORY' in text:
        if HELPER not in text or 'buf = tab5_packet_pool_alloc(MEMPOOL_ALIGNED(mp->block_size));' not in text:
            raise RuntimeError('Unknown patched packet pool')
        return text
    old='buf = MEM_ALLOC(MEMPOOL_ALIGNED(mp->block_size));'
    head='#include "esp_hosted_config.h"'
    if text.count(old)!=1 or text.count(head)!=1:
        raise RuntimeError('Unknown packet pool layout')
    return text.replace(head,head+'\n#include "soc/soc_caps.h"\n#include "esp_dma_utils.h"\n#include "esp_heap_caps.h"\n'+HELPER,1).replace(old,'buf = tab5_packet_pool_alloc(MEMPOOL_ALIGNED(mp->block_size));',1)

def transport_patch(text):
    if '// TAB5_TX_PRESSURE' in text:
        if text.count(GUARD)!=2 or text.count('if (!copy_buff) return ESP_ERR_NO_MEM;')!=2:
            raise RuntimeError('Unknown patched TX pressure handling')
        return text
    old='\t/*  Prepare transport buffer directly consumable */'
    if text.count(old)!=2 or text.count('\tassert(copy_buff);')!=2:
        raise RuntimeError('Unknown transport TX layout')
    return text.replace(old,GUARD+old).replace('\tassert(copy_buff);','\tif (!copy_buff) return ESP_ERR_NO_MEM;')

if __name__=='__main__':
    base=Path(sys.argv[1])/'managed_components/espressif__esp_hosted/host/drivers'
    for rel,patch in [('mempool/mempool.c',pool_patch),('transport/transport_drv.c',transport_patch)]:
        path=base/rel;source=path.read_text(encoding='utf-8');updated=patch(source)
        if updated!=source:path.write_text(updated,encoding='utf-8')
    print('TAB5_SDIO_PACKET_POOL_MEMORY_OK')
