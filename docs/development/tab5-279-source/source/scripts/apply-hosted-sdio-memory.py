"""Use P4 SDIO streaming DMA-aligned PSRAM instead of scarce internal RAM.

IDF 5.4.2 advertises SOC_SDMMC_PSRAM_DMA_CAPABLE; sdmmc_transaction.c
performs cache synchronization. Packet pools and other targets keep the
vendor allocator. No SDIO mode, clock or C6 firmware change.
"""
from pathlib import Path
import sys

OLD = '\t\t*buf = (uint8_t *)MEM_ALLOC(len);\n\t\tassert(*buf);'
NEW = '''#if CONFIG_IDF_TARGET_ESP32P4 && CONFIG_SPIRAM && SOC_SDMMC_PSRAM_DMA_CAPABLE
		// TAB5: streaming bursts must not consume the internal DMA reserve.
		esp_dma_mem_info_t stream_memory = {
			.extra_heap_caps = MALLOC_CAP_SPIRAM | MALLOC_CAP_8BIT,
			.dma_alignment_bytes = 64,
		};
		void *stream_buffer = NULL;
		size_t stream_size = 0;
		esp_err_t stream_error = esp_dma_capable_malloc(len, &stream_memory, &stream_buffer, &stream_size);
		*buf = stream_error == ESP_OK ? (uint8_t *)stream_buffer : NULL;
#else
		*buf = (uint8_t *)MEM_ALLOC(len);
#endif
		assert(*buf);'''
HEAD = '#include "esp_hosted_transport_init.h"'
INCLUDES = HEAD + '\n#include "soc/soc_caps.h"\n#include "esp_heap_caps.h"\n#include "esp_dma_utils.h"'

def patch(source):
    if source.count(NEW)==1 and source.count(INCLUDES)==1:return source
    if source.count(OLD)!=1 or source.count(HEAD)!=1 or NEW in source or INCLUDES in source:
        raise RuntimeError('Unknown hosted SDIO allocation; inspect dependency before patching')
    return source.replace(HEAD,INCLUDES).replace(OLD,NEW)

if __name__=='__main__':
    p=Path(sys.argv[1])/'managed_components/espressif__esp_hosted/host/drivers/transport/sdio/sdio_drv.c'
    source=p.read_text(encoding='utf-8');updated=patch(source)
    if updated!=source:p.write_text(updated,encoding='utf-8')
    print('TAB5 P4 SDIO streaming DMA-aligned PSRAM patch ready')
