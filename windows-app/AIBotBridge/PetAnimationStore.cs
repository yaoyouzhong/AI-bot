namespace AIBotBridge;

internal sealed class PetAnimationStore
{
    internal static PetAnimationStore Shared { get; } = new();
    private readonly object _sync = new();
    private PetAnimation? _animation;
    private ResourcePayload? _resource;
    private readonly Dictionary<string, PetAnimation> _legacy = new();
    private readonly Dictionary<string, PetAnimation> _selected = new();
    private readonly string _directory;
    private readonly Dictionary<string, string> _loadErrors = new();
    private readonly Dictionary<string, bool> _lastDraw = new();
    private long _lastRetry = long.MinValue;
    private readonly Dictionary<string, (long Tick, long Elapsed)> _clocks = new();
    private string CachePath => Path.Combine(_directory, "pet.apet");
    internal PetAnimationStore(string? directory = null)
    {
        _directory = directory ?? Path.Combine(AIBotBridge.AppPaths.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AI-bot");
        // Packaged development tools redirect AppData writes to LocalCache.
        // A normally launched bridge must recover those imports into its own profile.
        if (directory is null && !AppPaths.IsPublicSelfTest)
        {
            var packages = Path.Combine(AppPaths.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Packages");
            try {
                if (Directory.Exists(packages))
                    foreach (var package in Directory.EnumerateDirectories(packages, "OpenAI.Codex_*"))
                        RecoverRedirectedImports(Path.Combine(package, "LocalCache", "Local", "AI-bot"), _directory);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
        ReloadMissing();
        try { if (File.Exists(CachePath)) Set(PetAnimation.Decode(File.ReadAllBytes(CachePath))); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException) { }
    }
    internal static void RecoverRedirectedImports(string source, string destination)
    {
        foreach (var name in new[] { "claude-legacy.apet", "codex-legacy.apet", "claude-selected.apet", "codex-selected.apet", "pet.apet" })
        {
            var target = Path.Combine(destination, name);
            if (File.Exists(target)) continue; // Never replace user choices or damaged evidence.
            string? temporary = null;
            try {
                var bytes = File.ReadAllBytes(Path.Combine(source, name));
                _ = PetAnimation.Decode(bytes);
                Directory.CreateDirectory(destination);
                temporary = target + ".recovery-" + Guid.NewGuid().ToString("N");
                File.WriteAllBytes(temporary, bytes);
                File.Move(temporary, target, false); // Atomic, no overwrite if another import raced us.
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException) { }
            finally {
                if (temporary is not null) try { File.Delete(temporary); }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            }
        }
    }
    private string LegacyPath(string owner) => Path.Combine(_directory, owner + "-legacy.apet");
    internal void ReloadMissing()
    {
        lock (_sync)
        {
            foreach (var owner in new[] { "claude", "codex" })
            {
                foreach (var source in new[] { (Path: LegacyPath(owner), Store: _legacy), (Path: SelectedPath(owner), Store: _selected) })
                {
                    if (source.Store.ContainsKey(owner)) continue;
                    var name = Path.GetFileName(source.Path);
                    try { source.Store[owner] = PetAnimation.Decode(File.ReadAllBytes(source.Path)); _loadErrors.Remove(name); }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
                    { _loadErrors[name] = ex.GetType().Name; }
                }
            }
        }
    }
    // Read-only resident-process evidence; never includes sprite bytes or credentials.
    internal object Diagnostics()
    {
        lock (_sync) return new {
            processId = Environment.ProcessId, cacheDirectory = _directory,
            isolatedTestProfile = AppPaths.IsPublicSelfTest,
            pets = new[] { "claude", "codex" }.Select(owner => {
                var pet = Selection(owner);
                return new { owner, loaded = pet is not null, width = pet?.Width, height = pet?.Height,
                    frames = pet?.Frames.Length, lastDrawSucceeded = _lastDraw.TryGetValue(owner, out var drawn) ? (bool?)drawn : null };
            }).ToArray(), errors = new Dictionary<string,string>(_loadErrors)
        };
    }
    private string SelectedPath(string owner) => Path.Combine(_directory, owner + "-selected.apet");
    private static void ValidateOwner(string owner)
    {
        if (owner is not ("claude" or "codex")) throw new ArgumentException("Invalid owner.");
    }
    internal void Select(string owner, PetAnimation animation)
    {
        ValidateOwner(owner);
        var bytes = animation.Encode();
        lock (_sync)
        {
            Directory.CreateDirectory(_directory);
            var path = SelectedPath(owner);
            // Preserve the previous selection for recovery; never delete imports.
            if (File.Exists(path)) File.Copy(path, path + ".previous", true);
            File.WriteAllBytes(path + ".tmp", bytes);
            File.Move(path + ".tmp", path, true);
            _selected[owner] = animation;
            _clocks.Remove(owner);
        }
    }
    internal void RestoreDefault(string owner)
    {
        ValidateOwner(owner);
        lock (_sync)
        {
            if (!_legacy.TryGetValue(owner, out var animation))
                throw new InvalidOperationException("本机尚未迁入此角色的默认动画，原选择未改变。");
            Select(owner, animation);
        }
    }
    internal PetAnimation? Selection(string owner)
    {
        ValidateOwner(owner);
        lock (_sync) return _selected.GetValueOrDefault(owner) ?? _animation ?? _legacy.GetValueOrDefault(owner);
    }
    internal void SaveLegacy(string owner, PetAnimation animation)
    {
        if (owner is not ("claude" or "codex")) throw new ArgumentException("Invalid owner.");
        var path = LegacyPath(owner);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path + ".tmp", animation.Encode());
        File.Move(path + ".tmp", path, true);
        lock (_sync) _legacy[owner] = animation;
    }
    internal IReadOnlyList<ResourcePayload> AllResources()
    {
        lock (_sync)
        {
            var result = new List<ResourcePayload>();
            foreach (var owner in new[] { "claude", "codex" })
            {
                var animation = Selection(owner);
                if (animation is null) continue;
                var bytes = animation.Encode();
                result.Add(new(owner == "claude" ? BinaryResourceKind.ClaudePetAnimation : BinaryResourceKind.CodexPetAnimation,
                    unchecked((int)BinaryResourceProtocol.Crc32(bytes)), bytes));
            }
            return result;
        }
    }
    internal void Save(PetAnimation animation)
    {
        var bytes = animation.Encode();
        Directory.CreateDirectory(Path.GetDirectoryName(CachePath)!);
        File.WriteAllBytes(CachePath + ".tmp", bytes); File.Move(CachePath + ".tmp", CachePath, true);
        Set(animation);
        Select("claude", animation);
        Select("codex", animation);
    }
    private void Set(PetAnimation animation)
    {
        var bytes = animation.Encode();
        lock (_sync) { _animation = animation; _resource = new(BinaryResourceKind.PetAnimation, unchecked((int)BinaryResourceProtocol.Crc32(bytes)), bytes); }
    }
    internal IReadOnlyList<ResourcePayload> Resources { get { lock (_sync) return _resource is null ? [] : [_resource]; } }
    internal bool Draw(Graphics graphics, int x, int y, string owner = "codex", bool animate = true)
    {
        PetAnimation? animation; long elapsed;
        lock (_sync)
        {
            animation = Selection(owner);
            long now = Environment.TickCount64;
            if (animation is null && (_lastRetry == long.MinValue || now - _lastRetry >= 5000))
            {
                _lastRetry = now;
                ReloadMissing();
                animation = Selection(owner);
            }
            _lastDraw[owner] = animation is not null;
            var clock = _clocks.GetValueOrDefault(owner, (Tick: now, Elapsed: 0L));
            // Hidden pages and idle states do not advance their walk cycle.
            elapsed = clock.Elapsed + (animate ? Math.Clamp(now - clock.Tick, 0, 250) : 0);
            _clocks[owner] = (now, elapsed);
        }
        if (animation is null) return false;
        using var bitmap = animation.BitmapAt(elapsed); graphics.DrawImageUnscaled(bitmap, x + 56 - animation.Width / 2, y + 56 - animation.Height / 2); return true;
    }
}
