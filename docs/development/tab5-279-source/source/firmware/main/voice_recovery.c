#include "voice_recovery.h"
#include <string.h>

static const char *field(cJSON *root,const char *name) {
    cJSON *item=cJSON_GetObjectItemCaseSensitive(root,name);
    return cJSON_IsString(item)?item->valuestring:"";
}
void voice_recovery_run(const voice_recovery_io *io,const char *voice_id,const char *task_id) {
    void *ctx=io->context;int64_t deadline=io->now(ctx)+120000;
    bool stopped=false;
    io->publish(ctx,"recovering","连接中断，收音已停止；重连后取回文字",NULL);
    while(io->now(ctx)<deadline) {
        if(io->cancelled(ctx)){io->publish(ctx,"cancelled","已取消恢复，原草稿已保留","");return;}
        if(!io->ready(ctx)){io->wait(ctx,250);continue;}
        // Stop is idempotent, including a lost stop response. Never resend an
        // uncertain audio chunk and never resume capture in this take.
        cJSON *reply=io->request(ctx,stopped?"poll":"stop",voice_id);
        if(io->cancelled(ctx)){cJSON_Delete(reply);io->publish(ctx,"cancelled","已取消恢复，原草稿已保留","");return;}
        if(io->now(ctx)>=deadline){cJSON_Delete(reply);break;}
        if(!reply){io->wait(ctx,500);continue;}
        if(strcmp(field(reply,"voiceId"),voice_id)||strcmp(field(reply,"taskId"),task_id)) {
            cJSON_Delete(reply);
            io->publish(ctx,"error","原语音会话已失效，本段未完整保存，请重新录入",NULL);return;
        }
        const char *phase=field(reply,"state"),*text=field(reply,"text");
        if(!strcmp(phase,"review")||!strcmp(phase,"error")||!strcmp(phase,"cancelled")) {
            if(*text)io->publish(ctx,!strcmp(phase,"review")?"review":"error","已取回识别文字，本段可能不完整，请检查后补录",text);
            else io->publish(ctx,"error","本段未完整保存，未取回识别文字，请重新录入",NULL);
            cJSON_Delete(reply);return;
        }
        stopped=!strcmp(phase,"recognizing")||!strcmp(phase,"draining");
        if(!stopped&&strcmp(phase,"recording")) {
            cJSON_Delete(reply);io->publish(ctx,"error","原语音状态无效，本段未完整保存，请重新录入",NULL);return;
        }
        cJSON_Delete(reply);io->wait(ctx,500);
    }
    io->publish(ctx,"error","恢复等待已超时，本段未完整保存，请检查后重新录入",NULL);
}
