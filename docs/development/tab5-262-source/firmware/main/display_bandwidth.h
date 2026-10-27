#pragma once

// The successfully frozen still viewport owns this lease across photo
// preparation and upload. Release only after its unfreeze succeeds.
void display_bandwidth_photo_begin(void);
void display_bandwidth_photo_end(void);
void display_bandwidth_format(char *out, unsigned size);
