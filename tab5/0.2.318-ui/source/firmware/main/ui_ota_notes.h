// Small release-note document: headings, lists, paragraphs and dividers.
// Called only when the supplied text changes; no HTML or link actions.
static bool ota_notes_render(lv_obj_t *parent,const char *text) {
    char *copy=lv_malloc(strlen(text)+1);if(!copy)return false;
    strcpy(copy,text);lv_obj_clean(parent);
    char *line=copy;
    for(unsigned blocks=0;*line&&blocks<64;blocks++) {
        char *next=strchr(line,'\n');
        if(blocks==63)next=NULL;
        if(next)*next++=0;
        size_t len=strlen(line);if(len&&line[len-1]=='\r')line[--len]=0;
        if(!strcmp(line,"---")||!strcmp(line,"***")) {
            lv_obj_t *rule=lv_obj_create(parent);lv_obj_remove_style_all(rule);
            lv_obj_set_size(rule,LV_PCT(100),1);
            ui_bg_color(rule,lv_color_hex(0x435056),0);ui_bg_opa(rule,LV_OPA_COVER,0);
        } else if(*line) {
            char *body=line,*mark=NULL;bool heading=false;
            while(*body=='#'&&body-line<6)body++;
            if(body>line&&*body==' '){heading=true;body++;}else body=line;
            if(!heading&&((body[0]=='-'||body[0]=='*'||body[0]=='+')&&body[1]==' ')) {
                mark="·";body+=2;
            } else if(!heading) {
                char *p=body;while(*p>='0'&&*p<='9'&&p-body<3)p++;
                if(p>body&&(*p=='.'||*p==')')&&p[1]==' '){p[1]=0;mark=body;body=p+2;}
            }
            lv_obj_t *row=lv_obj_create(parent);lv_obj_remove_style_all(row);
            lv_obj_set_width(row,LV_PCT(100));lv_obj_set_height(row,LV_SIZE_CONTENT);
            lv_obj_set_flex_flow(row,LV_FLEX_FLOW_ROW);lv_obj_set_style_pad_column(row,8,0);
            lv_obj_remove_flag(row,LV_OBJ_FLAG_SCROLLABLE);
            if(heading)lv_obj_set_style_pad_top(row,blocks?10:0,0);
            if(mark) {
                lv_obj_t *bullet=label(row,mark,0,0,40,false);tint(bullet,BLUE);
            }
            lv_obj_t *body_label=label(row,body,0,0,LV_PCT(100),false);
            lv_obj_set_flex_grow(body_label,1);lv_label_set_long_mode(body_label,LV_LABEL_LONG_WRAP);
            ui_text_font(body_label,heading?&tab5_font_30:&tab5_font_24,0);
            tint(body_label,heading?BLUE:INK);lv_obj_set_style_text_line_space(body_label,6,0);
        } else {
            lv_obj_t *gap=lv_obj_create(parent);lv_obj_remove_style_all(gap);lv_obj_set_size(gap,1,4);
        }
        if(!next)break;
        line=next;
    }
    lv_free(copy);return true;
}
