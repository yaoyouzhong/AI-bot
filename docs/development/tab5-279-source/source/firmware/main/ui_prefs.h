#pragma once
#include <stdbool.h>
#include <stdint.h>
#define UI_CLOCK_STYLES 9
#define UI_HOME_PROVIDERS 11
// 0 automatic; 1 locked normal landscape; 2 locked inverted landscape.
int ui_orientation_load(void);
bool ui_orientation_save(int mode);
#ifndef TAB5_LOCAL_ART
#define TAB5_LOCAL_ART 0
#endif
#define UI_SAVER_COUNTDOWN 8
#define UI_SAVER_STYLES 9
static inline bool ui_saver_style_valid(int style) {
    return style>=0&&style<UI_SAVER_STYLES&&(TAB5_LOCAL_ART||style<6||style==UI_SAVER_COUNTDOWN);
}
// Persisted IDs stay stable. Legacy day/night selections open the unified automatic style.
static inline int ui_saver_style_normalize(int style) {return style==1||style==2?5:style;}
static inline int ui_saver_style_from_option(int option) {
    static const int choices[]={0,5,3,4,
#if TAB5_LOCAL_ART
        6,7,
#endif
        UI_SAVER_COUNTDOWN};
    return option>=0&&option<(int)(sizeof(choices)/sizeof(choices[0]))?choices[option]:0;
}
static inline int ui_saver_option_from_style(int style) {
    style=ui_saver_style_normalize(style);
    for(int i=0;i<(TAB5_LOCAL_ART?7:5);i++)if(ui_saver_style_from_option(i)==style)return i;
    return 0;
}
typedef struct {uint32_t version;uint16_t year;uint8_t month,day;char title[97];} ui_date_countdown;
bool ui_date_countdown_load(ui_date_countdown *value);
bool ui_date_countdown_save(const ui_date_countdown *value);
typedef struct {
    uint32_t version;
    int selected, interval, saver_minutes;
    bool cycle, alerts;
    uint8_t enabled[8], order[8];
} ui_preferences;
ui_preferences ui_preferences_load(void);
bool ui_preferences_save(const ui_preferences *prefs);
int ui_clock_style_load(void);
bool ui_clock_style_save(int style);
int ui_saver_style_load(void);
bool ui_saver_style_save(int style);
bool ui_tf_cache_load(void); // Missing setting means the original device cache.
bool ui_tf_cache_save(bool enabled);
unsigned ui_home_models_load(void);
bool ui_home_models_save(unsigned mask);
typedef struct {uint32_t version;char task[37],request[37],operation[16],turn[37],text[2001];char images[3][37];} ui_codex_checkpoint;
bool ui_codex_checkpoint_load(ui_codex_checkpoint *value);
bool ui_codex_checkpoint_save(const ui_codex_checkpoint *value);
typedef struct {char task[37],turn[64];int page,y;bool follow;unsigned stamp;} ui_reading_bookmark;
bool ui_reading_load(ui_reading_bookmark values[32]);
bool ui_reading_save(const ui_reading_bookmark values[32]);
