#pragma once
#include <string.h>
static inline const char *codex_rejection_feedback(const char *reason) {
    if(!strcmp(reason,"desktop_draft_present"))return "未发送：电脑已有草稿，请先处理";
    if(!strcmp(reason,"desktop_attachments_present"))return "未发送：电脑已有附件或引用";
    if(!strcmp(reason,"desktop_foreground_unconfirmed"))return "未发送：会话未确认，请重新定位";
    if(!strcmp(reason,"desktop_photo_append_unsupported"))return "照片追加暂不支持，草稿已保留";
    if(!strcmp(reason,"pending_delivery_changed"))return "记录已变化，草稿保留，请重新核对";
    if(!strcmp(reason,"composer_changed"))return "未发送：电脑输入状态已变化";
    if(!strcmp(reason,"task_busy_or_state_unknown"))return "任务正在运行，请刷新后追加";
    if(!strcmp(reason,"turn_changed"))return "当前会话已变化，请刷新动态";
    if(!strcmp(reason,"task_waiting_for_input"))return "请先在电脑处理待确认事项";
    return "请求未接受，草稿已保留";
}
