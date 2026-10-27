"""Keep CPU-only SDIO stream packet copies out of the internal DMA pool."""
from pathlib import Path
import sys

HELPERS = '''// TAB5_SDIO_RX_PACKET_MEMORY
static void *sdio_rx_packet_alloc(void) {
#if CONFIG_IDF_TARGET_ESP32P4 && CONFIG_SPIRAM && H_SDIO_HOST_RX_MODE == H_SDIO_HOST_STREAMING_MODE
    // DMA has already finished into the aligned stream buffer. This copy is
    // consumed by the network/HCI stack, never submitted to SDMMC DMA.
    return heap_caps_malloc(MAX_SDIO_BUFFER_SIZE, MALLOC_CAP_SPIRAM | MALLOC_CAP_8BIT);
#else
    return sdio_buffer_alloc(MEMSET_REQUIRED);
#endif
}
static void sdio_rx_packet_free(void *buffer) {
#if CONFIG_IDF_TARGET_ESP32P4 && CONFIG_SPIRAM && H_SDIO_HOST_RX_MODE == H_SDIO_HOST_STREAMING_MODE
    heap_caps_free(buffer);
#else
    sdio_buffer_free(buffer);
#endif
}

'''
BODY = '''static esp_err_t sdio_push_data_to_queue(uint8_t * buf, uint32_t buf_len)
{
    // Validate before allocation; malformed packets and allocation pressure
    // fail the receive operation, never assert/reset or leak a packet buffer.
    while (buf_len) {
        uint16_t len = 0, offset = 0;
        if (buf_len < sizeof(struct esp_payload_header) ||
            !is_valid_sdio_rx_packet(buf, &len, &offset)) return ESP_FAIL;
        uint32_t packet_size = (uint32_t)len + offset;
        if (packet_size > buf_len || packet_size > MAX_SDIO_BUFFER_SIZE) return ESP_FAIL;
        uint8_t *packet = sdio_rx_packet_alloc();
        if (!packet) {
            ESP_LOGE(TAG, "SDIO receive packet allocation failed; dropping stream");
            return ESP_ERR_NO_MEM;
        }
        memcpy(packet, buf, packet_size);
        if (sdio_push_pkt_to_queue(packet, len, offset)) {
            sdio_rx_packet_free(packet);
            return ESP_FAIL;
        }
        buf_len -= packet_size;
        buf += packet_size;
    }
    return ESP_OK;
}
'''

def patch(text):
    if '// TAB5_SDIO_RX_PACKET_MEMORY' in text:
        if HELPERS not in text or BODY not in text:
            raise RuntimeError('Unknown patched SDIO receive layout')
        return text
    marker='void transport_deinit_internal(void)'
    begin=text.rindex('static esp_err_t sdio_push_data_to_queue(')
    end=text.index('\n#endif',begin)
    old=text[begin:end]
    if 'assert(pkt_rxbuff);' not in old or text.count('buf_handle.free_buf_handle    = sdio_buffer_free;') != 1:
        raise RuntimeError('Unknown SDIO stream receive layout')
    text=text[:begin]+BODY+text[end:]
    return text.replace(marker,HELPERS+marker,1).replace(
        'buf_handle.free_buf_handle    = sdio_buffer_free;',
        'buf_handle.free_buf_handle    = sdio_rx_packet_free;',1)

if __name__=='__main__':
    path=Path(sys.argv[1])/'managed_components/espressif__esp_hosted/host/drivers/transport/sdio/sdio_drv.c'
    source=path.read_text(encoding='utf-8');updated=patch(source)
    if updated!=source:path.write_text(updated,encoding='utf-8')
    print('TAB5_SDIO_RX_PACKET_MEMORY_OK')
