#include <assert.h>
#include <stdbool.h>
#include <stdio.h>
#include <string.h>
#include "rpc_timeout_under_test.inc"
int main(void){
    assert(rpc_exchange_timeout("host-unlock",false)==2000);
    assert(rpc_exchange_timeout("unlock-pin",false)==75000);
    assert(rpc_exchange_timeout("wifi-unlock",false)==75000);
    assert(rpc_exchange_timeout("computer-control",false)==75000);
    assert(rpc_exchange_timeout("runtime-diagnostics",false)==2000);
    assert(rpc_exchange_timeout("ota",false)==10000);
    assert(rpc_exchange_timeout("gallery",false)==8000);
    assert(rpc_exchange_timeout(NULL,true)==5000);
    assert(rpc_exchange_admission("host-unlock")==1000);
    assert(rpc_exchange_admission("unlock-pin")==1000);
    assert(rpc_exchange_admission("wifi-unlock")==0);
    assert(rpc_exchange_admission("computer-control")==1000);
    assert(rpc_exchange_admission("voice")==2000);
    assert(rpc_exchange_admission("screen-preview")==100);
    assert(rpc_exchange_timeout("screen-preview",false)==3000);
    assert(rpc_exchange_admission(NULL)==0);
    assert(rpc_exchange_admission("boot-verify")==1000);
    assert(rpc_exchange_admission("ota")==2000);
    assert(rpc_exchange_admission("ota-control")==2000);
    puts("HOST_UNLOCK_TIMEOUT_TEST_OK 16 assertions");
}
