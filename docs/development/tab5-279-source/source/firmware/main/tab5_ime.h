#pragma once
#include "lvgl.h"
#include <stdbool.h>
#include <stddef.h>
#include <stdint.h>

#define TAB5_PINYIN_MAX 32
#define TAB5_PINYIN_CANDIDATES 48
typedef struct { const char *text; uint8_t consumed; uint32_t weight; } tab5_pinyin_candidate;
size_t tab5_pinyin_lookup(const char *raw, tab5_pinyin_candidate *out, size_t capacity);

/* Independent composers; legacy helpers address the first (Codex) composer. */
lv_obj_t *tab5_ime_create(lv_obj_t *parent, lv_obj_t *keyboard, lv_obj_t *textarea, const lv_font_t *font);
void tab5_ime_show(bool show);
void tab5_ime_finish(void); /* Preserve unselected spelling as literal text. */
const char *tab5_ime_pending(void);
void tab5_ime_show_for(lv_obj_t *panel,bool show);
void tab5_ime_finish_for(lv_obj_t *panel);
const char *tab5_ime_pending_for(lv_obj_t *panel);
void tab5_ime_reset_for(lv_obj_t *panel); /* Discard only this editor's uncommitted spelling. */
