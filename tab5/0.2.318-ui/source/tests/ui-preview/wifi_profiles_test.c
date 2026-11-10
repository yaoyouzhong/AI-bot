#include "wifi_profiles.h"
#undef NDEBUG
#include <assert.h>
#include <stdio.h>
#include <string.h>
int main(void) {
    wifi_profile_store s={.version=WIFI_PROFILE_VERSION};assert(wifi_profiles_valid(&s));
    assert(wifi_profiles_put(&s,"home","synthetic-home-password"));
    assert(wifi_profiles_put(&s,"office","synthetic-office-password"));
    assert(s.count==2&&s.preferred==1&&!strcmp(s.entries[0].ssid,"home"));
    assert(wifi_profiles_put(&s,"home","changed-synthetic-password")&&s.count==2&&s.preferred==0);
    assert(wifi_profiles_index(&s,"office")==1&&wifi_profiles_index(&s,"unknown")==-1);
    assert(wifi_profiles_put(&s,"3","")&&wifi_profiles_put(&s,"4","")&&wifi_profiles_put(&s,"5",""));
    wifi_profile_store before=s;
    assert(!wifi_profiles_put(&s,"","x")&&!wifi_profiles_put(&s,NULL,"x"));
    s.entries[0].ssid[32]='x';memset(s.entries[0].ssid,'x',32);assert(!wifi_profiles_valid(&s));
    s=before;s.count=99;assert(!wifi_profiles_valid(&s));
    s=before;strcpy(s.entries[1].ssid,s.entries[0].ssid);assert(!wifi_profiles_valid(&s));
    s=before;assert(wifi_profiles_forget(&s,"office"));assert(s.count==4&&s.preferred==3);
    assert(wifi_profiles_index(&s,"office")==-1&&!strcmp(s.entries[1].ssid,"3"));
    wifi_profile empty={0};assert(!memcmp(&s.entries[4],&empty,sizeof(empty)));
    wifi_profile_store unchanged=s;assert(!wifi_profiles_forget(&s,"missing")&&!wifi_profiles_forget(&s,NULL));assert(!memcmp(&s,&unchanged,sizeof(s)));
    char active[33]="home",password[64]="old";
    wifi_profiles_sync_active(&s,active,password);assert(!strcmp(active,"home")&&!strcmp(password,"changed-synthetic-password"));
    assert(wifi_profiles_forget(&s,"home"));wifi_profiles_sync_active(&s,active,password);
    assert(!strcmp(active,"5")&&s.preferred==2);
    assert(wifi_profiles_forget(&s,"5"));wifi_profiles_sync_active(&s,active,password);assert(!strcmp(active,"4"));
    assert(wifi_profiles_forget(&s,"3")&&wifi_profiles_forget(&s,"4"));
    assert(wifi_profiles_valid(&s)&&s.count==0&&s.preferred==0);
    // Reboot with a stale legacy config cannot resurrect the last deleted SSID.
    strcpy(active,"4");strcpy(password,"old");wifi_profiles_sync_active(&s,active,password);
    char zero_name[33]={0},zero_password[64]={0};assert(!memcmp(active,zero_name,33)&&!memcmp(password,zero_password,64));
    assert(wifi_profiles_put(&s,"new","new-password")&&s.count==1);
    wifi_profile_store_v1 legacy={.version=1,.count=2,.preferred=1,.entries={{.ssid="home",.password="synthetic-home"},{.ssid="office",.password="synthetic-office"}}};
    assert(wifi_profiles_migrate(&s,&legacy)&&s.version==WIFI_PROFILE_VERSION&&s.count==2&&s.preferred==1);
    assert(!s.entries[0].label[0]&&!strcmp(s.entries[1].password,"synthetic-office"));
    assert(wifi_profiles_set_label(&s,"office","单位访客网")&&s.preferred==1);
    before=s;assert(!wifi_profiles_set_label(&s,"missing","家里")&&!wifi_profiles_set_label(&s,"office","bad\nlabel"));
    assert(!wifi_profiles_set_label(&s,"office","1234567890123456789012345678901234567890123456789"));assert(!memcmp(&s,&before,sizeof(s)));
    assert(wifi_profiles_put(&s,"office","updated")&&!strcmp(s.entries[1].label,"单位访客网"));
    assert(wifi_profiles_set_label(&s,"office","")&&!s.entries[1].label[0]&&!strcmp(s.entries[1].password,"updated"));
    assert(wifi_profiles_set_label(&s,"home","家里")&&wifi_profiles_forget(&s,"home"));
    assert(!strcmp(s.entries[0].ssid,"office")&&!s.entries[0].label[0]);
    legacy.entries[0].ssid[0]=0;before=s;assert(!wifi_profiles_migrate(&s,&legacy)&&!memcmp(&s,&before,sizeof(s)));
    wifi_profile_store_v2 old_v2={.version=2,.count=5,.preferred=3};
    for(unsigned i=0;i<5;i++) {
        snprintf(old_v2.entries[i].ssid,33,"saved-%u",i);
        snprintf(old_v2.entries[i].password,64,"synthetic-%u",i);
        snprintf(old_v2.entries[i].label,WIFI_LABEL_SIZE,"label-%u",i);
    }
    assert(sizeof(wifi_profile_store_v1)==500&&sizeof(old_v2)==744);
    assert(wifi_profiles_migrate_v2(&s,&old_v2)&&s.count==5&&s.preferred==3);
    assert(!memcmp(s.entries,old_v2.entries,sizeof(old_v2.entries)));
    for(unsigned i=5;i<10;i++) {char name[33];snprintf(name,sizeof(name),"saved-%u",i);assert(wifi_profiles_put(&s,name,"synthetic-new"));}
    assert(s.count==10&&s.preferred==9);
    before=s;assert(!wifi_profiles_put(&s,"eleventh",""));assert(!memcmp(&s,&before,sizeof(s)));
    assert(wifi_profiles_put(&s,"saved-9","updated-tenth")&&s.count==10);
    wifi_profile_store rebooted;memcpy(&rebooted,&s,sizeof(s));
    assert(wifi_profiles_valid(&rebooted)&&!strcmp(rebooted.entries[9].password,"updated-tenth"));
    assert(wifi_profiles_forget(&rebooted,"saved-5")&&wifi_profiles_put(&rebooted,"replacement","")&&rebooted.count==10);
    old_v2.count=0;old_v2.preferred=0;assert(wifi_profiles_migrate_v2(&s,&old_v2)&&!s.count);
    strcpy(active,"stale");strcpy(password,"stale");wifi_profiles_sync_active(&s,active,password);assert(!active[0]&&!password[0]);
    before=s;old_v2.count=6;assert(!wifi_profiles_migrate_v2(&s,&old_v2)&&!memcmp(&s,&before,sizeof(s)));
    legacy.count=6;assert(!wifi_profiles_migrate(&s,&legacy)&&!memcmp(&s,&before,sizeof(s)));
    puts("WIFI_CAPACITY_10_OK v1_v2_fixed_abi_migration_preserved_tenth_reboot_eleventh_rejected_empty_authoritative");
    puts("WIFI_LABELS_OK legacy_migration_labels_preserve_password_upsert_clear_delete_validation");
    puts("WIFI_PROFILES_OK preservation_capacity_forget_active_last_stale_legacy_credentials");return 0;
}
