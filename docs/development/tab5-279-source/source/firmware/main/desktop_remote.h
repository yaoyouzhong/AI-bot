#pragma once
#include <stdbool.h>
#include <stddef.h>
typedef struct {
    bool busy,appended,draft_empty;
    int status;
    char task_id[37],project_id[17],title[241],folder[121],message[160];
    double updated;
} desktop_remote_state;
bool desktop_remote_open(const char *request_id,const char *task_id);
bool desktop_remote_latest(const char *request_id);
bool desktop_remote_stage(const char *request_id,const char *task_id,const char *text);
bool desktop_remote_submit(const char *request_id,const char *task_id,const char *draft_request_id);
bool desktop_remote_clear(const char *request_id,const char *task_id);
void desktop_remote_get(desktop_remote_state *out);
