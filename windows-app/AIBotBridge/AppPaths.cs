namespace AIBotBridge;

// Only the exclusive public self-test entry point can opt into a fresh profile.
// Normal startup continues to use Windows known folders, not environment overrides.
internal static class AppPaths
{
    internal static bool IsPublicSelfTest { get; private set; }
    private static string? _root;

    internal static void BeginPublicSelfTest()
    {
        if (_root is not null) throw new InvalidOperationException("Test profile already initialized.");
        _root = Path.Combine(Environment.CurrentDirectory, "artifacts", "public-profile-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        IsPublicSelfTest = true;
    }

    internal static string GetFolderPath(Environment.SpecialFolder folder)
    {
        if (!IsPublicSelfTest) return Environment.GetFolderPath(folder);
        var name = folder switch
        {
            Environment.SpecialFolder.UserProfile => "home",
            Environment.SpecialFolder.ApplicationData => "roaming",
            Environment.SpecialFolder.LocalApplicationData => "local",
            _ => throw new InvalidOperationException("Unmapped test profile folder.")
        };
        var path = Path.Combine(_root!, name);
        Directory.CreateDirectory(path);
        return path;
    }

    internal static string? GetProviderEnvironmentVariable(string name) =>
        IsPublicSelfTest ? null : Environment.GetEnvironmentVariable(name);
}
