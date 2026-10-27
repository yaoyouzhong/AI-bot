#undef NDEBUG
#define TAB5_AGENDA_TEST
#include <assert.h>
#include <stddef.h>
#include <stdlib.h>
#include <crtdbg.h>
#include "../../firmware/main/agenda.c"
static void test_commit_document(const void *value){agenda_deletions removed;assert(agenda_storage_decode(value,local_stage_size,&disk,&removed));}
int main(int argc,char **argv) {
    _set_abort_behavior(0,_WRITE_ABORT_MSG|_CALL_REPORTFAULT);
    _CrtSetReportMode(_CRT_ASSERT,_CRTDBG_MODE_FILE);_CrtSetReportFile(_CRT_ASSERT,_CRTDBG_FILE_STDERR);setvbuf(stdout,NULL,_IONBF,0);
    cJSON *fixture=NULL;
    if(argc==2) {
        FILE *file=fopen(argv[1],"rb");assert(file);fseek(file,0,SEEK_END);long size=ftell(file);rewind(file);
        char *json=calloc(size+1,1);assert(json&&fread(json,1,size,file)==(size_t)size);fclose(file);
        fixture=cJSON_Parse(json);free(json);assert(fixture);host_workday_years=cJSON_GetObjectItemCaseSensitive(fixture,"workdayYears");assert(cJSON_IsArray(host_workday_years));
    }
    agenda_state unavailable;agenda_get(&unavailable);assert(!unavailable.ready);
    fail_allocation=true;agenda_start();assert(!memory&&!guard&&!commands&&!task_calls);fail_allocation=false;
    fail_mutex=true;agenda_start();assert(!memory&&!guard&&!commands&&!task_calls&&allocation_frees==1);fail_mutex=false;
    fail_queue=true;agenda_start();assert(!memory&&!guard&&!commands&&!task_calls&&allocation_frees==2&&mutex_deletes==1);fail_queue=false;
    fail_task=true;agenda_start();assert(memory&&guard&&!commands&&task_calls==1&&queue_deletes==1);
    agenda_get(&unavailable);assert(!unavailable.ready&&strstr(unavailable.message,"启动失败"));
    assert(!agenda_save(&(agenda_item){0},"")&&!agenda_remove("11111111111111111111111111111111","22222222222222222222222222222222"));
    vSemaphoreDelete(guard);guard=0;free(memory);memory=NULL;fail_task=false;
    agenda_start();assert(memory&&guard&&commands&&task_calls==2);
    agenda_memory *same=memory;unsigned allocations=allocation_calls;agenda_start();assert(memory==same&&allocation_calls==allocations&&task_calls==2);
    assert(allocation_caps==(MALLOC_CAP_SPIRAM|MALLOC_CAP_8BIT)&&allocation_bytes==sizeof(*memory));
    puts("AGENDA_PSRAM_START_OK external-only allocation, safe allocation/mutex/queue/task failures, unavailable reads and repeat-start ownership");
    assert(load_document()&&memory->state.ready&&memory->state.document.count==0);
    host=memory->state.document;host.count=1;memset(host.revision,'a',32);agenda_item *i=&host.items[0];memset(i->id,'1',32);memset(i->edit,'2',32);strcpy(i->title,"offline schedule");i->due=test_clock;i->end=test_clock+3600;i->repeat=0;
    assert(agenda_document_valid(&host));disk=host;disk_present=true;memset(&memory->state,0,sizeof(memory->state));fail_write=true;
    assert(load_document()&&!local_size&&memory->state.document.count==1&&!memcmp(&memory->state.document,&host,sizeof(host))&&strstr(memory->state.message,"原数据已保留"));fail_write=false;
    sync_run(LINK_USB);assert(memory->state.document.count==1&&disk_present);
    // The installed 202 envelope migrates on boot, releasing settings headroom.
    agenda_local legacy202={.version=1,.document=host};memcpy(local_disk,&legacy202,sizeof(legacy202));local_size=sizeof(legacy202);
    memset(&memory->state,0,sizeof(memory->state));assert(load_document()&&memory->state.document.count==1&&local_size<=AGENDA_STORAGE_MAX);
    // An unset RTC must never manufacture a due alarm.
    test_time_valid=false;alert_run();assert(!notice_pending);test_time_valid=true;fail_write=true;alert_run();assert(!notice_pending&&disk.items[0].alerted==0);fail_write=false;alert_run();assert(notice_pending&&disk.items[0].alerted==test_clock&&local_size<=AGENDA_STORAGE_MAX);
    char title[97],id[33];int64_t occurrence;assert(agenda_alert(0,title,id,&occurrence)&&occurrence==test_clock);alert_run();assert(!notice_pending);
    // Reboot preserves deduplication and a locally committed snooze.
    memset(&memory->state,0,sizeof(memory->state));assert(load_document());alert_run();assert(!notice_pending);
    command c={.occurrence=test_clock,.until=test_clock+600};strcpy(c.id,i->id);strcpy(c.edit,i->edit);action_run(&c);assert(memory->state.pending&&disk.items[0].snooze_until==test_clock+600);
    memset(&memory->state,0,sizeof(memory->state));assert(load_document()&&memory->state.pending);test_clock+=599;alert_run();assert(!notice_pending);test_clock++;alert_run();assert(notice_pending);agenda_alert(0,title,id,&occurrence);
    response_status=0;sync_run(LINK_BLE);assert(memory->state.pending);response_status=200;sync_run(LINK_WIFI);assert(!memory->state.pending&&host.items[0].snooze_until==test_clock);
    // Save failure does not announce success, mutate visible data, or clear a queued operation.
    c.complete=true;c.request=7;fail_write=true;action_run(&c);assert(memory->state.document.items[0].done==0&&disk.items[0].done==0&&!memory->state.last_success&&memory->state.completed_request==7);fail_write=false;c.request=8;action_run(&c);assert(memory->state.pending&&disk.items[0].done==c.occurrence&&memory->state.last_success&&memory->state.completed_request==8);
    fail_write=true;sync_run(LINK_USB);assert(memory->state.pending);fail_write=false;sync_run(LINK_USB);assert(!memory->state.pending&&host.items[0].done==c.occurrence);
    // Automatic ending needs no user action, network, or additional flash write.
    host.items[0].done=0;host.items[0].snooze_occurrence=host.items[0].snooze_until=0;sync_run(LINK_USB);test_clock=host.items[0].end;
    agenda_document before_end=disk;fail_write=true;alert_run();assert(!notice_pending&&agenda_view_at(&memory->state.document.items[0],test_clock).complete&&!memcmp(&before_end,&disk,sizeof(disk)));fail_write=false;
    // Snoozing at or beyond end is rejected without mutating state.
    c.complete=false;c.until=test_clock+600;c.request=9;action_run(&c);assert(!memory->state.last_success&&memory->state.completed_request==9&&!memcmp(&before_end,&disk,sizeof(disk)));
    // Tomorrow's lead may arrive while today's long event is still running.
    host.items[0].due=test_clock;host.items[0].end=test_clock+86400;host.items[0].repeat=1;host.items[0].lead=10;memset(host.items[0].edit,'3',32);sync_run(LINK_USB);
    alert_run();assert(agenda_alert(0,title,id,&occurrence)&&occurrence==test_clock);
    test_clock+=85800;alert_run();assert(agenda_alert(0,title,id,&occurrence)&&occurrence==host.items[0].due+86400);alert_run();assert(!notice_pending);
    // Device definitions are persisted first and survive unsupported hosts and lost acknowledgements.
    command edit={.save=true,.request=10};strcpy(edit.definition.title,"device created");edit.definition.due=test_clock+3600;edit.definition.end=test_clock+7200;
    save_run(&edit);assert(memory->state.last_success&&memory->state.pending&&disk.count==2&&disk.items[1].dirty==4);char created[33];strcpy(created,memory->state.saved_id);
    memset(&memory->state,0,sizeof(memory->state));assert(load_document()&&memory->state.pending&&memory->state.document.count==2);
    host_support_edits=false;sync_run(LINK_USB);assert(memory->state.pending&&memory->state.document.count==2&&disk.count==2);
    host_support_edits=true;host_omit_device_ack=true;sync_run(LINK_USB);assert(memory->state.pending&&memory->state.document.count==2&&host.count==2);host_omit_device_ack=false;
    fail_write=true;sync_run(LINK_USB);assert(memory->state.pending&&host.count==2);fail_write=false;sync_run(LINK_USB);assert(!memory->state.pending&&host.count==2&&disk.count==2);
    edit.definition=memory->state.document.items[1];strcpy(edit.edit,edit.definition.edit);char base_edit[33];strcpy(base_edit,edit.edit);strcpy(edit.definition.title,"first local edit");edit.request=11;save_run(&edit);assert(memory->state.last_success&&!strcmp(disk.items[1].base_edit,base_edit));
    edit.definition=memory->state.document.items[1];strcpy(edit.edit,edit.definition.edit);strcpy(edit.definition.title,"second local edit");edit.request=12;save_run(&edit);assert(memory->state.last_success&&!strcmp(disk.items[1].base_edit,base_edit)&&!strcmp(disk.items[1].id,created));
    before_end=disk;edit.definition.end=edit.definition.due;edit.request=13;save_run(&edit);assert(!memory->state.last_success&&!memcmp(&before_end,&disk,sizeof(disk)));
    edit.definition=memory->state.document.items[1];strcpy(edit.edit,edit.definition.edit);strcpy(edit.definition.title,"failed local save");edit.request=14;fail_write=true;save_run(&edit);assert(!memory->state.last_success&&!memcmp(&before_end,&disk,sizeof(disk)));fail_write=false;
    // A complete year can merge National Day and Mid-Autumn into one range.
    cJSON *merged=cJSON_CreateArray(),*merged_year=cJSON_CreateObject();cJSON_AddItemToArray(merged,merged_year);cJSON_AddNumberToObject(merged_year,"year",2025);
    cJSON *merged_off=cJSON_AddArrayToObject(merged_year,"holidays");cJSON_AddArrayToObject(merged_year,"workdays");
    const int merged_dates[]={101,201,401,501,601,1001};const char *merged_names[]={"元旦","春节","清明节","劳动节","端午节","国庆节、中秋节"};
    for(unsigned n=0;n<6;n++){cJSON *range=cJSON_CreateObject();cJSON_AddNumberToObject(range,"start",merged_dates[n]);cJSON_AddNumberToObject(range,"end",merged_dates[n]);cJSON_AddStringToObject(range,"name",merged_names[n]);cJSON_AddItemToArray(merged_off,range);}
    host_workday_years=merged;sync_run(LINK_USB);assert(!memory->state.pending&&strstr(memory->state.message,"已同步")&&saved_workdays.count==1);
    // A bad calendar cannot withhold a valid item acknowledgement or erase the
    // last good calendar; its own warning must remain distinct from item sync.
    agenda_workday_calendar good_calendar=calendar_disk;
    edit.definition=memory->state.document.items[1];strcpy(edit.edit,edit.definition.edit);strcpy(edit.definition.title,"calendar independent sync");edit.request=15;save_run(&edit);assert(memory->state.pending);
    cJSON_DeleteItemFromObjectCaseSensitive(cJSON_GetArrayItem(merged_off,5),"name");
    sync_run(LINK_USB);assert(!memory->state.pending&&strstr(memory->state.message,"事项已同步")&&strstr(memory->state.message,"工作日历更新失败")&&!memcmp(&calendar_disk,&good_calendar,sizeof(good_calendar)));
    cJSON_AddStringToObject(cJSON_GetArrayItem(merged_off,5),"name",merged_names[5]);sync_run(LINK_USB);assert(!memory->state.pending&&!strstr(memory->state.message,"失败"));
    host_workday_years=NULL;cJSON_Delete(merged);
    puts("AGENDA_CALENDAR_SYNC_OK merged holiday ranges accepted; malformed calendar preserves last-good data without blocking acknowledged items");
    // Delete commits locally without a host, including an unsynchronized edit.
    command deletion={.remove=true,.request=20};strcpy(deletion.id,memory->state.document.items[1].id);strcpy(deletion.edit,memory->state.document.items[1].edit);
    agenda_document retained=memory->state.document;test_link=LINK_NONE;remove_run(&deletion);
    assert(memory->state.last_success&&memory->state.document.count==1&&disk.count==1&&host.count==2&&memory->state.pending&&memory->deleted.count==1&&!memcmp(&retained.items[0],&memory->state.document.items[0],sizeof(agenda_item)));
    memset(&memory->state,0,sizeof(memory->state));memset(&memory->deleted,0,sizeof(memory->deleted));assert(load_document()&&memory->state.document.count==1&&memory->state.pending&&memory->deleted.count==1);
    host_support_delete=false;sync_run(LINK_USB);assert(memory->state.document.count==1&&memory->deleted.count==1&&memory->state.pending);host_support_delete=true;
    host_wrong_delete_ack=true;sync_run(LINK_USB);assert(host.count==1&&memory->state.document.count==1&&memory->deleted.count==1&&memory->state.pending);host_wrong_delete_ack=false;
    // Failed receipt persistence retains the durable tombstone; restart + retry is idempotent.
    fail_write=true;sync_run(LINK_WIFI);assert(memory->state.pending&&memory->deleted.count==1);fail_write=false;
    memset(&memory->state,0,sizeof(memory->state));memset(&memory->deleted,0,sizeof(memory->deleted));assert(load_document()&&memory->deleted.count==1);sync_run(LINK_BLE);assert(!memory->state.pending&&!memory->deleted.count&&memory->state.document.count==1&&host.count==1);
    edit=(command){.save=true,.request=21};strcpy(edit.definition.title,"delete before first sync");edit.definition.due=test_clock+3600;edit.definition.end=test_clock+7200;save_run(&edit);assert(memory->state.pending&&memory->state.document.count==2);
    strcpy(deletion.id,memory->state.document.items[1].id);strcpy(deletion.edit,memory->state.document.items[1].edit);retained=disk;fail_write=true;remove_run(&deletion);assert(!memory->state.last_success&&host.count==1&&!memcmp(&retained,&disk,sizeof(disk))&&!memory->deleted.count);fail_write=false;
    remove_run(&deletion);assert(memory->state.last_success&&memory->state.document.count==1&&memory->deleted.count==1);sync_run(LINK_USB);assert(!memory->state.pending&&host.count==1&&!memory->deleted.count);
    // A local edit followed by deletion removes the old host edition too.
    edit=(command){.save=true,.request=22};strcpy(edit.definition.title,"edit then delete");edit.definition.due=test_clock+3600;edit.definition.end=test_clock+7200;save_run(&edit);sync_run(LINK_USB);assert(!memory->state.pending&&host.count==2);
    edit.definition=memory->state.document.items[1];strcpy(edit.edit,edit.definition.edit);strcpy(edit.definition.title,"offline changed");save_run(&edit);
    strcpy(deletion.id,memory->state.document.items[1].id);strcpy(deletion.edit,memory->state.document.items[1].edit);remove_run(&deletion);assert(memory->state.last_success&&memory->deleted.count==1&&host.count==2);sync_run(LINK_USB);assert(!memory->state.pending&&!memory->deleted.count&&host.count==1);
    // Queue capacity refuses further deletion safely; active records remain editable.
    agenda_deletions real_deleted=memory->deleted;memory->deleted.count=AGENDA_LIMIT;strcpy(deletion.id,memory->state.document.items[0].id);strcpy(deletion.edit,memory->state.document.items[0].edit);retained=memory->state.document;remove_run(&deletion);assert(!memory->state.last_success&&!memcmp(&retained,&memory->state.document,sizeof(retained)));memory->deleted=real_deleted;
    puts("AGENDA_LOCAL_FIRST_OK offline create/edit/delete, durable tombstones, reboot, old host protection, wrong/lost receipts, failed atomic writes, delete-before-first-sync, edit-then-delete and bounded queue");
    // Expiration alone never prunes. Only a full-list insertion reclaims one slot.
    agenda_document original_document=memory->state.document;int64_t original_time=test_clock;
    agenda_item expiring={0};memset(expiring.id,'8',32);memset(expiring.edit,'9',32);strcpy(expiring.title,"expired retained");expiring.due=test_clock+60;expiring.end=test_clock+120;
    agenda_item undated={0};memset(undated.id,'a',32);memset(undated.edit,'b',32);strcpy(undated.title,"keep undated");
    memory->candidate=memory->state.document;memory->candidate.items[memory->candidate.count++]=expiring;memory->candidate.items[memory->candidate.count++]=undated;assert(persist(&memory->candidate));publish(&memory->candidate,"");host=memory->candidate;
    test_clock=expiring.end;alert_run();assert(memory->state.document.count==3&&!notice_pending);
    memset(&memory->state,0,sizeof(memory->state));assert(load_document()&&memory->state.document.count==3);sync_run(LINK_USB);assert(memory->state.document.count==3&&!memory->deleted.count);
    host_support_retention=false;agenda_document retained_local=memory->state.document;sync_run(LINK_USB);assert(!memcmp(&retained_local,&memory->state.document,sizeof(retained_local))&&strstr(memory->state.message,"更新电脑"));host_support_retention=true;
    memory->candidate=memory->state.document;
    while(memory->candidate.count<15){unsigned n=memory->candidate.count++;agenda_item *fill=&memory->candidate.items[n];memset(fill,0,sizeof(*fill));snprintf(fill->id,33,"%032x",4096+n);snprintf(fill->edit,33,"%032x",8192+n);strcpy(fill->title,"future item");fill->due=test_clock+3600;fill->end=test_clock+7200;}
    assert(persist(&memory->candidate));publish(&memory->candidate,"");host=memory->candidate;
    command fresh={.save=true,.request=100};strcpy(fresh.definition.title,"sixteenth item");fresh.definition.due=test_clock+60;fresh.definition.end=test_clock+120;
    save_run(&fresh);assert(memory->state.last_success&&memory->state.document.count==16&&!memory->deleted.count&&!strcmp(memory->state.document.items[1].id,expiring.id));sync_run(LINK_USB);assert(!memory->state.pending&&host.count==16);
    // Both are expired now; the earlier ending must be the only eviction.
    test_clock+=120;fresh.request++;fresh.definition.due=test_clock+3600;fresh.definition.end=test_clock+7200;strcpy(fresh.definition.title,"replace oldest");
    retained_local=disk;test_time_valid=false;save_run(&fresh);assert(!memory->state.last_success&&!memcmp(&retained_local,&disk,sizeof(disk)));test_time_valid=true;
    fail_write=true;save_run(&fresh);assert(!memory->state.last_success&&!memory->deleted.count&&!memcmp(&retained_local,&disk,sizeof(disk)));fail_write=false;
    save_run(&fresh);assert(memory->state.last_success&&memory->state.document.count==16&&memory->deleted.count==1&&!strcmp(memory->deleted.items[0].id,expiring.id)&&memory->state.document.items[0].repeat&&memory->state.document.items[2].due==0);
    memset(&memory->state,0,sizeof(memory->state));memset(&memory->deleted,0,sizeof(memory->deleted));assert(load_document()&&memory->state.document.count==16&&memory->deleted.count==1);
    host_wrong_delete_ack=true;sync_run(LINK_USB);assert(memory->state.pending&&memory->deleted.count==1);host_wrong_delete_ack=false;
    fail_write=true;sync_run(LINK_USB);assert(memory->state.pending&&memory->deleted.count==1);fail_write=false;
    sync_run(LINK_USB);assert(!memory->state.pending&&!memory->deleted.count&&host.count==16);for(unsigned n=0;n<host.count;n++)assert(strcmp(host.items[n].id,expiring.id));
    // Updating a retained expired entry is not an insertion and does not reclaim.
    command update={.save=true,.request=102,.definition=memory->state.document.items[15]};strcpy(update.edit,update.definition.edit);strcpy(update.definition.title,"edit retained expired");save_run(&update);assert(memory->state.last_success&&memory->state.document.count==16&&!memory->deleted.count);
    host_omit_device_ack=true;sync_run(LINK_USB);assert(memory->state.pending);host_omit_device_ack=false;sync_run(LINK_USB);assert(!memory->state.pending);
    // A full receipt queue refuses replacement, but never loses either document.
    memory->deleted.count=AGENDA_LIMIT;for(unsigned n=0;n<memory->deleted.count;n++){snprintf(memory->deleted.items[n].id,33,"%032x",256+n);snprintf(memory->deleted.items[n].edit,33,"%032x",512+n);}
    memory->candidate=memory->state.document;assert(persist(&memory->candidate));publish(&memory->candidate,"");retained_local=disk;save_run(&fresh);assert(!memory->state.last_success&&!memcmp(&retained_local,&disk,sizeof(disk))&&memory->deleted.count==AGENDA_LIMIT);
    memset(&memory->deleted,0,sizeof(memory->deleted));save_run(&fresh);assert(memory->state.last_success&&memory->state.document.count==16&&memory->deleted.count==1);sync_run(LINK_USB);assert(!memory->state.pending);
    retained_local=disk;save_run(&fresh);assert(!memory->state.last_success&&!memcmp(&retained_local,&disk,sizeof(disk))); // no expired one-offs remain
    memory->candidate=original_document;assert(persist(&memory->candidate));publish(&memory->candidate,"");test_clock=original_time;
    puts("AGENDA_RETENTION_OK under-limit reboot/sync retention, old-host gate, sixteenth insertion, oldest-only replacement, unset RTC, failed atomic write, durable eviction receipt, lost acknowledgements, expired editing, full receipt queue and no-victim rejection");
    // A corrupt new snapshot cannot fall back to a valid legacy snapshot.
    local_disk[0]=99;uint8_t preserved[sizeof(local_disk)];memcpy(preserved,local_disk,sizeof(preserved));assert(!load_document()&&!memcmp(local_disk,preserved,sizeof(preserved)));
    cJSON *years=cJSON_CreateArray(),*year=cJSON_CreateObject();cJSON_AddItemToArray(years,year);cJSON_AddNumberToObject(year,"year",2027);
    cJSON *holidays=cJSON_AddArrayToObject(year,"holidays");const int dates[]={101,201,401,501,601,901,1001};
    for(unsigned n=0;n<7;n++){cJSON *range=cJSON_CreateObject();cJSON_AddNumberToObject(range,"start",dates[n]);cJSON_AddNumberToObject(range,"end",dates[n]);cJSON_AddItemToArray(holidays,range);}
    cJSON *on=cJSON_AddArrayToObject(year,"workdays");cJSON_AddItemToArray(on,cJSON_CreateNumber(103));
    agenda_workday_calendar table={0},empty={.version=1};assert(agenda_workdays_parse(years,&table));
    int64_t makeup=(int64_t)(countdown_civil_day(2027,1,3)-countdown_civil_day(1970,1,1))*86400;
    assert(agenda_workday_at(makeup)<0&&workday_cache_store(&table));assert(agenda_workday_at(makeup)==1);
    agenda_workdays_set(&empty);memset(&saved_workdays,0,sizeof(saved_workdays));assert(agenda_workday_at(makeup)<0);workday_cache_load();assert(agenda_workday_at(makeup)==1);
    agenda_workday_calendar prior=calendar_disk;table.years[0].working[0]=0;fail_write=true;assert(!workday_cache_store(&table)&&!memcmp(&calendar_disk,&prior,sizeof(prior))&&agenda_workday_at(makeup)==1);fail_write=false;
    cJSON_DeleteItemFromArray(holidays,6);assert(!agenda_workdays_parse(years,&table)&&agenda_workday_at(makeup)==1);cJSON_Delete(years);
    puts("AGENDA_WORKDAY_CACHE_OK independent annual NVS cache; offline reboot, makeup Sunday, failed write and partial-year rejection preserve last-good calendar");
    cJSON_Delete(fixture);
    printf("AGENDA_RUNTIME_OK production worker load/save, unset RTC, reboot deduplication, offline snooze, failed sync retry, persistence failures and corrupt-store preservation; legacy=%zu local=%zu bytes\n",sizeof(disk),sizeof(agenda_local));free(memory);memory=NULL;return 0;
}
