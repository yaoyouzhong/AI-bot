#include "reply_document.h"
#include <string.h>
#include <stdlib.h>
extern const lv_font_t tab5_font_24, tab5_font_30;

void reply_document_set(lv_obj_t *parent, const char *text) {
    lv_obj_clean(parent);
    // One temporary buffer, allocated through LVGL's PSRAM allocator on device.
    char *copy=lv_malloc(strlen(text)+1);
    if(!copy)return;
    strcpy(copy,text);
    char *line=copy;
    unsigned blocks=0;
    while(*line) {
        bool code=false;
        if(!strncmp(line,"```",3)) {
            char *start=strchr(line,'\n');
            if(!start)break;
            line=start+1;code=true;
        }
        char *end=strchr(line,'\n');
        char *next=end?end+1:NULL;
        if(code) {
            end=strstr(line,"\n```");
            next=end?strchr(end+1,'\n'):NULL;
            if(next)next++;
        }
        // Count empty lines too; hostile or unusually long pages cannot create
        // unbounded LVGL objects. Keep the remainder visible in the final block.
        if(++blocks==64){end=NULL;next=NULL;}
        if(end)*end=0;
        size_t length=strlen(line);
        if(length&&line[length-1]=='\r')line[length-1]=0;
        if(*line) {
            char *body=line;
            bool heading=false;
            if(!code) {
                while(*body=='#'&&body-line<6)body++;
                if(body>line&&*body==' '){heading=true;body++;}else body=line;
            }
            lv_obj_t *label=lv_label_create(parent);
            lv_obj_set_width(label,LV_PCT(100));
            lv_obj_set_height(label,LV_SIZE_CONTENT);
            lv_label_set_text(label,body);
            lv_obj_set_style_text_font(label,heading?&tab5_font_30:&tab5_font_24,0);
            lv_obj_set_style_text_color(label,lv_color_hex(heading?0xD9EEE8:code?0xB8DCD7:0xD2DCDA),0);
            lv_obj_set_style_text_line_space(label,code?5:9,0);
            lv_obj_set_style_pad_top(label,heading?8:code?8:0,0);
            lv_obj_set_style_pad_bottom(label,code?8:4,0);
            if(code) {
                lv_obj_set_style_bg_color(label,lv_color_hex(0x202B30),0);
                lv_obj_set_style_bg_opa(label,LV_OPA_COVER,0);
                lv_obj_set_style_pad_hor(label,12,0);
                lv_obj_set_style_radius(label,8,0);
            }
        } else {
            lv_obj_t *gap=lv_obj_create(parent);
            lv_obj_remove_style_all(gap);lv_obj_set_size(gap,1,8);
        }
        if(!next)break;
        line=next;
    }
    lv_free(copy);
}
