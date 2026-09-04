using Windows.Media.Control;
using Windows.Storage.Streams;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;

namespace AIBotBridge;

internal sealed class NowPlayingService
{
    private readonly object _sync = new();
    private GlobalSystemMediaTransportControlsSessionManager? _manager;
    private MusicSnapshot? _snapshot;
    private int _emptySamples;
    private string _resourceKey = string.Empty;
    private byte[] _textBitmap = Array.Empty<byte>();
    private byte[] _coverBitmap = Array.Empty<byte>();
    private int _textRevision;
    private int _coverRevision;

    internal MusicSnapshot? Snapshot
    {
        get { lock (_sync) return _snapshot; }
    }

    internal IReadOnlyList<ResourcePayload> Resources
    {
        get
        {
            lock (_sync)
            {
                var result = new List<ResourcePayload>(2);
                if (_textRevision > 0) result.Add(new(BinaryResourceKind.TextBitmap, _textRevision, _textBitmap));
                if (_coverRevision > 0) result.Add(new(BinaryResourceKind.MusicCover, _coverRevision, _coverBitmap));
                return result;
            }
        }
    }

    internal async Task RunAsync(CancellationToken cancellationToken)
    {
        await RefreshAsync(cancellationToken);
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
        while (await timer.WaitForNextTickAsync(cancellationToken))
            await RefreshAsync(cancellationToken);
    }

    internal async Task RefreshAsync(CancellationToken cancellationToken)
    {
        try
        {
            _manager ??= await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            cancellationToken.ThrowIfCancellationRequested();
            var session = _manager.GetCurrentSession();
            if (session is null)
            {
                ApplyEmpty();
                return;
            }

            var properties = await session.TryGetMediaPropertiesAsync();
            cancellationToken.ThrowIfCancellationRequested();
            var timeline = session.GetTimelineProperties();
            var playback = session.GetPlaybackInfo();
            var title = properties?.Title?.Trim() ?? string.Empty;
            if (title.Length == 0)
            {
                ApplyEmpty();
                return;
            }

            var duration = Math.Max(0, (timeline.EndTime - timeline.StartTime).TotalSeconds);
            var elapsed = Math.Max(0, timeline.Position.TotalSeconds);
            var playing = playback?.PlaybackStatus ==
                          GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
            if (playing && timeline.LastUpdatedTime.Year > 2000)
                elapsed += (DateTimeOffset.UtcNow - timeline.LastUpdatedTime).TotalSeconds;
            if (duration > 0) elapsed = Math.Clamp(elapsed, 0, duration);

            var artist = properties?.Artist?.Trim() ?? string.Empty;
            var resourceKey = title + "\n" + artist;
            byte[]? textBitmap = null;
            byte[]? coverBitmap = null;
            lock (_sync)
                if (resourceKey != _resourceKey) textBitmap = RenderTextBitmap(title, artist);
            if (textBitmap is not null)
                coverBitmap = await RenderCoverBitmapAsync(properties?.Thumbnail);

            lock (_sync)
            {
                _emptySamples = 0;
                _snapshot = new MusicSnapshot(title, artist,
                    properties?.AlbumTitle?.Trim() ?? string.Empty, playing &&
                    !(duration > 0 && elapsed >= duration - 0.25), elapsed, duration, DateTimeOffset.UtcNow);
                if (textBitmap is not null && resourceKey != _resourceKey)
                {
                    _resourceKey = resourceKey;
                    _textBitmap = textBitmap;
                    _coverBitmap = coverBitmap ?? new byte[112 * 112 * 2];
                    _textRevision++;
                    _coverRevision++;
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or InvalidOperationException or COMException)
        {
            ApplyEmpty();
        }
    }

    private void ApplyEmpty()
    {
        lock (_sync)
        {
            _emptySamples++;
            if (_emptySamples < 3 && _snapshot is not null) return;
            _snapshot = new MusicSnapshot(string.Empty, string.Empty, string.Empty, false,
                0, 0, DateTimeOffset.UtcNow);
        }
    }

    internal static byte[] RenderTextBitmap(string title, string artist)
    {
        using var bitmap = new Bitmap(232, 44, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.Black);
        graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        using var format = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center,
            Trimming = StringTrimming.EllipsisCharacter,
            FormatFlags = StringFormatFlags.NoWrap
        };
        using var titleFont = new Font("Microsoft YaHei UI", 16, FontStyle.Bold, GraphicsUnit.Pixel);
        using var artistFont = new Font("Microsoft YaHei UI", 12, FontStyle.Regular, GraphicsUnit.Pixel);
        using var titleBrush = new SolidBrush(Color.White);
        using var artistBrush = new SolidBrush(Color.FromArgb(184, 184, 184));
        graphics.DrawString(title, titleFont, titleBrush, new RectangleF(2, 2, 228, 23), format);
        graphics.DrawString(artist, artistFont, artistBrush, new RectangleF(2, 27, 228, 15), format);
        return ToRgb565(bitmap);
    }

    private static async Task<byte[]?> RenderCoverBitmapAsync(IRandomAccessStreamReference? reference)
    {
        if (reference is null) return null;
        try
        {
            using var stream = await reference.OpenReadAsync();
            if (stream.Size is 0 or > 10_000_000) return null;
            using var reader = new DataReader(stream.GetInputStreamAt(0));
            await reader.LoadAsync((uint)stream.Size);
            var bytes = new byte[stream.Size];
            reader.ReadBytes(bytes);
            using var sourceStream = new MemoryStream(bytes);
            using var source = Image.FromStream(sourceStream);
            using var target = new Bitmap(112, 112, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using var graphics = Graphics.FromImage(target);
            graphics.Clear(Color.Black);
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            var scale = Math.Max(112.0 / source.Width, 112.0 / source.Height);
            var width = (float)(source.Width * scale);
            var height = (float)(source.Height * scale);
            graphics.DrawImage(source, (112 - width) / 2, (112 - height) / 2, width, height);
            return ToRgb565(target);
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or COMException)
        {
            return null;
        }
    }

    private static byte[] ToRgb565(Bitmap bitmap)
    {
        var result = new byte[bitmap.Width * bitmap.Height * 2];
        var offset = 0;
        for (var y = 0; y < bitmap.Height; y++)
        for (var x = 0; x < bitmap.Width; x++)
        {
            var color = bitmap.GetPixel(x, y);
            var value = (ushort)(((color.R & 0xF8) << 8) | ((color.G & 0xFC) << 3) | (color.B >> 3));
            result[offset++] = (byte)value;
            result[offset++] = (byte)(value >> 8);
        }
        return result;
    }
}
