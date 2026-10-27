#pragma once
#include <stdbool.h>
#include <stdint.h>
#include <stddef.h>
#include "cJSON.h"

bool codex_remote_submit(const char *task_id, const char *message, const char *session, int64_t issued_at);
bool codex_remote_read(const char *task_id,const char *session,int64_t issued_at);
bool codex_remote_busy(void);
bool codex_remote_read_page(const char *task_id,const char *session,int64_t issued_at,int page);
int codex_remote_status(const char *task_id, char *out, unsigned size);
void codex_remote_upload_diagnostics(char *out,unsigned size);
bool codex_remote_request(const char *op,const char *task_id,const char *message,const char *session,int64_t epoch,const char *request_id,const char *expected_turn);
bool codex_remote_with_images(const char *op,const char *task_id,const char *message,const char *session,int64_t epoch,const char *request_id,const char *expected_turn,const char images[3][37]);
bool codex_remote_upload(const char *task_id,const char *session,int64_t epoch,const char *image_id,const uint8_t *data,size_t size);

bool codex_remote_read_turn(const char *task_id,const char *session,int64_t epoch,int page,const char *turn_id,int turn_offset);

void codex_remote_read_result(char *out,unsigned size);
cJSON *codex_remote_take_read_view(void);
