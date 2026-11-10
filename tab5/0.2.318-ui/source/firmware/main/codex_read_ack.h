#pragma once
// Called only after the matching public reply has rendered in the open view.
void codex_read_acknowledge(const char *client,const char *task,const char *turn,const char *receipt,const char *session);
