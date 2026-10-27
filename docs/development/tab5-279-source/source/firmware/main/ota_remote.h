#pragma once
#include "cJSON.h"
#include "link_state.h"
#include <stdbool.h>
// Report ownership transfers only on success. Results are consumed by LVGL.
bool ota_remote_report(cJSON *report,link_kind link);
bool ota_remote_result(char request[33],bool *commit,bool *confirmed);

// Read-only authenticated boot report; no installation or input actions.
bool boot_verify_report(const char *request,const char *session,link_kind link);
