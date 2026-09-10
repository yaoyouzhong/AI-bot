namespace AIBotBridge;

internal static class TrayMenuSelfTest
{
    internal static void Run()
    {
        using var icon = AppIcon.Load();
        if (icon.Width != 32 || icon.Height != 32) throw new InvalidOperationException("Robot tray icon did not load.");
        string? command = null;
        var mode = "weather";
        using var menu = TrayMenu.Build(value => command = value, value => mode = value,
            () => mode, () => "已连接：COM7（USB）");
        var headings = menu.Items.OfType<ToolStripMenuItem>().Select(item => item.Text);
        if (!headings.SequenceEqual(new[] { "模型额度", "设备连接", "显示模式", "循环展示",
                "内容设置", "桌宠与外观", "桥接服务", "退出" }))
            throw new InvalidOperationException("Legacy menu grouping/order changed.");
        var display = (ToolStripMenuItem)menu.Items[2];
        ((ToolStripMenuItem)menu.Items[0]).DropDownItems.OfType<ToolStripMenuItem>().Single(i => i.Text == "Codex 额度趋势…").PerformClick();
        if (command != "quota-trend") throw new InvalidOperationException("Quota trend route failed.");
        var weather = display.DropDownItems.OfType<ToolStripMenuItem>().Single(item => item.Tag as string == "weather");
        weather.PerformClick();
        if (mode != "weather") throw new InvalidOperationException("Weather route failed.");
        var quotas = display.DropDownItems.OfType<ToolStripMenuItem>().Single(item => item.Tag as string == "dual");
        quotas.PerformClick();
        if (mode != "dual") throw new InvalidOperationException("Quota route must select the dual quota page.");
        var appearance = (ToolStripMenuItem)menu.Items[5];
        var cycle = (ToolStripMenuItem)menu.Items[3];
        cycle.DropDownItems[0].PerformClick();
        if (command != "cycle:toggle") throw new InvalidOperationException("Cycle toggle route failed.");
        cycle.DropDownItems.OfType<ToolStripMenuItem>().Single(i => i.Text == "天气时钟").PerformClick();
        if (command != "cycle:page:weather") throw new InvalidOperationException("Cycle page route failed.");
        var intervals = cycle.DropDownItems.OfType<ToolStripMenuItem>().Single(i => i.Text == "切换间隔");
        intervals.DropDownItems[2].PerformClick();
        if (command != "cycle:interval:30") throw new InvalidOperationException("Cycle interval route failed.");
        appearance.DropDownItems[0].PerformClick();
        if (command != "mirror") throw new InvalidOperationException("Mirror route failed.");
        appearance.DropDownItems.OfType<ToolStripMenuItem>().Single(i => i.Text == "更换桌宠动画…（petdex）").PerformClick();
        if (command != "pet-gallery") throw new InvalidOperationException("Petdex gallery route failed.");
        var resetPet = appearance.DropDownItems.OfType<ToolStripMenuItem>().Single(i => i.Text == "恢复默认动画");
        resetPet.DropDownItems[0].PerformClick();
        if (command != "pet-reset:claude") throw new InvalidOperationException("Claude reset route failed.");
        resetPet.DropDownItems[1].PerformClick();
        if (command != "pet-reset:codex") throw new InvalidOperationException("Codex reset route failed.");
        var selectPet = appearance.DropDownItems.OfType<ToolStripMenuItem>().Single(i => i.Text == "分别更换桌宠动画");
        selectPet.DropDownItems[1].PerformClick();
        if (command != "pet:codex") throw new InvalidOperationException("Codex import route failed.");
        var content = (ToolStripMenuItem)menu.Items[4];
        content.DropDownItems[0].PerformClick();
        if (command != "stocks-settings") throw new InvalidOperationException("Stock settings route failed.");
        var usb = ((ToolStripMenuItem)menu.Items[1]).DropDownItems.OfType<ToolStripMenuItem>().Last();
        usb.DropDownItems[0].PerformClick();
        if (command != "info") throw new InvalidOperationException("USB info route failed.");
        var output = Path.Combine(Environment.CurrentDirectory, "artifacts", "tray-menu-self-test.png");
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        menu.Show(new Point(-30000, -30000));
        Application.DoEvents();
        using var bitmap = new Bitmap(menu.Width, menu.Height);
        menu.DrawToBitmap(bitmap, new Rectangle(Point.Empty, menu.Size));
        menu.Close();
        bitmap.Save(output);
        Console.WriteLine("TRAY_MENU_SELF_TEST_OK " + output + " (notification-area visibility needs user confirmation)");
    }
}
