#pragma once
#include <stdbool.h>
#include "sd_paths.h"
// All TF-card users share one mount and serialize filesystem access. No format.
void sd_storage_init(void);
void sd_storage_start(void); // Background detection; start after board power/I2C initialization.
bool sd_storage_available(void); // Nonblocking hint; mounting happens in workers.
bool sd_storage_lock(void);
void sd_storage_unlock(bool media_error);
int sd_storage_state(void); // 0 unknown, 1 mounted, -1 unavailable/retrying.
