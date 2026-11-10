// Exercise the shipped ESP-IDF FatFs against a sparse, in-memory 128GB disk.
// No physical device is opened or formatted by this test.
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include "ff.h"
#include "diskio.h"
#include "sd_paths.h"

#define SECTOR_COUNT ((LBA_t)(127982895104ULL / 512))
#define BLOCK_SECTORS 256
typedef struct block { LBA_t key; struct block *next; BYTE bytes[BLOCK_SECTORS * 512]; } block;
static block *buckets[4096];
PARTITION VolToPart[FF_VOLUMES] = {{0, 0}, {1, 0}};
static block *find_block(LBA_t sector, int create) {
    LBA_t key = sector / BLOCK_SECTORS;
    block **slot = &buckets[key % 4096];
    while (*slot && (*slot)->key != key) slot = &(*slot)->next;
    if (!*slot && create) { *slot = calloc(1, sizeof(block)); if (!*slot) abort(); (*slot)->key = key; }
    return *slot;
}
DSTATUS disk_initialize(BYTE drive) { return drive ? STA_NODISK : 0; }
DSTATUS disk_status(BYTE drive) { return disk_initialize(drive); }
DRESULT disk_read(BYTE drive, BYTE *buffer, LBA_t sector, UINT count) {
    if (drive || sector >= SECTOR_COUNT || count > SECTOR_COUNT - sector) return RES_PARERR;
    for (UINT i = 0; i < count; i++, sector++) {
        block *item = find_block(sector, 0);
        if (item) memcpy(buffer + i * 512, item->bytes + (sector % BLOCK_SECTORS) * 512, 512);
        else memset(buffer + i * 512, 0, 512);
    }
    return RES_OK;
}
DRESULT disk_write(BYTE drive, const BYTE *buffer, LBA_t sector, UINT count) {
    if (drive || sector >= SECTOR_COUNT || count > SECTOR_COUNT - sector) return RES_PARERR;
    for (UINT i = 0; i < count; i++, sector++) {
        block *item = find_block(sector, 1);
        memcpy(item->bytes + (sector % BLOCK_SECTORS) * 512, buffer + i * 512, 512);
    }
    return RES_OK;
}
DRESULT disk_ioctl(BYTE drive, BYTE command, void *value) {
    if (drive) return RES_PARERR;
    switch (command) {
    case CTRL_SYNC: return RES_OK;
    case GET_SECTOR_COUNT: *(LBA_t *)value = SECTOR_COUNT; return RES_OK;
    case GET_SECTOR_SIZE: *(WORD *)value = 512; return RES_OK;
    case GET_BLOCK_SIZE: *(DWORD *)value = 64; return RES_OK;
    default: return RES_PARERR;
    }
}
void *ff_memalloc(UINT size) { return malloc(size); }
void ff_memfree(void *ptr) { free(ptr); }
int ff_mutex_create(int volume) { (void)volume; return 1; }
void ff_mutex_delete(int volume) { (void)volume; }
int ff_mutex_take(int volume) { (void)volume; return 1; }
void ff_mutex_give(int volume) { (void)volume; }
DWORD get_fattime(void) { return ((2026 - 1980) << 25) | (10 << 21) | (11 << 16); }
static void require(FRESULT result, const char *operation) {
    if (result != FR_OK) { fprintf(stderr, "%s: FRESULT=%d\n", operation, result); exit(1); }
}
#if EXPECT_LFN
static void roundtrip(const char *directory) {
    char work[160], temporary[200], final[200];
    snprintf(work, sizeof(work), "0:/%s/works", directory); require(f_mkdir(work), work);
    snprintf(work, sizeof(work), "0:/%s/works/2026-01-01", directory); require(f_mkdir(work), work);
    snprintf(temporary, sizeof(temporary), "%s/00-l.0.aig.part", work);
    snprintf(final, sizeof(final), "%s/00-l.0.aig", work);
    BYTE payload[8191], readback[8191];
    for (UINT i = 0; i < sizeof(payload); i++) payload[i] = (BYTE)(i * 37);
    FIL file; UINT transferred;
    require(f_open(&file, temporary, FA_CREATE_NEW | FA_WRITE), temporary);
    require(f_write(&file, payload, sizeof(payload), &transferred), "write");
    if (transferred != sizeof(payload)) exit(1);
    require(f_sync(&file), "sync"); require(f_close(&file), "close");
    require(f_rename(temporary, final), "atomic rename");
    FATFS fs;
    require(f_mount(NULL, "0:", 0), "unmount"); require(f_mount(&fs, "0:", 1), "remount");
    require(f_open(&file, final, FA_READ), final);
    require(f_read(&file, readback, sizeof(readback), &transferred), "read");
    if (transferred != sizeof(payload) || memcmp(payload, readback, sizeof(payload))) exit(1);
    require(f_close(&file), "read close");
    FILINFO info; require(f_stat(final, &info), "stat");
    if (strcmp(info.fname, "00-l.0.aig")) exit(1);
    if (f_stat(temporary, &info) != FR_NO_FILE) exit(1);
    require(f_mount(NULL, "0:", 0), "end unmount");
}
#endif
int main(void) {
    BYTE buffer[32768]; FATFS fs;
    MKFS_PARM options = {FM_FAT32 | FM_SFD, 2, 0, 0, 32768};
    require(f_mkfs("0:", &options, buffer, sizeof(buffer)), "format test memory disk");
    require(f_mount(&fs, "0:", 1), "mount");
    if (fs.fs_type != FS_FAT32 || fs.csize != 64) exit(1);
#if FF_MAX_SS != FF_MIN_SS
    if (fs.ssize != 512) exit(1);
#endif
    require(f_mkdir("0:/" SD_STORAGE_APP_DIR), "app root");
    require(f_mkdir("0:/" SD_STORAGE_GALLERY_DIR), "gallery");
    FRESULT result = f_mkdir("0:/" SD_STORAGE_CALLIGRAPHY_DIR);
#if EXPECT_LFN
    require(result, "calligraphy directory");
    require(f_mkdir("0:/" SD_STORAGE_PAINTING_DIR), "painting directory");
    roundtrip(SD_STORAGE_CALLIGRAPHY_DIR);
    require(f_mount(&fs, "0:", 1), "mount second category");
    roundtrip(SD_STORAGE_PAINTING_DIR);
    puts("FATFS_PATHS_OK 128GB FAT32 512-byte sectors 32KiB clusters; both categories, date, part, rename, remount, exact bytes");
#else
    if (result != FR_INVALID_NAME) { fprintf(stderr, "Expected INVALID_NAME, got %d\n", result); return 1; }
    puts("FATFS_LEGACY_FAILURE_REPRODUCED calligraphy directory FR_INVALID_NAME");
#endif
    return 0;
}
