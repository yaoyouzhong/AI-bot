#pragma once
#include <stdbool.h>
#include <stdint.h>
typedef struct {
    bool busy;
    bool ready;
    bool silent;
    bool can_insert;
    int level;
    unsigned generation;
    char task_id[37], source[8], phase[20], message[160], text[2001];
    char source_name[512];
} voice_remote_state;
bool voice_remote_start(const char *task_id, const char *session, int64_t epoch_ms);
bool voice_remote_start_tab5(const char *owner, const char *session, int64_t epoch_ms);
bool voice_remote_init(void);
void voice_remote_command(int command); /* 1 stop, 2 cancel, 3 use TAB5 */
void voice_remote_get(voice_remote_state *out);
void voice_remote_notify_reply(void); /* Short local chime, deferred while capturing. */
void voice_remote_notify_health(void); /* Distinct double alert, without reply speech. */
void voice_remote_notify_timer(void); /* Non-spoken chime, respecting mute and capture. */
void voice_remote_notify_agenda(void); /* Agenda-only cue; same worker, mute and capture priority. */
bool voice_remote_preview_sound(void); /* Worker-owned playback; refuses while recording or muted. */
int voice_remote_queue_depth(void);
int voice_remote_mic_error(void);
