namespace AIBotBridge;

internal static class ConnectionAdvice
{
    internal static string For(bool enabled,DeviceView view) {
        if(!enabled)return "设备已停用；需要使用时在设备中心启用。";
        if(view.Status=="连接异常")return "通信服务异常；在此选择该设备并点击“重新连接”。";
        if(view.Wifi?.Contains("桥接未通")==true)return view.Online
            ?"当前连接可用；Wi-Fi 已联网但桥接未通，请检查电脑与设备的局域网连接。"
            :"Wi-Fi 已联网但桥接未通；请检查电脑与设备是否在同一局域网，以及桥接的防火墙访问权限。";
        if(view.Online)return "当前通信正常，无需操作。";
        if(view.Status=="连接中")return "正在连接设备，请稍候。";
        return "设备未连接，等待接入；需要使用时连接 USB 数据线，或检查设备的 Wi-Fi。";
    }
}
