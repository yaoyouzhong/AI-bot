"""Preserve blob versions during failed writes and interrupted NVS updates.

Pinned ESP-IDF 5.4.2: same-key chunks have distinct chunkIndex identities.
The real-partition replay exercises rollback, GC and every flash-operation cut.
Keep the SDK format, CRC, garbage collector and public API unchanged.
"""
from pathlib import Path
import sys

PAGE_OLD = 'findItem(item.nsIndex, item.datatype, item.key, findItemIndex, dupItem)'
PAGE_NEW = 'findItem(item.nsIndex, item.datatype, item.key, findItemIndex, dupItem, item.chunkIndex)'
TRACK = '''            UsedPageNode* node = new (std::nothrow) UsedPageNode();
            if (!node) {
                err = ESP_ERR_NO_MEM;
                break;
            }
            node->mPage = &page;
            usedPages.push_back(node);
'''
CLEANUP_OLD = '''        /* Anything failed, then we should erase all the written chunks*/
        int ii=0;
        for (auto it = std::begin(usedPages); it != std::end(usedPages); it++) {
            it->mPage->eraseItem(nsIndex, ItemType::BLOB_DATA, key, ii++);
        }'''
CLEANUP_NEW = '''        // TAB5: reclaim only this failed version; GC may have moved its chunks.
        const uint16_t firstChunk = static_cast<uint8_t>(chunkStart);
        for (uint16_t chunk = firstChunk; chunk < firstChunk + chunkCount; ++chunk) {
            for (auto page = mPageManager.begin(); page != mPageManager.end(); ++page) {
                if (page->eraseItem(nsIndex, ItemType::BLOB_DATA, key,
                        static_cast<uint8_t>(chunk)) == ESP_OK) break;
            }
        }'''

def transform_page(source: str) -> str:
    if source.count(PAGE_NEW) == 1 and PAGE_OLD not in source:
        return source
    if source.count(PAGE_OLD) != 1 or PAGE_NEW in source:
        raise RuntimeError('Unknown NVS last-item duplicate check')
    return source.replace(PAGE_OLD, PAGE_NEW)

def transform_storage(source: str) -> str:
    if source.count(CLEANUP_NEW) == 1 and CLEANUP_OLD not in source and TRACK not in source:
        return source
    if source.count(CLEANUP_OLD) != 1 or source.count(TRACK) != 1 or source.count('    TUsedPageList usedPages;\n') != 1:
        raise RuntimeError('Unknown NVS failed-blob cleanup')
    return (source.replace(CLEANUP_OLD, CLEANUP_NEW).replace(TRACK, '')
            .replace('    TUsedPageList usedPages;\n', '').replace('    usedPages.clearAndFreeNodes();\n', ''))

def patch(root: Path) -> None:
    updates = []
    for name, transform in [('nvs_page.cpp', transform_page), ('nvs_storage.cpp', transform_storage)]:
        path = root / 'components/nvs_flash/src' / name
        original = path.read_text(encoding='utf-8')
        updates.append((path, original, transform(original)))
    for path, original, updated in updates:
        if original != updated:
            path.write_text(updated, encoding='utf-8')

if __name__ == '__main__':
    patch(Path(sys.argv[1]))
    print('TAB5 NVS blob version identity and GC-safe rollback patches ready')
