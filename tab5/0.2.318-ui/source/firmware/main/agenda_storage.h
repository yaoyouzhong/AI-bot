#pragma once
#include "agenda_core.h"
#include <stddef.h>
typedef struct {char id[33],edit[33],base_edit[33];} agenda_deletion;
typedef struct {uint32_t count;agenda_deletion items[AGENDA_LIMIT];} agenda_deletions;
// 16 maximum-length items plus 16 deletions; IDs/times are losslessly packed.
#define AGENDA_STORAGE_MAX 3574
bool agenda_deletions_valid(const agenda_document *document,const agenda_deletions *deleted);
size_t agenda_storage_encode(const agenda_document *document,const agenda_deletions *deleted,uint8_t *out,size_t capacity);
bool agenda_storage_decode(const uint8_t *bytes,size_t size,agenda_document *document,agenda_deletions *deleted);
