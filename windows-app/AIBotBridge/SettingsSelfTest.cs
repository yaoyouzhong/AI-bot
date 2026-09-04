namespace AIBotBridge;

internal static class SettingsSelfTest
{
    internal static void Run(string output)
    {
        var settings = BridgeSettings.CreatePublicSelfTestSettings();
        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["weather_city"] = "上海",
            ["weather_latitude"] = "31.2304",
            ["weather_longitude"] = "121.4737",
            ["stock_symbols"] = "sh000001,hk00700,usAAPL",
            ["screensaver_timeout_minutes"] = "10",
            ["serial_port"] = "COM7"
        };
        var dataDirectory = Path.Combine(Path.GetDirectoryName(output)!, "settings-self-test-data");
        if (!settings.SaveEditable(values, out var error, dataDirectory))
            throw new InvalidOperationException(error);

        var reloaded = BridgeSettings.LoadCurrentFromDirectory(dataDirectory);
        foreach (var expected in values)
            if (!reloaded.Get(expected.Key).Equals(expected.Value, StringComparison.Ordinal))
                throw new InvalidOperationException($"Setting did not round-trip: {expected.Key}");

        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        using var form = new SettingsForm(reloaded);
        form.ShowInTaskbar = false;
        form.StartPosition = FormStartPosition.Manual;
        form.Location = new Point(-32000, -32000);
        form.Show();
        Application.DoEvents();
        using var image = new Bitmap(form.ClientSize.Width, form.ClientSize.Height);
        form.DrawToBitmap(image, form.ClientRectangle);
        form.Hide();
        image.Save(output);
        Console.WriteLine($"SETTINGS_SELF_TEST_OK path={output}");
    }
}
