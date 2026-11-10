#include "tab5_pinyin_data.h"
#include "esp_heap_caps.h"
#include "miniz.h"
#include <stdlib.h>
#include <string.h>
#include "tab5_pinyin_dict.inc"

static tab5_pinyin_data active;
static bool dictionary_decode(const uint8_t *input,size_t length,uint8_t *out,size_t expected) {
    tinfl_decompressor *state=heap_caps_malloc(sizeof(*state),MALLOC_CAP_SPIRAM|MALLOC_CAP_8BIT);
    if(!state)return false;
    tinfl_init(state);size_t consumed=length,produced=expected;
    tinfl_status status=tinfl_decompress(state,input,&consumed,out,out,&produced,
        TINFL_FLAG_PARSE_ZLIB_HEADER|TINFL_FLAG_USING_NON_WRAPPING_OUTPUT_BUF);
    bool ok=status==TINFL_STATUS_DONE&&consumed==length&&produced==expected;
    free(state);return ok;
}
bool tab5_pinyin_data_init(tab5_pinyin_data *out) {
    if(!out)return false;
    if(active.rows){*out=active;return true;}
    uint8_t *bytes=heap_caps_malloc(PINYIN_RAW_BYTES,MALLOC_CAP_SPIRAM|MALLOC_CAP_8BIT);
    if(!bytes)return false;
    if(!dictionary_decode(pinyin_packed,sizeof(pinyin_packed),bytes,PINYIN_RAW_BYTES)){free(bytes);return false;}
    // Publish only a complete, checksummed dictionary. No per-keystroke inflate,
    // allocation, eviction or mutation of text held by independent editors.
    active=(tab5_pinyin_data){(const char *)bytes,(const char *)bytes+PINYIN_KEYS_BYTES,
        (const uint8_t (*)[11])(bytes+PINYIN_KEYS_BYTES+PINYIN_WORDS_BYTES),PINYIN_ROWS,PINYIN_RAW_BYTES,sizeof(pinyin_packed)};
    *out=active;return true;
}
