#pragma once
#include "cJSON.h"
#include "link_state.h"
// Takes ownership only on success. Runs network work outside the LVGL task.
bool display_settings_report(cJSON *report,link_kind link);
