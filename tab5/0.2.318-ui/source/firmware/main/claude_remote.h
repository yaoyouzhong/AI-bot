#pragma once
#include <stdbool.h>
typedef struct {
    bool busy,has_earlier;unsigned generation;int status,page,total;
    char binding[33],revision[65],title[513],text[1601],error[48];
} claude_remote_state;
bool claude_remote_start(const char *binding,const char *revision,int page);
void claude_remote_get(claude_remote_state *out);
void claude_remote_cancel(void);
