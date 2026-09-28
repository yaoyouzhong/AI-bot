namespace AIBotBridge;

internal static class DeviceCenterMenu
{
    internal static ContextMenuStrip Build(DeviceRegistry registry,Action<string> common,Action<string,string> action) {
        var menu=new ContextMenuStrip();
        void Dispatch(Action callback){if(!menu.IsHandleCreated){callback();return;}menu.Close(ToolStripDropDownCloseReason.ItemClicked);menu.BeginInvoke(()=>{if(!menu.IsDisposed)callback();});}
        ToolStripMenuItem Item(string title,string key){var item=new ToolStripMenuItem(title);item.Click+=(_,_)=>Dispatch(()=>common(key));return item;}
        var devices=Item("我的设备","devices");menu.Items.Add(devices);
        foreach(var d in registry.Devices) {
            var device=new ToolStripMenuItem(d.Name+(d.Enabled?"":"（已停用）")){Tag=d.Id};devices.DropDownItems.Add(device);
            foreach(var cap in DeviceCapabilities.Actions(d.Kind)){string key=cap.Action;var item=new ToolStripMenuItem(cap.Label){Enabled=d.Enabled};item.Click+=(_,_)=>Dispatch(()=>action(d.Id,key));device.DropDownItems.Add(item);}
        }
        foreach(var d in registry.Devices.Where(d=>d.Kind==HardwareKind.Esp8266)) {
            var parent=devices.DropDownItems.OfType<ToolStripMenuItem>().Single(i=>Equals(i.Tag,d.Id));
            var modes=new ToolStripMenuItem("显示页面"){Enabled=d.Enabled};parent.DropDownItems.Add(modes);
            foreach(var page in DisplayModes.Pages){string mode=page.Mode;var item=new ToolStripMenuItem(page.Label);item.Click+=(_,_)=>Dispatch(()=>action(d.Id,"mode:"+mode));modes.DropDownItems.Add(item);}
            var animations=new ToolStripMenuItem("天气右下角动画"){Enabled=d.Enabled};parent.DropDownItems.Add(animations);
            foreach(var (label,value) in new[]{("天气机器人","robot"),("像素天气小屋","house"),("像素盆栽","plant"),("天气萌宠","pet"),("关闭动画","off")}){var item=new ToolStripMenuItem(label);item.Click+=(_,_)=>Dispatch(()=>action(d.Id,"animation:"+value));animations.DropDownItems.Add(item);}
        }
        devices.DropDownItems.Add(new ToolStripSeparator());devices.DropDownItems.Add(Item("打开设备中心","devices"));devices.DropDownItems.Add(Item("添加设备","add-device"));
        var data=Item("账号与数据源","accounts");menu.Items.Add(data);
        foreach(var pair in new[]{("模型账号与授权","authorize"),("天气数据","weather-settings"),("自选股票","stocks-settings"),("额度历史","quota-trend"),("刷新数据","refresh")})data.DropDownItems.Add(Item(pair.Item1,pair.Item2));
        var settings=Item("桥接设置","bridge-settings");menu.Items.Add(settings);
        var history=Item("电脑额度历史采集","desktop-history");history.Checked=registry.DesktopQuotaHistory;settings.DropDownItems.Add(history);
        var startup=Item("开机启动","startup");startup.Checked=StartupRegistration.IsEnabled;settings.DropDownItems.Add(startup);settings.DropDownItems.Add(Item("服务状态","status"));
        menu.Items.Add(Item("关于","about"));menu.Items.Add(Item("退出","exit"));return menu;
    }
}
