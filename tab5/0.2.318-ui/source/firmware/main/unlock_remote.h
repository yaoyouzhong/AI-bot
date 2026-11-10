#pragma once
#include <stdbool.h>
typedef struct {bool busy,finished,unlocked;int error,stage;} unlock_remote_state;
bool unlock_remote_wifi_only(void);
unsigned unlock_remote_connection(void);
bool unlock_remote_start(const char *request_id,unsigned connection);
void unlock_remote_cancel(void);
void unlock_remote_get(unlock_remote_state *out);
