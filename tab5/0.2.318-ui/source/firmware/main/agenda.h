#pragma once
#include "agenda_core.h"
typedef struct {agenda_document document;uint32_t generation,completed_request;bool ready,busy,pending,last_success;char saved_id[33],message[160];} agenda_state;
void agenda_start(void);
void agenda_get(agenda_state *out);
// Enqueued only; success is reported after the worker has committed to NVS.
uint32_t agenda_action(const char *id,const char *edit,int64_t occurrence,bool complete,int64_t until);
uint32_t agenda_save(const agenda_item *definition,const char *expected_edit);
// Local durable deletion; the worker synchronizes its tombstone after reconnect.
uint32_t agenda_remove(const char *id,const char *edit);
bool agenda_alert(int64_t now,char title[97],char id[33],int64_t *occurrence);
void agenda_offer(const char *revision);
