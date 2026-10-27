#pragma once
#include "cJSON.h"
#ifdef TAB5_UI_PREVIEW
static inline void wifi_submission_test_poll(cJSON *health){(void)health;}
#else
void wifi_submission_test_poll(cJSON *health);
#endif
