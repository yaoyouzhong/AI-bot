// Only called after GCM verification with this connection's challenge and the
// selected computer's key. Never publishes state or renews USB ownership.
static bool ble_owner_proof(const char *clear,uint64_t *sequence) {
    cJSON *root=cJSON_Parse(clear);if(!root)return false;
    char selected[33],name[TRUSTED_HOST_NAME];device_host_identity(selected,name);
    device_config cfg=device_get_config();
    cJSON *type=cJSON_GetObjectItemCaseSensitive(root,"type"),*version=cJSON_GetObjectItemCaseSensitive(root,"version"),
        *device=cJSON_GetObjectItemCaseSensitive(root,"deviceId"),*host=cJSON_GetObjectItemCaseSensitive(root,"hostId"),*seq=cJSON_GetObjectItemCaseSensitive(root,"sequence");
    bool ok=cJSON_IsObject(root)&&cJSON_GetArraySize(root)==5&&cJSON_IsString(type)&&!strcmp(type->valuestring,"tab5_ble_owner")&&
        cJSON_IsNumber(version)&&version->valuedouble==1&&cJSON_IsString(device)&&!strcmp(device->valuestring,cfg.id)&&
        cJSON_IsString(host)&&selected[0]&&!strcmp(host->valuestring,selected)&&cJSON_IsNumber(seq)&&
        seq->valuedouble>0&&seq->valuedouble<=9007199254740991.0&&seq->valuedouble==(uint64_t)seq->valuedouble;
    if(ok)*sequence=(uint64_t)seq->valuedouble;
    cJSON_Delete(root);
    return ok;
}
