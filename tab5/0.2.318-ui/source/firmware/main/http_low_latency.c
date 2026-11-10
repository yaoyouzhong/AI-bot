#include "esp_tls.h"
#include "esp_log.h"
#include "lwip/sockets.h"
#include "lwip/tcp.h"

// IDF 5.4's HTTP TCP transport calls this public connector. Configure only
// the newly owned socket; no SDK patch, global timer or persistent setting.
esp_err_t __real_esp_tls_plain_tcp_connect(const char *host,int hostlen,int port,
    const esp_tls_cfg_t *cfg,esp_tls_error_handle_t error_handle,int *sockfd);
esp_err_t __wrap_esp_tls_plain_tcp_connect(const char *host,int hostlen,int port,
    const esp_tls_cfg_t *cfg,esp_tls_error_handle_t error_handle,int *sockfd) {
    esp_err_t err=__real_esp_tls_plain_tcp_connect(host,hostlen,port,cfg,error_handle,sockfd);
    if(err==ESP_OK&&sockfd&&*sockfd>=0) {
        int enabled=1;
        if(setsockopt(*sockfd,IPPROTO_TCP,TCP_NODELAY,&enabled,sizeof(enabled))!=0)
            ESP_LOGW("tab5_http","TCP_NODELAY unavailable");
    }
    return err;
}
