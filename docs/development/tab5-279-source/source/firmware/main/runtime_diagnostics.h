#pragma once
#include "cJSON.h"
#include "link_state.h"
#include <stdbool.h>
// Numeric runtime state only: never photo/audio/text, keys or Wi-Fi credentials.
cJSON *runtime_diagnostics_snapshot(void);
bool runtime_diagnostics_report(const char *request,const char *session,link_kind link);
