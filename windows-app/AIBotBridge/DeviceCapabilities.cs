namespace AIBotBridge;

internal static class DeviceCapabilities
{
    internal static readonly (string Action,string Label)[] EspActions=[("cycle","显示设置"),("appearance","外观设置"),("legacy-settings","连接设置"),("mirror","设备镜像"),("flash","ESP8266 固件升级"),("info","设备信息"),("fallback","Wi-Fi 回退检查"),("reset","重置设备 Wi-Fi"),("pet-gallery","桌宠素材")];
    internal static readonly (string Action,string Label)[] TabActions=[("tab5","连接与固件升级"),("voice","语音设置"),("birthday-settings","日历与生日"),("tab5-display","显示设置"),("task-pins","常用任务")];
    internal static (string Action,string Label)[] Actions(HardwareKind kind)=>kind==HardwareKind.Tab5?TabActions:EspActions;
    internal static bool Allows(RegisteredDevice d,string action)=>d.Enabled&&(action=="data"||d.Kind==HardwareKind.Tab5&&action=="upgrade-tab5"||Actions(d.Kind).Any(a=>a.Action==action)||
        d.Kind==HardwareKind.Esp8266&&(action.StartsWith("cycle:")||action.StartsWith("mode:")||action.StartsWith("screensaver:")||action.StartsWith("animation:")));
    internal static bool RequiresOnline(string action)=>action is "info" or "fallback" or "reset";
    internal static RegisteredDevice Require(DeviceRegistry snapshot,string id,string action,bool online) {
        var device=snapshot.Devices.SingleOrDefault(d=>d.Id==id)??throw new InvalidOperationException("设备已移除，请重新打开设备中心。");
        if(!Allows(device,action))throw new InvalidOperationException("此设备未启用或不支持该功能。");
        if(RequiresOnline(action)&&!online)throw new InvalidOperationException("设备离线，连接后才能执行此操作。");
        return device;
    }
}
