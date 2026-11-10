#include <stdbool.h>
#include <stddef.h>
#include <stdint.h>
uint32_t asset_crc32(const uint8_t *data, size_t size) {
    uint32_t crc = 0xffffffff;
    for (size_t i = 0; i < size; i++) {
        crc ^= data[i];
        for (int bit = 0; bit < 8; bit++)
            crc = (crc >> 1) ^ ((crc & 1) ? 0xedb88320 : 0);
    }
    return ~crc;
}
bool asset_unpack(const uint8_t *data, size_t size, uint8_t *output, size_t capacity) {
    if (size % 4 || capacity % 2)
        return false;
    size_t used = 0;
    for (size_t i = 0; i < size; i += 4) {
        size_t count = data[i] | ((size_t)data[i + 1] << 8);
        if (!count || count > (capacity - used) / 2)
            return false;
        for (size_t n = 0; n < count; n++) {
            output[used++] = data[i + 2];
            output[used++] = data[i + 3];
        }
    }
    return used == capacity;
}
