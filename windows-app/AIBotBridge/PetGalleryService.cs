using System.Text.Json;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace AIBotBridge;

internal sealed record GalleryPet(string Slug, string Name, Uri Sheet)
{
    public override string ToString() => $"{Name}（{Slug}）";
}
internal sealed record GalleryMotion(string Label, int Row, int Frames, int Duration)
{
    public override string ToString() => Label;
}
internal sealed record GallerySheet(int Width, int Height, byte[] Pixels);

// Independent adapter for the public v1 manifest; no upstream source/art is bundled.
internal static class PetGalleryService
{
    internal static readonly GalleryMotion[] Motions = [
        new("待机 Idle",0,6,1100), new("右跑 Run Right",1,8,1060), new("左跑 Run Left",2,8,1060),
        new("挥手 Waving",3,4,700), new("跳跃 Jumping",4,5,840), new("失败 Failed",5,8,1220),
        new("等待 Waiting",6,6,1010), new("原地跑 Running",7,6,820), new("思考 Review",8,6,1030)];
    private static readonly HttpClient Http = new(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(45) };
    private const string Manifest = "https://assets.petdex.dev/manifests/petdex-v1.json";
    internal static bool Allowed(Uri uri) => uri.Scheme == "https" && uri.Host == "assets.petdex.dev" && uri.IsDefaultPort && uri.UserInfo.Length == 0;
    private static async Task<byte[]> Download(Uri uri, int limit, CancellationToken token)
    {
        if (!Allowed(uri)) throw new InvalidDataException("图库资源地址不在允许的来源内。");
        using var response = await Http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, token);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength > limit) throw new InvalidDataException("图库资源过大。");
        using var input = await response.Content.ReadAsStreamAsync(token);
        using var output = new MemoryStream(); var buffer = new byte[16384]; int read;
        while ((read = await input.ReadAsync(buffer, token)) > 0)
        {
            if (output.Length + read > limit) throw new InvalidDataException("图库资源过大。");
            output.Write(buffer, 0, read);
        }
        return output.ToArray();
    }
    internal static GalleryPet[] Parse(byte[] bytes)
    {
        using var doc = JsonDocument.Parse(bytes);
        var result = new List<GalleryPet>();
        foreach (var item in doc.RootElement.GetProperty("pets").EnumerateArray())
        {
            var slug = item.GetProperty("slug").GetString();
            var url = item.GetProperty("spritesheetUrl").GetString();
            if (string.IsNullOrWhiteSpace(slug) || !Uri.TryCreate(url, UriKind.Absolute, out var uri) || !Allowed(uri)) continue;
            result.Add(new(slug, item.TryGetProperty("displayName", out var name) ? name.GetString() ?? slug : slug, uri));
        }
        if (result.Count == 0) throw new InvalidDataException("图库列表为空，保留已有缓存。");
        return result.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase).ToArray();
    }
    internal static async Task<(GalleryPet[] Pets, bool Cached)> Load(CancellationToken token)
    {
        var directory = Path.Combine(AIBotBridge.AppPaths.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AI-bot");
        var cache = Path.Combine(directory, "petdex-v1.json");
        try
        {
            var bytes = await Download(new Uri(Manifest), 8 * 1024 * 1024, token);
            var pets = Parse(bytes);
            // Cache write failure must not hide a successfully downloaded list.
            try { Directory.CreateDirectory(directory); await File.WriteAllBytesAsync(cache + ".tmp", bytes, token); File.Move(cache + ".tmp", cache, true); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            return (pets, false);
        }
        catch (Exception ex) when (!token.IsCancellationRequested && ex is HttpRequestException or IOException or JsonException or TaskCanceledException)
        {
            var candidates = new[] { cache, Path.Combine(AIBotBridge.AppPaths.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AIClockBridge", "petdex-v1.json") };
            foreach (var path in candidates)
                try { if (new FileInfo(path).Length <= 8 * 1024 * 1024) return (Parse(await File.ReadAllBytesAsync(path, token)), true); }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException) { }
            throw new IOException("图库下载失败，且没有可用列表缓存。", ex);
        }
    }
    internal static async Task<GallerySheet> LoadSheet(GalleryPet pet, CancellationToken token)
    {
        var bytes = await Download(pet.Sheet, 20 * 1024 * 1024, token);
        using var stream = new InMemoryRandomAccessStream();
        using (var writer = new DataWriter(stream)) { writer.WriteBytes(bytes); await writer.StoreAsync(); writer.DetachStream(); }
        stream.Seek(0);
        var decoder = await BitmapDecoder.CreateAsync(stream);
        if (decoder.PixelWidth != 1536 || decoder.PixelHeight is not (1872 or 2288)) throw new InvalidDataException($"不支持的图库尺寸 {decoder.PixelWidth}×{decoder.PixelHeight}，未改变当前桌宠。");
        token.ThrowIfCancellationRequested();
        var pixels = await decoder.GetPixelDataAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Straight, new BitmapTransform(), ExifOrientationMode.IgnoreExifOrientation, ColorManagementMode.DoNotColorManage);
        token.ThrowIfCancellationRequested();
        return new(1536,(int)decoder.PixelHeight,pixels.DetachPixelData());
    }
    internal static PetAnimation Animate(GallerySheet sheet, GalleryMotion motion, string owner)
    {
        if (owner is not ("claude" or "codex") || sheet.Width != 1536 || sheet.Height is not (1872 or 2288) || sheet.Pixels.Length != 1536 * sheet.Height * 4 || motion.Row is < 0 or > 8 || motion.Frames is < 1 or > 8)
            throw new InvalidDataException("Invalid gallery animation.");
        int width = owner == "claude" ? 111 : 120, height = 120;
        double scale = Math.Min(width / 192.0, height / 208.0);
        int dw = (int)Math.Round(192 * scale), dh = (int)Math.Round(208 * scale);
        var frames = new byte[motion.Frames][];
        for (int frame = 0; frame < frames.Length; frame++)
        {
            var target = new byte[width * height * 2];
            for (int y = 0; y < dh; y++) for (int x = 0; x < dw; x++)
            {
                int source = ((motion.Row * 208 + y * 208 / dh) * sheet.Width + frame * 192 + x * 192 / dw) * 4;
                int alpha = sheet.Pixels[source + 3];
                int r = sheet.Pixels[source + 2] * alpha / 255, g = sheet.Pixels[source + 1] * alpha / 255, b = sheet.Pixels[source] * alpha / 255;
                int rgb = ((r & 248) << 8) | ((g & 252) << 3) | (b >> 3);
                int offset = (((height - dh) / 2 + y) * width + (width - dw) / 2 + x) * 2;
                target[offset] = (byte)rgb; target[offset+1] = (byte)(rgb >> 8);
            }
            frames[frame] = target;
        }
        return new(Enumerable.Repeat((ushort)Math.Max(50, motion.Duration / motion.Frames / 10 * 10), frames.Length).ToArray(), frames, width, height);
    }
}
