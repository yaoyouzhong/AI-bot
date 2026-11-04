"""Exercise the selection function from the proposed TAB5 patch, without UI or device I/O."""
from pathlib import Path
import os
import subprocess
import tempfile

root = Path(__file__).resolve().parents[1]
original = (root / 'docs/development/TAB5-CLIENT-COMPANIONS-20261104.patch').read_text()
start = original.index('diff --git a/firmware/main/ui_claude_companion.h ')
end = original.index('\ndiff --git ', start + 10)
lines = original[start:end].splitlines()
body = []
in_hunk = False
for line in lines:
    if line.startswith('@@ '):
        in_hunk = True
    elif in_hunk and line.startswith('+') and not line.startswith('+++'):
        body.append(line[1:])
assert len(body) == 98
with tempfile.TemporaryDirectory(prefix='aibot-tab5-policy-') as tmp:
    base = Path(tmp)
    file = base / 'firmware/main/ui_claude_companion.h'
    file.parent.mkdir(parents=True)
    file.write_text('\n'.join(body) + '\n')
    start = original.index('diff --git a/tests/ui-preview/claude_companion_ui_test.h ')
    end = original.find('\ndiff --git ', start + 10)
    block = original[start:end if end != -1 else None].splitlines()
    ui_lines, in_hunk = [], False
    for line in block:
        if line.startswith('@@ '):
            in_hunk = True
        elif in_hunk and line.startswith('+') and not line.startswith('+++'):
            ui_lines.append(line[1:])
    assert len(ui_lines) == 71
    ui_file = base / 'tests/ui-preview/claude_companion_ui_test.h'
    ui_file.parent.mkdir(parents=True)
    ui_file.write_text('\n'.join(ui_lines) + '\n')
    ui_source = base / 'firmware/main/ui.c'
    ui_source.write_text('''    if(home_companion_provider==0) {
        visible(home_companion,false);visible(home_task_title,true);visible(model_dot[1],false);
        for(int i=0;i<2;i++){visible(home_system_captions[i],false);visible(home_system_values[i],false);}
        ui_text(home_task_note,"当前客户端");ui_text(home_task_title,"Claude Code");
        lv_obj_set_width(home_task_title,480);lv_obj_set_y(home_task_title,68);
''')
    subprocess.run(['patch', '-p1', '-i', str(root / 'docs/development/TAB5-COMPANION-SELECTION-20261104.patch')], cwd=base, check=True, capture_output=True)
    if os.environ.get('TAB5_SOURCE'):
        verified = Path(os.environ['TAB5_SOURCE'])
        file = verified / 'firmware/main/ui_claude_companion.h'
        ui_source = verified / 'firmware/main/ui.c'
    assert 'item(item(shown,"claudeSession"),"title")' in ui_source.read_text()
    assert 'cJSON_IsString(claude_title_value)' in ui_source.read_text()
    assert 'claude_title?claude_title:"Claude Code"' in ui_source.read_text()
    source = file.read_text()
    a = source.index('static bool companion_busy(')
    b = source.index('static codex_companion_state companion_model_mood(', a)
    production = source[a:b]
    prelude = r'''
#include <assert.h>
#include <stdbool.h>
#include <stdio.h>
#include <string.h>
typedef struct node { const char *key, *str, *valuestring; double valuedouble; int isnum, flag; struct node *child; int count; } cJSON;
static cJSON *item(cJSON *n,const char *key) { if(!n)return NULL; for(int i=0;i<n->count;i++)if(!strcmp(n->child[i].key,key))return &n->child[i];return NULL; }
static int cJSON_IsNumber(cJSON *n){return n&&n->isnum;}
static int cJSON_IsString(cJSON *n){return n&&n->valuestring!=NULL;}
static bool yes(cJSON *n,const char *key){cJSON *v=item(n,key);return v&&v->flag;}
static const char *text(cJSON *n,const char *key){cJSON *v=item(n,key);return v&&v->str?v->str:"";}
#define LV_MIN(a,b) ((a)<(b)?(a):(b))
static int home_companion_provider=1;
'''
    cases = r'''
int main(void) {
    cJSON codeFields[]={{.key="state",.str="working"},{.key="ageSeconds",.isnum=1,.valuedouble=10},{.key="needsInput"}};
    cJSON claudeFields[]={{.key="state",.str="idle"},{.key="ageSeconds",.isnum=1,.valuedouble=20},{.key="needsInput"}};
    cJSON usageFields[]={{.key="lastActivity",.isnum=1,.valuedouble=985}};
    cJSON providers[]={{.key="deepseek",.child=usageFields,.count=1}};
    cJSON domesticFields[]={{.key="state",.str="idle"},{.key="activeProvider",.str="deepseek"},{.key="needsInput"},{.key="providers",.child=providers,.count=1}};
    cJSON claudeSessionFields[]={{.key="lastActivity"}};
    cJSON codexTaskFields[]={{.key="updatedAt"}};
    cJSON tasksFields[]={{.key="overviewTask",.child=codexTaskFields,.count=1}};
    cJSON rootFields[]={{.key="codex",.child=codeFields,.count=3},{.key="claude",.child=claudeFields,.count=3},{.key="domesticActivity",.child=domesticFields,.count=4},{.key="epochUtc",.isnum=1,.valuedouble=1000},{.key="claudeSession",.child=claudeSessionFields,.count=1},{.key="codexTasks",.child=tasksFields,.count=1}};
    cJSON root={.child=rootFields,.count=6};
    assert(companion_provider_resolve(&root,1u)==0); /* only Claude selected, even when Codex works */
    codeFields[0].str="idle"; claudeFields[0].str="working";
    assert(companion_provider_resolve(&root,2u)==1); /* only Codex selected */
    assert(companion_provider_resolve(&root,3u)==0); /* both: working Claude */
    codeFields[2].flag=1; assert(companion_provider_resolve(&root,3u)==1); /* waiting beats working */
    codeFields[2].flag=0; claudeFields[0].str="idle"; /* both idle: most recent */
    assert(companion_provider_resolve(&root,3u)==1);
    claudeFields[1].valuedouble=5; assert(companion_provider_resolve(&root,3u)==0);
    claudeFields[1].valuedouble=10; usageFields[0].valuedouble=990; home_companion_provider=0;
    assert(companion_provider_resolve(&root,3u)==0); /* tie preserves current */
    claudeFields[1].isnum=0; codeFields[1].isnum=0; usageFields[0].isnum=0;
    assert(companion_provider_resolve(&root,3u)==0); /* absent legacy fields */
    codeFields[1].isnum=1;codeFields[1].valuedouble=-1;claudeFields[1].isnum=1;claudeFields[1].valuedouble=9;
    assert(companion_provider_resolve(&root,3u)==0); /* invalid negative age is not recent */
    usageFields[0].isnum=1;usageFields[0].valuedouble=1001;claudeFields[1].isnum=0;codeFields[1].valuedouble=20;
    assert(companion_provider_resolve(&root,3u)==1); /* future activity ignored */
    claudeSessionFields[0].isnum=1;claudeSessionFields[0].valuedouble=995;
    codexTaskFields[0].isnum=1;codexTaskFields[0].valuedouble=980;
    assert(companion_provider_resolve(&root,3u)==0); /* sessions override unrelated global timestamps */
    codexTaskFields[0].valuedouble=998;assert(companion_provider_resolve(&root,3u)==1);
    cJSON titleFields[]={{.key="title"}};
    cJSON titleRootFields[]={{.key="claudeSession",.child=titleFields,.count=1}};
    cJSON titleRoot={.child=titleRootFields,.count=1};
    assert(!strcmp(claude_title_display(&titleRoot),"Claude Code"));
    titleFields[0].valuestring="";assert(!strcmp(claude_title_display(&titleRoot),"Claude Code"));
    titleFields[0].valuestring="Synthetic";assert(!strcmp(claude_title_display(&titleRoot),"Synthetic"));
    puts("TAB5_COMPANION_SELECTION_OK 12 selection and 3 title cases; isolated source policy, no UI or device");
}
'''
    driver = base / 'test.c'
    ui_text = ui_source.read_text()
    begin = ui_text.index('        cJSON *claude_title_value=')
    end = ui_text.index('        ui_text(home_task_note,', begin)
    title_projection = 'static const char *claude_title_display(cJSON *shown) {\n' + ui_text[begin:end] + 'return claude_title?claude_title:"Claude Code";\n}\n'
    driver.write_text(prelude + production + title_projection + cases)
    exe = base / 'test'
    subprocess.run(['cc', '-std=c11', '-Wall', '-Wextra', '-Werror', str(driver), '-o', str(exe)], check=True)
    subprocess.run([str(exe)], check=True)
