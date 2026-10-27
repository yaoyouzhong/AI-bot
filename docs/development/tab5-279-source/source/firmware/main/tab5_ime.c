#include "tab5_ime.h"
#include <string.h>

typedef struct {const char *py,*text;uint32_t weight,boundaries;} pinyin_entry;
#include "tab5_pinyin_dict.inc"
#define DICT_COUNT (sizeof(pinyin_dictionary)/sizeof(pinyin_dictionary[0]))
#define PAGE_SIZE 4

static uint32_t dictionary_u24(const uint8_t *bytes) {
    return (uint32_t)bytes[0]|((uint32_t)bytes[1]<<8)|((uint32_t)bytes[2]<<16);
}
static const char *dictionary_key(size_t index) {
    return pinyin_keys+dictionary_u24(pinyin_dictionary[index]);
}
static pinyin_entry dictionary_entry(size_t index) {
    const uint8_t *row=pinyin_dictionary[index];
    return (pinyin_entry){dictionary_key(index),pinyin_words+dictionary_u24(row+3),
                          dictionary_u24(row+6),(uint32_t)row[9]|((uint32_t)row[10]<<8)};
}

static size_t lower_bound(const char *key) {
    size_t lo=0,hi=DICT_COUNT;
    while(lo<hi){size_t mid=lo+(hi-lo)/2;if(strcmp(dictionary_key(mid),key)<0)lo=mid+1;else hi=mid;}
    return lo;
}
static void candidate_add(tab5_pinyin_candidate *out,size_t *count,size_t cap,const pinyin_entry *entry,uint8_t consumed) {
    for(size_t i=0;i<*count;i++)if(out[i].consumed==consumed&&!strcmp(out[i].text,entry->text))return;
    size_t pos=0;
    while(pos<*count&&(out[pos].consumed>consumed||(out[pos].consumed==consumed&&out[pos].weight>=entry->weight)))pos++;
    if(pos>=cap)return;
    if(*count<cap)(*count)++;
    for(size_t i=*count-1;i>pos;i--)out[i]=out[i-1];
    out[pos]=(tab5_pinyin_candidate){entry->text,consumed,entry->weight};
}
size_t tab5_pinyin_lookup(const char *raw,tab5_pinyin_candidate *out,size_t capacity) {
    if(!raw||!out||!capacity)return 0;
    char key[TAB5_PINYIN_MAX+1];uint8_t ends[TAB5_PINYIN_MAX+1]={0};
    size_t len=0,count=0;uint32_t boundaries=0;
    for(size_t i=0;raw[i];i++) {
        if(i>=TAB5_PINYIN_MAX)return 0;
        if(raw[i]=='\'') {if(!len||!raw[i+1]||raw[i+1]=='\'')return 0;boundaries|=1u<<len;continue;}
        char c=raw[i];if(c>='A'&&c<='Z')c+=(char)('a'-'A');
        if(c<'a'||c>'z')return 0;
        key[len++]=c;ends[len]=(uint8_t)(i+1);
    }
    key[len]=0;
    if(!len)return 0;
    for(size_t used=len;used>0;used--) {
        char saved=key[used];key[used]=0;
        for(size_t index=lower_bound(key);index<DICT_COUNT&&!strcmp(dictionary_key(index),key);index++) {
            pinyin_entry decoded=dictionary_entry(index);
            const pinyin_entry *entry=&decoded;
            uint32_t required=boundaries&(UINT32_MAX>>(32-used));
            if((entry->boundaries&required)!=required)continue;
            candidate_add(out,&count,capacity,entry,ends[used]);
        }
        key[used]=saved;
    }
    /* An initial/incomplete syllable can still offer single characters. */
    if(!count&&!boundaries)for(size_t index=lower_bound(key);index<DICT_COUNT&&!strncmp(dictionary_key(index),key,len);index++) {
        pinyin_entry entry=dictionary_entry(index);
        if(strlen(entry.text)==3)
            candidate_add(out,&count,capacity,&entry,ends[len]);
    }
    return count;
}

typedef struct {
    lv_obj_t *keyboard,*textarea,*panel,*mode,*spelling,*choices;
    char raw[TAB5_PINYIN_MAX+1];
    tab5_pinyin_candidate candidates[TAB5_PINYIN_CANDIDATES];
    const char *map[PAGE_SIZE+3];
    size_t count,page;
    bool chinese,visible;
} ime_state;
static ime_state *default_ime;

const char *tab5_ime_pending(void){return default_ime?default_ime->raw:"";}
static unsigned utf8_length(const char *s) {unsigned n=0;for(;*s;s++)if(((unsigned char)*s&0xc0)!=0x80)n++;return n;}
static bool insert_text(ime_state *ime,const char *text) {
    uint32_t max=lv_textarea_get_max_length(ime->textarea);
    if(max&&utf8_length(lv_textarea_get_text(ime->textarea))+utf8_length(text)>max)return false;
    lv_textarea_add_text(ime->textarea,text);return true;
}
static void refresh(ime_state *ime) {
    ime->count=tab5_pinyin_lookup(ime->raw,ime->candidates,TAB5_PINYIN_CANDIDATES);
    if(ime->page*PAGE_SIZE>=ime->count)ime->page=0;
    lv_label_set_text(ime->spelling,ime->raw);
    ime->map[0]="<";
    for(size_t i=0;i<PAGE_SIZE;i++)ime->map[i+1]=ime->page*PAGE_SIZE+i<ime->count?ime->candidates[ime->page*PAGE_SIZE+i].text:" ";
    ime->map[PAGE_SIZE+1]=">";ime->map[PAGE_SIZE+2]="";
    lv_buttonmatrix_set_map(ime->choices,ime->map);
    for(size_t i=0;i<PAGE_SIZE+2;i++) {
        bool enabled=i==0?ime->page>0:i==PAGE_SIZE+1?(ime->page+1)*PAGE_SIZE<ime->count:ime->page*PAGE_SIZE+i-1<ime->count;
        if(enabled)lv_buttonmatrix_clear_button_ctrl(ime->choices,(uint32_t)i,LV_BUTTONMATRIX_CTRL_DISABLED);
        else lv_buttonmatrix_set_button_ctrl(ime->choices,(uint32_t)i,LV_BUTTONMATRIX_CTRL_DISABLED);
        lv_buttonmatrix_set_button_width(ime->choices,(uint32_t)i,(i==0||i==PAGE_SIZE+1)?1:3);
    }
}
static void finish(ime_state *ime) {
    if(!ime||!ime->textarea)return;
    if(ime->raw[0]&&insert_text(ime,ime->raw)){ime->raw[0]=0;ime->page=0;refresh(ime);}
}
static void show(ime_state *ime,bool visible) {
    if(!ime||!ime->panel)return;
    if(!visible)finish(ime);
    ime->visible=visible;
    if(visible){lv_obj_remove_flag(ime->panel,LV_OBJ_FLAG_HIDDEN);lv_obj_move_foreground(ime->panel);}
    else lv_obj_add_flag(ime->panel,LV_OBJ_FLAG_HIDDEN);
}
static void choose(ime_state *ime,size_t index) {
    if(index>=ime->count||!insert_text(ime,ime->candidates[index].text))return;
    size_t used=ime->candidates[index].consumed;
    while(ime->raw[used]=='\'')used++;
    memmove(ime->raw,ime->raw+used,strlen(ime->raw+used)+1);ime->page=0;refresh(ime);
}
static void choices_event(lv_event_t *e) {
    ime_state *ime=lv_event_get_user_data(e);
    uint32_t button=lv_buttonmatrix_get_selected_button(lv_event_get_target(e));
    if(button==0){if(ime->page)ime->page--;refresh(ime);}
    else if(button==PAGE_SIZE+1){if((ime->page+1)*PAGE_SIZE<ime->count)ime->page++;refresh(ime);}
    else if(button>=1&&button<=PAGE_SIZE)choose(ime,ime->page*PAGE_SIZE+button-1);
}
static void mode_event(lv_event_t *e) {
    ime_state *ime=lv_event_get_user_data(e);
    (void)e;finish(ime);if(ime->raw[0])return;
    ime->chinese=!ime->chinese;
    lv_label_set_text(lv_obj_get_child(ime->mode,0),ime->chinese?"中":"EN");
}
static void keyboard_event(lv_event_t *e) {
    ime_state *ime=lv_event_get_user_data(e);
    if(lv_obj_has_state(ime->textarea,LV_STATE_DISABLED))return;
    uint32_t selected=lv_buttonmatrix_get_selected_button(ime->keyboard);
    if(selected==LV_BUTTONMATRIX_BUTTON_NONE)return;
    const char *key=lv_buttonmatrix_get_button_text(ime->keyboard,selected);
    if(!key)return;
    if(ime->chinese&&key[1]==0&&((key[0]>='a'&&key[0]<='z')||(key[0]>='A'&&key[0]<='Z')||(key[0]=='\''&&ime->raw[0]))) {
        size_t len=strlen(ime->raw);
        if(len<TAB5_PINYIN_MAX) {ime->raw[len]=key[0]>='A'&&key[0]<='Z'?key[0]+'a'-'A':key[0];ime->raw[len+1]=0;ime->page=0;refresh(ime);}
        return;
    }
    if(ime->raw[0]) {
        if(!strcmp(key,LV_SYMBOL_BACKSPACE)){ime->raw[strlen(ime->raw)-1]=0;ime->page=0;refresh(ime);return;}
        if((!strcmp(key," ")||!strcmp(key,LV_SYMBOL_NEW_LINE))&&ime->count){choose(ime,ime->page*PAGE_SIZE);return;}
        finish(ime);if(ime->raw[0])return;
    }
    lv_keyboard_def_event_cb(e);
}
lv_obj_t *tab5_ime_create(lv_obj_t *parent,lv_obj_t *keyboard,lv_obj_t *textarea,const lv_font_t *font) {
    ime_state *ime=lv_malloc_zeroed(sizeof(*ime));LV_ASSERT_MALLOC(ime);
    if(!default_ime)default_ime=ime;
    ime->keyboard=keyboard;ime->textarea=textarea;ime->chinese=true;
    lv_obj_remove_event_cb(keyboard,lv_keyboard_def_event_cb);
    lv_obj_add_event_cb(keyboard,keyboard_event,LV_EVENT_VALUE_CHANGED,ime);
    ime->panel=lv_obj_create(parent);lv_obj_set_pos(ime->panel,0,194);lv_obj_set_size(ime->panel,1220,56);
    lv_obj_remove_flag(ime->panel,LV_OBJ_FLAG_SCROLLABLE);lv_obj_set_style_pad_all(ime->panel,4,0);
    lv_obj_set_style_border_width(ime->panel,0,0);lv_obj_set_style_radius(ime->panel,0,0);
    lv_obj_set_style_bg_color(ime->panel,lv_color_hex(0x20272A),0);
    lv_obj_set_style_text_font(ime->panel,font,0);lv_obj_set_style_text_color(ime->panel,lv_color_hex(0xE4EDF1),0);
    ime->mode=lv_button_create(ime->panel);lv_obj_set_size(ime->mode,56,48);
    lv_obj_set_style_bg_color(ime->mode,lv_color_hex(0x354247),0);lv_obj_set_style_shadow_width(ime->mode,0,0);
    lv_obj_t *mode_label=lv_label_create(ime->mode);lv_label_set_text(mode_label,"中");lv_obj_center(mode_label);
    lv_obj_add_event_cb(ime->mode,mode_event,LV_EVENT_CLICKED,ime);
    ime->spelling=lv_label_create(ime->panel);lv_obj_set_pos(ime->spelling,72,9);lv_obj_set_width(ime->spelling,174);
    lv_label_set_long_mode(ime->spelling,LV_LABEL_LONG_DOT);
    ime->choices=lv_buttonmatrix_create(ime->panel);lv_obj_set_pos(ime->choices,258,0);lv_obj_set_size(ime->choices,954,48);
    lv_obj_remove_flag(ime->choices,LV_OBJ_FLAG_CLICK_FOCUSABLE);
    lv_obj_set_style_pad_all(ime->choices,0,0);lv_obj_set_style_pad_column(ime->choices,8,0);
    lv_obj_set_style_bg_opa(ime->choices,LV_OPA_TRANSP,0);lv_obj_set_style_border_width(ime->choices,0,0);
    lv_obj_set_style_text_font(ime->choices,font,LV_PART_ITEMS);lv_obj_set_style_text_color(ime->choices,lv_color_hex(0xE4EDF1),LV_PART_ITEMS);
    lv_obj_set_style_bg_color(ime->choices,lv_color_hex(0x354247),LV_PART_ITEMS);
    lv_obj_set_style_border_width(ime->choices,0,LV_PART_ITEMS);lv_obj_set_style_radius(ime->choices,8,LV_PART_ITEMS);
    lv_obj_set_style_pad_all(ime->choices,0,LV_PART_ITEMS);
    lv_obj_set_style_bg_color(ime->choices,lv_color_hex(0xC3E5F0),LV_PART_ITEMS|LV_STATE_PRESSED);
    lv_obj_set_style_text_color(ime->choices,lv_color_hex(0x173642),LV_PART_ITEMS|LV_STATE_PRESSED);
    lv_obj_add_event_cb(ime->choices,choices_event,LV_EVENT_VALUE_CHANGED,ime);
    lv_obj_set_user_data(ime->panel,ime);
    refresh(ime);show(ime,false);return ime->panel;
}

void tab5_ime_finish(void){finish(default_ime);}
void tab5_ime_show(bool visible){show(default_ime,visible);}
void tab5_ime_show_for(lv_obj_t *panel,bool visible){if(panel)show(lv_obj_get_user_data(panel),visible);}
void tab5_ime_finish_for(lv_obj_t *panel){if(panel)finish(lv_obj_get_user_data(panel));}
const char *tab5_ime_pending_for(lv_obj_t *panel){ime_state *ime=panel?lv_obj_get_user_data(panel):NULL;return ime?ime->raw:"";}
void tab5_ime_reset_for(lv_obj_t *panel){ime_state *ime=panel?lv_obj_get_user_data(panel):NULL;if(ime){ime->raw[0]=0;ime->page=0;refresh(ime);}}
