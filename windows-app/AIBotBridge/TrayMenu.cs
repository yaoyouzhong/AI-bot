namespace AIBotBridge;

// Independently constructed from the legacy user-facing grouping, not copied source.
internal static class TrayMenu
{
    internal static ContextMenuStrip Build(Action<string> action, Action<string> selectMode,
        Func<string> selectedMode, Func<string> connection, Func<QuotaSnapshot?>? captureQuotas = null)
    {
        var menu = new ContextMenuStrip();
        ToolStripMenuItem Group(string text)
        {
            var item = new ToolStripMenuItem(text);
            menu.Items.Add(item);
            return item;
        }
        void Command(ToolStripMenuItem parent, string text, string command) =>
            parent.DropDownItems.Add(text, null, (_, _) => action(command));
        void Mode(ToolStripMenuItem parent, string text, string mode)
        {
            var item = new ToolStripMenuItem(text) { Tag = mode };
            item.Click += (_, _) => selectMode(mode);
            parent.DropDownOpening += (_, _) => item.Checked = selectedMode() == mode;
            parent.DropDownItems.Add(item);
        }

        var quota = Group("模型额度");
        foreach (var provider in new[] { "Claude", "Codex" })
        {
            var summaryItem = new ToolStripMenuItem(provider + "：等待额度数据") { Enabled = false };
            quota.DropDownItems.Add(summaryItem);
            quota.DropDownOpening += (_, _) =>
            {
                var snapshot = captureQuotas?.Invoke();
                var value = provider == "Claude" ? snapshot?.Claude : snapshot?.Codex;
                string Percent(double? percent) => percent.HasValue ? ((int)Math.Clamp(percent.Value,0,100)) + "%" : "--";
                summaryItem.Text = $"{provider}：5H {Percent(value?.PrimaryPercent)} · WK {Percent(value?.WeeklyPercent)}" + (value?.Stale == true ? "（缓存）" : "");
            };
        }
        quota.DropDownItems.Add(new ToolStripSeparator());
        Command(quota, "Codex 额度趋势…", "quota-trend");
        Command(quota, "国产模型额度授权…", "authorize");

        var device = Group("设备连接");
        var summary = new ToolStripMenuItem { Enabled = false };
        device.DropDownItems.Add(summary);
        device.DropDownOpening += (_, _) => summary.Text = connection();
        device.DropDownItems.Add(new ToolStripSeparator());
        Command(device, "设备控制…", "device");
        Command(device, "设置连接串口…", "settings");
        var usb = new ToolStripMenuItem("USB 管理与诊断");
        Command(usb, "设备信息…", "info");
        Command(usb, "测试 Wi-Fi 回退（保持 USB 供电）…", "fallback");
        usb.DropDownItems.Add(new ToolStripSeparator());
        Command(usb, "重置设备 Wi-Fi…", "reset");
        device.DropDownItems.Add(usb);

        var display = Group("显示模式");
        Mode(display, "智能跟随", "auto");
        Mode(display, "Claude", "claude");
        Mode(display, "Codex", "codex");
        Mode(display, "Claude + Codex 额度", "dual");
        var domestic = new ToolStripMenuItem("国产模型");
        foreach (var page in DisplayModes.Pages.Where(page => page.Mode.StartsWith("domestic_")))
            Mode(domestic, page.Label, page.Mode);
        display.DropDownItems.Add(domestic);
        Mode(display, "系统监控", "system");
        Mode(display, "音乐播放", "music");
        Mode(display, "股票行情", "stocks");
        Mode(display, "天气时钟", "weather");
        Mode(display, "桌宠", "pet");
        Mode(display, "AI 活动状态", "activity");
        display.DropDownItems.Add(new ToolStripSeparator());
        var saver = new ToolStripMenuItem("屏保");
        foreach (var minutes in new[] { 0, 1, 5, 10, 30, 60 })
        {
            var item = new ToolStripMenuItem(minutes == 0 ? "关闭" : minutes + " 分钟");
            item.Click += (_, _) => action("screensaver:" + minutes);
            saver.DropDownOpening += (_, _) => item.Checked = BridgeSettings.Load().Get("screensaver_timeout_minutes", "0") == minutes.ToString();
            saver.DropDownItems.Add(item);
        }
        saver.DropDownItems.Add(new ToolStripSeparator());
        Mode(saver, "立即预览", "screensaver");
        display.DropDownItems.Add(saver);

        var cycle = Group("循环展示");
        var enabled = new ToolStripMenuItem("启用循环展示");
        enabled.Click += (_, _) => action("cycle:toggle");
        cycle.DropDownOpening += (_, _) => enabled.Checked = DisplayModes.Load(BridgeSettings.Load()).CycleEnabled;
        cycle.DropDownItems.Add(enabled);
        cycle.DropDownItems.Add(new ToolStripSeparator());
        foreach (var page in DisplayModes.Pages)
        {
            var item = new ToolStripMenuItem(page.Label);
            item.Click += (_, _) => action("cycle:page:" + page.Mode);
            cycle.DropDownOpening += (_, _) => item.Checked = DisplayModes.Load(BridgeSettings.Load()).Pages.Contains(page.Mode);
            cycle.DropDownItems.Add(item);
        }
        var intervals = new ToolStripMenuItem("切换间隔");
        foreach (var seconds in new[] {10,15,30,60})
        {
            var item = new ToolStripMenuItem(seconds + " 秒");
            item.Click += (_, _) => action("cycle:interval:" + seconds);
            intervals.DropDownOpening += (_, _) => item.Checked = DisplayModes.Load(BridgeSettings.Load()).IntervalSeconds == seconds;
            intervals.DropDownItems.Add(item);
        }
        cycle.DropDownItems.Add(new ToolStripSeparator());
        cycle.DropDownItems.Add(intervals);
        Command(cycle, "调整展示顺序…", "cycle");

        var content = Group("内容设置");
        Command(content, "设置自选股…", "stocks-settings");
        var weather = new ToolStripMenuItem("设置天气");
        Command(weather, "数据源与定位…", "weather-settings");
        var animations = new ToolStripMenuItem("右下角动画");
        foreach (var (label, value) in new[] { ("天气机器人", "robot"), ("像素天气小屋", "house"), ("像素盆栽", "plant"), ("天气萌宠", "pet"), ("关闭动画", "off") })
        {
            var choice = new ToolStripMenuItem(label);
            choice.Click += (_, _) => MigratedWeather.WeatherMonitor.Animation = value;
            animations.DropDownOpening += (_, _) => choice.Checked = MigratedWeather.WeatherMonitor.Animation == value;
            animations.DropDownItems.Add(choice);
        }
        weather.DropDownItems.Add(animations);
        content.DropDownItems.Add(weather);

        var appearance = Group("桌宠与外观");
        Command(appearance, "打开 / 关闭设备镜像", "mirror");
        Command(appearance, "更换桌宠动画…（petdex）", "pet-gallery");
        Command(appearance, "两个角色使用同一本机动画…", "pet");
        var importPet = new ToolStripMenuItem("分别更换桌宠动画");
        Command(importPet, "Claude 选择本机动画…", "pet:claude");
        Command(importPet, "Codex 选择本机动画…", "pet:codex");
        appearance.DropDownItems.Add(importPet);
        var resetPet = new ToolStripMenuItem("恢复默认动画");
        Command(resetPet, "Claude 恢复默认", "pet-reset:claude");
        Command(resetPet, "Codex 恢复默认", "pet-reset:codex");
        appearance.DropDownItems.Add(resetPet);

        var service = Group("桥接服务");
        Command(service, "刷新状态", "refresh");
        var startup = new ToolStripMenuItem("开机启动");
        startup.Click += (_, _) => action("startup");
        service.DropDownOpening += (_, _) => startup.Checked = StartupRegistration.IsEnabled;
        service.DropDownItems.Add(startup);
        Command(service, "查看当前状态…", "status");
        Command(service, "确认任务完成提醒", "completion-ack");
        Command(service, "桥接服务地址…", "address");
        Command(service, "全部设置…", "settings");
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("退出", null, (_, _) => action("exit"));
        return menu;
    }
}
