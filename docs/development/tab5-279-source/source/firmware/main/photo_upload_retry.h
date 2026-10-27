// Reconstructed independently from AI-bot main's documented imageChunkRetry
// contract and photo_upload_chunks_test.c; the archived header was missing.
#pragma once

#define PHOTO_BLE_RETRIES 1
#define PHOTO_BLE_RECOVERY_MS 20000

static bool photo_ble_current(unsigned host, int mode, const char *session) {
    if (!session || !session[0] || device_host_changing() ||
        host != device_host_epoch() || mode != device_get_config().mode) return false;
    xSemaphoreTake(device_mutex, portMAX_DELAY);
    bool same = !strcmp(device_links.session[LINK_BLE], session);
    xSemaphoreGive(device_mutex);
    return same && !device_host_changing() && host == device_host_epoch();
}

static bool photo_ble_retry_supported(void) {
    xSemaphoreTake(device_mutex, portMAX_DELAY);
    cJSON *cap = cJSON_GetObjectItemCaseSensitive(device_snapshots[LINK_BLE], "imageChunkRetry");
    bool supported = cJSON_IsNumber(cap) && cap->valuedouble == 1;
    xSemaphoreGive(device_mutex);
    return supported;
}

static bool photo_ble_wait(unsigned host, int mode, const char *session) {
    int64_t until = esp_timer_get_time() / 1000 + PHOTO_BLE_RECOVERY_MS;
    while (photo_ble_current(host, mode, session)) {
        if (esp_timer_get_time() / 1000 >= until) break;
        if (ble_connected && ble_host_authorized() &&
            rpc_image_link_select(session) == LINK_BLE &&
            photo_ble_current(host, mode, session)) return true;
        vTaskDelay(pdMS_TO_TICKS(50));
    }
    return false;
}
