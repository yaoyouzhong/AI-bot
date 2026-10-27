#pragma once
#include <stdbool.h>
#include <stddef.h>

// Call only for a status frame already accepted by device_receive().
void ota_update_consider(const char *authenticated_frame);
void ota_update_status(char *out, size_t size);
void ota_update_target_version(char *out, size_t size);
void ota_update_offer_details(char *version,size_t version_size,char *notes,size_t notes_size);
// Successful flash-write progress, capped at 99 until all checks pass.
int ota_update_progress(void);
bool ota_update_available(void);
bool ota_update_busy(void);
bool ota_update_completed(void);
bool ota_update_verifying(void);
// UI acknowledges after the completion screen has been rendered.
void ota_update_completion_presented(void);
// UI acknowledges after the preparation overlay has been rendered.
void ota_update_preparation_presented(void);
// prepareMs,bufferSlots,lcdUnderrunsDuringUpgrade,busy (current boot).
void ota_update_diagnostics(char *out,size_t size);
bool ota_update_request(void);

bool ota_update_offer_matches(const char *offer_id,const char *sha);

// Atomically retain the exact desktop-authorized offer; NULL/NULL is manual UI.
bool ota_update_request_checked(const char *offer_id,const char *sha);
