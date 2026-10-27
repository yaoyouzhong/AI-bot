#pragma once
#include "lvgl.h"
// Render public assistant text. No tool execution, links, or HTML interpretation.
void reply_document_set(lv_obj_t *parent, const char *text);
