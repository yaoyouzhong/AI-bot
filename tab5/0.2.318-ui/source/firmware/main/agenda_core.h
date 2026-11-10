#pragma once
#include <stdbool.h>
#include <stdint.h>
#include "cJSON.h"
#define AGENDA_LIMIT 16
typedef struct {
    char id[33],edit[33],title[97],base_edit[33];
    int64_t due,done,snooze_occurrence,snooze_until,alerted,end;
    uint32_t repeat,lead,dirty;
} agenda_item;
typedef struct {uint32_t version,count;char revision[33];agenda_item items[AGENDA_LIMIT];} agenda_document;
typedef struct {int64_t occurrence,alert_at;bool complete,calendar_pending;} agenda_view;
typedef struct {uint32_t year;uint8_t working[46];} agenda_workday_year;
typedef struct {uint32_t version,count;agenda_workday_year years[3];} agenda_workday_calendar;
bool agenda_workdays_parse(const cJSON *years,agenda_workday_calendar *out);
bool agenda_workdays_valid(const agenda_workday_calendar *value);
void agenda_workdays_set(const agenda_workday_calendar *value);
int agenda_workday_at(int64_t occurrence); // -1: official annual schedule unavailable
bool agenda_occurrence_valid(const agenda_item *item,int64_t occurrence);
agenda_view agenda_view_at(const agenda_item *item,int64_t now);
int64_t agenda_ends_at(const agenda_item *item,int64_t occurrence);
bool agenda_expired(const agenda_item *item,int64_t now);
int agenda_oldest_expired(const agenda_document *document,int64_t now);
bool agenda_parse(const cJSON *root,agenda_document *out);
bool agenda_document_valid(const agenda_document *value);
bool agenda_apply(agenda_item *item,int64_t occurrence,bool complete,int64_t until);
cJSON *agenda_item_json(const agenda_item *item);
