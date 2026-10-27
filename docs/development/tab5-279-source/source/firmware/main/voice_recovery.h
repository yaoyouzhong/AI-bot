#pragma once
#include <stdbool.h>
#include <stdint.h>
#include "cJSON.h"

/* Recover one existing take only. No start, audio resend, transport switching or
 * automatic send. The UI keeps its original generation/task/cursor binding. */
typedef struct {
    void *context;
    int64_t (*now)(void *);
    void (*wait)(void *,unsigned);
    bool (*cancelled)(void *);
    bool (*ready)(void *);
    cJSON *(*request)(void *,const char *operation,const char *voice_id);
    void (*publish)(void *,const char *phase,const char *message,const char *text);
} voice_recovery_io;
void voice_recovery_run(const voice_recovery_io *io,const char *voice_id,const char *task_id);
