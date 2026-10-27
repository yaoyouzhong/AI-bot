// Wi-Fi worker owns all probes. USB only copies the numeric snapshot; it never
// waits on ESP-Hosted RPC, sockets, or the AP. No credentials are collected.
#include <errno.h>
#include <fcntl.h>
static esp_netif_t *station_netif;
static atomic_int status_transport_error,status_tls_code,status_tls_flags;
static struct {
    uint32_t mask,gateway,runs;
    uint64_t mac,bssid,netif_mac;
    int channel,rssi,is_default,gateway_error,host_error,ps,ap_error;
} wifi_path={.gateway_error=-1,.host_error=-1,.ps=-1,.ap_error=-1};
static uint64_t wifi_mac_number(const uint8_t mac[6]) {
    uint64_t value=0;for(int i=0;i<6;i++)value=(value<<8)|mac[i];return value;
}
static int wifi_tcp_probe(uint32_t address,unsigned port) {
    if(!address||!port||port>65535)return EINVAL;
    int fd=socket(AF_INET,SOCK_STREAM,IPPROTO_TCP);if(fd<0)return errno;
    int result=0,flags=fcntl(fd,F_GETFL,0);
    if(flags<0||fcntl(fd,F_SETFL,flags|O_NONBLOCK)<0){result=errno;goto done;}
    struct sockaddr_in peer={.sin_family=AF_INET,.sin_port=htons(port),.sin_addr.s_addr=address};
    if(connect(fd,(struct sockaddr *)&peer,sizeof(peer))<0) {
        result=errno;if(result!=EINPROGRESS)goto done;
        fd_set ready;FD_ZERO(&ready);FD_SET(fd,&ready);
        struct timeval timeout={.tv_sec=1};
        int selected=select(fd+1,NULL,&ready,NULL,&timeout);
        if(selected<=0){result=selected==0?ETIMEDOUT:errno;goto done;}
        socklen_t length=sizeof(result);
        if(getsockopt(fd,SOL_SOCKET,SO_ERROR,&result,&length)<0)result=errno;
    }
done:
    close(fd);return result;
}
static void wifi_path_sample(const device_config *cfg) {
    static int64_t next_sample;
    int64_t now=esp_timer_get_time()/1000;
    if(now<next_sample||!wifi_connected||ota_update_busy()||!atomic_load(&status_open_error))return;
    next_sample=now+30000;
    esp_netif_ip_info_t ip={0};wifi_ap_record_t ap={0};uint8_t mac[6]={0},netif_mac[6]={0};wifi_ps_type_t ps;
    esp_netif_get_ip_info(station_netif,&ip);
    esp_netif_get_mac(station_netif,netif_mac);
    int ap_error=esp_wifi_sta_get_ap_info(&ap);esp_wifi_get_mac(WIFI_IF_STA,mac);
    int ps_value=esp_wifi_get_ps(&ps)==ESP_OK?(int)ps:-1;
    int gateway_error=wifi_tcp_probe(ip.gw.addr,80);
    int host_error=wifi_tcp_probe(inet_addr(cfg->host),cfg->port);
    int is_default=esp_netif_get_default_netif()==station_netif;
    xSemaphoreTake(device_mutex,portMAX_DELAY);
    wifi_path.mask=ntohl(ip.netmask.addr);wifi_path.gateway=ntohl(ip.gw.addr);
    wifi_path.mac=wifi_mac_number(mac);wifi_path.bssid=wifi_mac_number(ap.bssid);
    wifi_path.netif_mac=wifi_mac_number(netif_mac);
    wifi_path.channel=ap.primary;wifi_path.rssi=ap.rssi;wifi_path.ap_error=ap_error;wifi_path.ps=ps_value;
    wifi_path.is_default=is_default;
    wifi_path.runs++;wifi_path.gateway_error=gateway_error;wifi_path.host_error=host_error;
    xSemaphoreGive(device_mutex);
}
void tab5_wifi_path_diagnostics(char *out,size_t size) {
    xSemaphoreTake(device_mutex,portMAX_DELAY);
    snprintf(out,size,"1,%u,%u,%d,%d,%llu,%llu,%d,%u,%d,%d,%d,%d,%d,%d,%d,%llu",
        (unsigned)wifi_path.mask,(unsigned)wifi_path.gateway,wifi_path.channel,wifi_path.rssi,
        (unsigned long long)wifi_path.mac,(unsigned long long)wifi_path.bssid,wifi_path.is_default,(unsigned)wifi_path.runs,
        wifi_path.gateway_error,wifi_path.host_error,atomic_load(&status_transport_error),
        atomic_load(&status_tls_code),atomic_load(&status_tls_flags),wifi_path.ps,wifi_path.ap_error,(unsigned long long)wifi_path.netif_mac);
    xSemaphoreGive(device_mutex);
}
