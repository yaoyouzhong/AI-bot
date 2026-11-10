"""P4 Hosted CPU staging and ATT resource-failure ownership, pinned sources."""
from pathlib import Path
import sys

HELPER = '''// TAB5_HOSTED_HCI_STAGING
static void *tab5_hci_staging_alloc(size_t size) {
#if CONFIG_IDF_TARGET_ESP32P4 && CONFIG_SPIRAM && CONFIG_ESP_HOSTED_SDIO_HOST_INTERFACE
    // NO_ZEROCOPY is CPU-copied into SDIO's own DMA pool later.
    return heap_caps_malloc(size, MALLOC_CAP_SPIRAM | MALLOC_CAP_8BIT);
#else
    return MEM_ALLOC(size);
#endif
}
// TAB5_HOSTED_HCI_STAGING_END

'''
ATT_OLD = '''    rc = ble_l2cap_tx(conn, chan, txom);
    assert(rc == 0);
    return rc;'''
ATT_NEW = '''    rc = ble_l2cap_tx(conn, chan, txom);
#if CONFIG_IDF_TARGET_ESP32P4 && CONFIG_ESP_HOSTED_ENABLED
    // TAB5_ATT_TX_FAILURE: L2CAP consumes txom on every outcome. Resource or
    // Hosted transport failure is an error for the caller, not an invariant.
    // Undo the busy flag set for this failed request, not another request.
    if (rc != 0) {
        if (tab5_att_request) conn->client_att_busy = false;
        BLE_HS_LOG(ERROR, "ATT TX failed; rc=%d\\n", rc);
    }
#else
    assert(rc == 0);
#endif
    return rc;'''

def patch_att(text):
    if '// TAB5_ATT_TX_FAILURE' in text:
        if text.count(ATT_NEW) != 1 or text.count('bool tab5_att_request = ble_att_is_request_op(txom->om_data[0]);') != 1:
            raise RuntimeError('Unknown patched ATT transmit layout')
        return text
    request = '    if (ble_att_is_request_op(txom->om_data[0])) {'
    if text.count(ATT_OLD) != 1 or text.count(request) != 1:
        raise RuntimeError('Unknown pinned NimBLE ATT transmit layout')
    return text.replace(request, '    bool tab5_att_request = ble_att_is_request_op(txom->om_data[0]);\n    if (tab5_att_request) {', 1).replace(ATT_OLD, ATT_NEW, 1)

def patch_hosted(text):
    if '// TAB5_HOSTED_HCI_STAGING' in text:
        if HELPER not in text or text.count('data = tab5_hci_staging_alloc(') != 2 or 'free(data); // TAB5_HCI_FLATTEN_FAILURE' not in text:
            raise RuntimeError('Unknown patched Hosted HCI staging layout')
        return text
    anchor = '#define BLE_HCI_CMD_HDR_LEN                 (3)\n'
    flatten_error = '\t\tESP_LOGE(TAG, "Tx: Error copying HCI_H4_ACL data %d", res);\n'
    if text.count(anchor) != 1 or text.count('data = MEM_ALLOC(') != 2 or text.count(flatten_error) != 1:
        raise RuntimeError('Unknown pinned Hosted HCI layout')
    text = text.replace('#include "os_wrapper.h"', '#include "os_wrapper.h"\n#include "esp_heap_caps.h"', 1)
    text = text.replace(anchor, anchor + HELPER, 1).replace('data = MEM_ALLOC(', 'data = tab5_hci_staging_alloc(')
    # esp_hosted_tx consumes data even when rejecting it. Flatten fails before it.
    return text.replace(flatten_error, flatten_error + '\t\tfree(data); // TAB5_HCI_FLATTEN_FAILURE\n', 1)

def apply(idf, project):
    updates = []
    for path, transform in [
        (idf / 'components/bt/host/nimble/nimble/nimble/host/src/ble_att_cmd.c', patch_att),
        (project / 'managed_components/espressif__esp_hosted/host/drivers/bt/vhci_drv.c', patch_hosted),
    ]:
        before = path.read_text('utf-8')
        updates.append((path, before, transform(before)))
    # Validate all pinned layouts before touching either dependency.
    for path, before, after in updates:
        if before != after:
            path.write_text(after, 'utf-8', newline='\n')

if __name__ == '__main__':
    apply(Path(sys.argv[1]), Path(sys.argv[2]))
    print('TAB5_BLE_TX_SAFETY_OK P4_Hosted_CPU_staging ATT_error_ownership')
