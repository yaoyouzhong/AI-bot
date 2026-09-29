namespace AIBotBridge;

internal static class DeviceCenterMenu
{
    internal static ContextMenuStrip Build(DeviceRegistry registry,Action<string,string> deviceAction,Action<string> common) {
        var menu=new ContextMenuStrip{ShowImageMargin=false,ShowItemToolTips=false};
        void Dispatch(Action action){if(!menu.IsHandleCreated){action();return;}menu.Close(ToolStripDropDownCloseReason.ItemClicked);menu.BeginInvoke(()=>{if(!menu.IsDisposed)action();});}
        ToolStripMenuItem Entry(string label,string key,Action action){var item=new ToolStripMenuItem(label){Name=key};item.Click+=(_,_)=>Dispatch(action);return item;}
        menu.Items.Add(Entry("设备中心","devices",()=>common("devices")));
        var esp=registry.Devices.SingleOrDefault(d=>d.Kind==HardwareKind.Esp8266&&d.Enabled);
        if(esp is not null)menu.Items.Add(Entry("小屏预览","mirror",()=>deviceAction(esp.Id,"mirror")));
        if(registry.Devices.Length>0)menu.Items.Add(new ToolStripSeparator());
        foreach(var device in registry.Devices.OrderBy(d=>d.Kind)) {
            string name=device.Name.Length>24?device.Name[..23]+"…":device.Name;
            var group=new ToolStripMenuItem(name.Replace("&","&&")+(device.Enabled?"":"（已停用）")){Name="device:"+device.Id,Enabled=device.Enabled,AutoToolTip=false};
            ((ToolStripDropDownMenu)group.DropDown).ShowImageMargin=false;
            (string Key,string Label)[] actions=device.Kind==HardwareKind.Esp8266
                ?[("cycle","显示设置"),("appearance","外观设置"),("data","数据设置"),("legacy-settings","连接设置")]
                :[("voice","语音设置"),("birthday-settings","日历生日"),("data","数据设置"),("tab5","连接升级")];
            foreach(var (key,label) in actions)group.DropDownItems.Add(Entry(label,key,()=>deviceAction(device.Id,key)));
            menu.Items.Add(group);
        }
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(Entry("退出","exit",()=>common("exit")));
        return menu;
    }
}
