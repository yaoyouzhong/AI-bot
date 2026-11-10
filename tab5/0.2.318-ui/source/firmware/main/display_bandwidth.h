#pragma once

// Protect deadline-bound LCD scanout before starting the display. Photo
// viewport leases never remove this baseline on unfreeze.
void display_bandwidth_init(void);
// The frozen viewport retains its lease across photo preparation/upload.
void display_bandwidth_photo_begin(void);
void display_bandwidth_photo_end(void);
void display_bandwidth_format(char *out, unsigned size);
