namespace AIBotBridge;

internal static class AppIcon
{
    internal static Icon Load()
    {
        using var stream = typeof(AppIcon).Assembly.GetManifestResourceStream("AIBotBridge.Assets.app-icon.ico")
            ?? throw new InvalidOperationException("Embedded robot icon is missing.");
        using var source = new Icon(stream, new Size(32, 32));
        return (Icon)source.Clone();
    }
}
