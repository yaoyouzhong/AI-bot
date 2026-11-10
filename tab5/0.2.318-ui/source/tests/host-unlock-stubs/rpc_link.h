#pragma once
#include "link_state.h"
#include "cJSON.h"
cJSON *rpc_call(link_kind link,cJSON *request,int *status,char nonce[33]);
