#pragma once

// TF storage layout v1. Names are relative to the mount point. Writers own
// only their module directory; unused namespaces are not created eagerly.
#define SD_STORAGE_MOUNT "/sdcard"
#define SD_STORAGE_APP_DIR "aibot"
#define SD_STORAGE_GALLERY_DIR SD_STORAGE_APP_DIR "/gallery"
#define SD_STORAGE_PAINTING_DIR SD_STORAGE_GALLERY_DIR "/painting"
#define SD_STORAGE_CALLIGRAPHY_DIR SD_STORAGE_GALLERY_DIR "/calligraphy"
#define SD_STORAGE_PHOTOS_DIR SD_STORAGE_APP_DIR "/photos"
#define SD_STORAGE_DATA_DIR SD_STORAGE_APP_DIR "/data"
#define SD_STORAGE_CACHE_DIR SD_STORAGE_APP_DIR "/cache"
