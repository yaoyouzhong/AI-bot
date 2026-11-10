// Read-only Claude public replies. The binding is opaque and pinned at tap.
#include "claude_remote.h"
#ifdef TAB5_UI_PREVIEW
static claude_remote_state preview_claude_read;
static int preview_claude_page;
bool claude_remote_start(const char *binding,const char *revision,int page){(void)revision;if(preview_claude_read.busy)return false;strcpy(preview_claude_read.binding,binding);preview_claude_read.busy=true;preview_claude_read.generation++;preview_claude_page=page;return true;}
void claude_remote_get(claude_remote_state *out){*out=preview_claude_read;}
void claude_remote_cancel(void){unsigned generation=preview_claude_read.generation+1;memset(&preview_claude_read,0,sizeof(preview_claude_read));preview_claude_read.generation=generation;}
#endif
static lv_obj_t *claude_reader_layer,*claude_reader_title,*claude_reader_status,*claude_reader_scroll,*claude_reader_body,*claude_reader_empty,*claude_reader_count;
static lv_obj_t *claude_reader_previous,*claude_reader_next,*claude_reader_latest;
static claude_remote_state *claude_reader_view;
static char claude_reader_binding[33],claude_reader_revision[65],claude_reader_session[40];
static unsigned claude_reader_host,claude_reader_generation;
static int claude_reader_page,claude_reader_total;
static char *claude_reader_display;
// The current CJK font has no emoji atlas. Use explicit readable names for
// common unsupported symbols; transport and cached source remain exact UTF-8.
static bool claude_reader_display_copy(const char *source){
    static const char *symbols[]={"🙂","😀","😄","✅","⚠️","⚠","🔄","👍","❌"};
    static const char *names[]={"[微笑]","[笑脸]","[开心]","[勾选]","[注意]","[注意]","[刷新]","[赞]","[叉号]"};
    if(!claude_reader_display)claude_reader_display=lv_malloc(8193);
    if(!claude_reader_display)return false;
    size_t used=0;bool replaced=false;
    while(*source&&used<8192){
        bool found=false;
        for(unsigned i=0;i<sizeof(symbols)/sizeof(symbols[0]);i++)if(!strncmp(source,symbols[i],strlen(symbols[i]))){
            size_t n=strlen(names[i]);if(used+n>8192)break;
            memcpy(claude_reader_display+used,names[i],n);used+=n;source+=strlen(symbols[i]);found=true;replaced=true;break;
        }
        if(!found)claude_reader_display[used++]=*source++;
    }
    claude_reader_display[used]=0;return replaced;
}
static void claude_reader_enable(lv_obj_t *o,bool enabled){if(enabled)lv_obj_remove_state(o,LV_STATE_DISABLED);else lv_obj_add_state(o,LV_STATE_DISABLED);}
static void claude_reader_clear(void){reply_document_set(claude_reader_body,"");ui_text(claude_reader_empty,"");ui_text(claude_reader_count,"");if(claude_reader_display)claude_reader_display[0]=0;claude_reader_revision[0]=0;claude_reader_total=0;}
static void claude_reader_close(lv_event_t *e){(void)e;claude_reader_opened=false;claude_remote_cancel();claude_reader_clear();visible(claude_reader_layer,false);}
static void claude_reader_load(int page){
    if(!claude_reader_opened)return;
    if(claude_remote_start(claude_reader_binding,page<0?"":claude_reader_revision,page))ui_text(claude_reader_status,"正在读取公开回复…");
    else ui_text(claude_reader_status,"读取尚未开始，请检查连接后重试");
}
static void claude_reader_navigate(lv_event_t *e){
    int direction=(int)(intptr_t)lv_event_get_user_data(e);
    claude_reader_load(direction?claude_reader_page+direction:-1);
}
static void claude_reader_create(void){
    if(claude_reader_layer)return;
    claude_reader_layer=lv_obj_create(lv_layer_top());lv_obj_remove_style_all(claude_reader_layer);
    lv_obj_set_size(claude_reader_layer,1280,720);ui_bg_color(claude_reader_layer,lv_color_hex(UI_BACKGROUND),0);
    lv_obj_set_style_bg_opa(claude_reader_layer,LV_OPA_COVER,0);
    lv_obj_remove_flag(claude_reader_layer,LV_OBJ_FLAG_SCROLLABLE);lv_obj_add_flag(claude_reader_layer,LV_OBJ_FLAG_CLICKABLE);
    lv_obj_t *back=button(claude_reader_layer,"返回",32,24,128,claude_reader_close,0);lv_obj_set_height(back,64);
    claude_reader_title=label(claude_reader_layer,"Claude 会话",184,24,1048,false);lv_obj_set_height(claude_reader_title,76);
    ui_text_font(claude_reader_title,&tab5_font_30,0);lv_label_set_long_mode(claude_reader_title,LV_LABEL_LONG_DOT);
    claude_reader_status=muted(claude_reader_layer,"",48,110,1184);
    claude_reader_scroll=lv_obj_create(claude_reader_layer);lv_obj_set_pos(claude_reader_scroll,48,154);lv_obj_set_size(claude_reader_scroll,1184,446);
    ui_bg_color(claude_reader_scroll,lv_color_hex(0x2B3539),0);lv_obj_set_style_border_width(claude_reader_scroll,0,0);
    lv_obj_set_style_radius(claude_reader_scroll,12,0);lv_obj_set_style_pad_all(claude_reader_scroll,16,0);
    lv_obj_set_scroll_dir(claude_reader_scroll,LV_DIR_VER);lv_obj_set_scrollbar_mode(claude_reader_scroll,LV_SCROLLBAR_MODE_AUTO);
    claude_reader_body=lv_obj_create(claude_reader_scroll);lv_obj_remove_style_all(claude_reader_body);
    lv_obj_set_width(claude_reader_body,1140);lv_obj_set_height(claude_reader_body,LV_SIZE_CONTENT);lv_obj_set_flex_flow(claude_reader_body,LV_FLEX_FLOW_COLUMN);
    lv_obj_set_style_pad_row(claude_reader_body,8,0);lv_obj_remove_flag(claude_reader_body,LV_OBJ_FLAG_SCROLLABLE);
    claude_reader_empty=label(claude_reader_scroll,"",0,0,1140,false);
    claude_reader_previous=button(claude_reader_layer,"上一页",48,624,180,claude_reader_navigate,-1);
    claude_reader_latest=button(claude_reader_layer,"最新 / 重试",252,624,200,claude_reader_navigate,0);
    claude_reader_next=button(claude_reader_layer,"下一页",476,624,180,claude_reader_navigate,1);
    lv_obj_set_height(claude_reader_previous,60);lv_obj_set_height(claude_reader_latest,60);lv_obj_set_height(claude_reader_next,60);
    claude_reader_count=muted(claude_reader_layer,"",744,642,488);visible(claude_reader_layer,false);
}
static void claude_reader_open(lv_event_t *e){
    (void)e;cJSON *session=item(shown,"claudeSession");const char *binding=text(session,"readBinding");
    if(strlen(binding)!=32||!yes(session,"repliesEnabled"))return;
    claude_reader_create();claude_remote_cancel();claude_reader_clear();
    snprintf(claude_reader_binding,sizeof(claude_reader_binding),"%s",binding);
    snprintf(claude_reader_session,sizeof(claude_reader_session),"%s",text(item(shown,"pcInput"),"session"));
    claude_reader_host=device_host_epoch();claude_reader_opened=true;claude_reader_generation=0;
    const char *title=text(session,"title");ui_text_fmt(claude_reader_title,"Claude · %s",title[0]&&strcmp(title,"--")?title:"Claude Code");
    ui_text(claude_reader_empty,"");visible(claude_reader_empty,false);visible(claude_reader_body,false);
    ui_text(claude_reader_count,"");claude_reader_enable(claude_reader_previous,false);claude_reader_enable(claude_reader_next,false);
    claude_reader_enable(claude_reader_latest,true);
    visible(claude_reader_layer,true);lv_obj_move_foreground(claude_reader_layer);claude_reader_load(-1);
}
static void claude_reader_tick(void){
    if(!claude_reader_opened)return;
    if(saver||ota_update_busy()||photo_flow){claude_reader_close(NULL);return;}
    if(claude_reader_host!=device_host_epoch()||strcmp(claude_reader_session,text(item(shown,"pcInput"),"session"))||!yes(item(shown,"claudeSession"),"repliesEnabled")){
        claude_remote_cancel();claude_reader_clear();ui_text(claude_reader_empty,"电脑已停止提供会话数据，请返回总览");
        visible(claude_reader_empty,true);visible(claude_reader_body,false);ui_text(claude_reader_status,"会话连接已变化");
        claude_reader_enable(claude_reader_previous,false);claude_reader_enable(claude_reader_next,false);claude_reader_enable(claude_reader_latest,false);return;
    }
    if(!claude_reader_view)claude_reader_view=lv_malloc(sizeof(*claude_reader_view));
    if(!claude_reader_view)return;
    claude_remote_get(claude_reader_view);claude_remote_state *v=claude_reader_view;
    if(v->generation==claude_reader_generation)return;
    claude_reader_generation=v->generation;
    claude_reader_enable(claude_reader_previous,!v->busy&&claude_reader_total&&claude_reader_page>0);
    claude_reader_enable(claude_reader_next,!v->busy&&claude_reader_total&&claude_reader_page+1<claude_reader_total);claude_reader_enable(claude_reader_latest,!v->busy);
    if(v->busy){ui_text(claude_reader_status,"正在读取公开回复…");return;}
    if(v->status!=200||strcmp(v->binding,claude_reader_binding)){
        if(v->status==403){
            claude_reader_clear();visible(claude_reader_empty,true);visible(claude_reader_body,false);ui_text(claude_reader_empty,"电脑已停止提供会话数据");
            ui_text(claude_reader_status,"会话数据已停用，请返回总览");
            claude_reader_enable(claude_reader_previous,false);claude_reader_enable(claude_reader_next,false);claude_reader_enable(claude_reader_latest,false);return;
        }
        bool changed=v->status==409&&!strcmp(v->error,"session_changed");
        ui_text(claude_reader_status,changed?"会话已变化，请返回总览重新打开":v->status==409?"回复已更新，请点最新 / 重试":"读取未完成，请检查连接后重试");
        if(changed)claude_reader_enable(claude_reader_latest,false);
        return;
    }
    snprintf(claude_reader_revision,sizeof(claude_reader_revision),"%s",v->revision);
    claude_reader_page=v->page;claude_reader_total=v->total;
    ui_text_fmt(claude_reader_title,"Claude · %s",v->title[0]?v->title:"Claude Code");
    bool symbols=claude_reader_display_copy(v->text);
    reply_document_set(claude_reader_body,claude_reader_display?claude_reader_display:v->text);visible(claude_reader_body,v->text[0]);visible(claude_reader_empty,!v->text[0]);
    ui_text(claude_reader_empty,v->text[0]?"":v->has_earlier?"较早内容未载入，当前范围暂无公开文字":"此会话暂无公开回复");
    ui_text(claude_reader_status,v->has_earlier?"只读 · 已显示最近公开回复，较早内容已省略":symbols?"只读 · 部分表情以文字标记显示":"只读 · Claude 公开回复");
    ui_text_fmt(claude_reader_count,"第 %d / %d 页",v->page+1,v->total);lv_obj_scroll_to_y(claude_reader_scroll,0,LV_ANIM_OFF);
    claude_reader_enable(claude_reader_previous,v->page>0);claude_reader_enable(claude_reader_next,v->page+1<v->total);
}
