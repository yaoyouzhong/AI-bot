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
    private byte[] _tab5CoverBitmap = [];
    private byte[] _coverSource = [];
    private JpegCover? _jpegCover;
    private readonly BrowserMusicArtwork _artwork = new();
    private readonly SemaphoreSlim _refreshGate=new(1,1);
    internal event Action? ArtworkChanged;
    internal async Task AcceptBrowserArtworkAsync(string json,CancellationToken token) {
        if(await _artwork.AcceptAsync(json,token))await RefreshAsync(token);
    }
    private CoverImages? _renderedCover;
    internal sealed record JpegCover(byte[] Bytes,int Width,int Height);
    internal sealed record CoverImages(byte[] Legacy, byte[] Tab5,int SourceWidth,int SourceHeight,JpegCover Jpeg);
    internal string ArtworkSourceSize => _renderedCover is {} cover?$"{cover.SourceWidth}x{cover.SourceHeight}":"none";
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
                if (_coverRevision > 0 && _coverBitmap.Length>0) result.Add(new(BinaryResourceKind.MusicCover, _coverRevision, _coverBitmap));
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
        await _refreshGate.WaitAsync(cancellationToken);
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
            var timelineAvailable=duration>0 || timeline.LastUpdatedTime.Year>2000;
            if(duration<=0) {
                var localDuration=await Task.Run(()=>NeteaseLocalDuration.Read(session.SourceAppUserModelId,
                    title,artist,properties?.AlbumTitle?.Trim()??""),cancellationToken);
                if(localDuration is {} seconds)duration=seconds;
            }
            var resourceKey = session.SourceAppUserModelId + "\n" + title + "\n" + artist + "\n" + properties?.AlbumTitle;
            var coverBitmap = _artwork.Find(title,artist) is {} linked
                ? RenderCoverBytes(linked) : await RenderCoverBitmapAsync(properties?.Thumbnail);
            ApplySample(new MusicSnapshot(title, artist,
                    properties?.AlbumTitle?.Trim() ?? string.Empty, playing &&
                    !(duration > 0 && elapsed >= duration - 0.25), elapsed, duration, DateTimeOffset.UtcNow){TimelineAvailable=timelineAvailable,
                        ArtworkWidth=coverBitmap?.SourceWidth??0,ArtworkHeight=coverBitmap?.SourceHeight??0},resourceKey,coverBitmap?.Legacy,coverBitmap?.Tab5,coverBitmap?.Jpeg);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or InvalidOperationException or COMException)
        {
            ApplyEmpty();
        }
        finally{_refreshGate.Release();}
    }

    internal void ApplySample(MusicSnapshot sample,string key,byte[]? cover,byte[]? tab5Cover=null,JpegCover? jpeg=null)
    {
        bool changed;
        lock(_sync)
        {
            changed=key!=_resourceKey||!_coverBitmap.AsSpan().SequenceEqual(cover??[])||!_tab5CoverBitmap.AsSpan().SequenceEqual(tab5Cover??[]);
            _emptySamples=0;
            if(key!=_resourceKey){_resourceKey=key;_textBitmap=RenderTextBitmap(sample.Title,sample.Artist);_textRevision++;}
            var bytes=cover??[];
            if(!_coverBitmap.AsSpan().SequenceEqual(bytes)){_coverBitmap=bytes;_coverRevision++;}
            var large=tab5Cover??[];
            if(!_tab5CoverBitmap.AsSpan().SequenceEqual(large))_tab5CoverBitmap=large;
            if(jpeg is null)_jpegCover=null;
            else if(_jpegCover is null||!_jpegCover.Bytes.AsSpan().SequenceEqual(jpeg.Bytes))_jpegCover=jpeg;
            // Stable references prevent recompressing unchanged artwork every status tick.
            _snapshot=sample with{CoverRgb565=_coverBitmap.Length>0?_coverBitmap:null,
                Tab5CoverRgb565=_tab5CoverBitmap.Length>0?_tab5CoverBitmap:null,Tab5CoverJpeg=_jpegCover};
        }
        if(changed)ArtworkChanged?.Invoke();
    }

    internal void ApplyEmpty()
    {
        lock (_sync)
        {
            _emptySamples++;
            if (_emptySamples < 3 && _snapshot is not null) return;
            if(_resourceKey.Length>0){_resourceKey="";_textBitmap=RenderTextBitmap("No Music","");_textRevision++;}
            _coverBitmap=[];_tab5CoverBitmap=[];_jpegCover=null;_coverSource=[];_renderedCover=null;
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

    private async Task<CoverImages?> RenderCoverBitmapAsync(IRandomAccessStreamReference? reference)
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
            return RenderCoverBytes(bytes);
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or COMException)
        {
            return null;
        }
    }

    private CoverImages RenderCoverBytes(byte[] bytes) {
        if(_renderedCover is not null&&_coverSource.AsSpan().SequenceEqual(bytes))return _renderedCover;
        using var stream=new MemoryStream(bytes);using var source=Image.FromStream(stream);
        var rendered=RenderCoverImages(source);_coverSource=bytes;_renderedCover=rendered;return rendered;
    }
    internal static CoverImages RenderCoverImages(Image source) => new(RenderCover(source,112,crop:true),RenderCover(source,336,crop:false),source.Width,source.Height,RenderJpeg(source));
    private static JpegCover RenderJpeg(Image source) {
        double scale=Math.Min(560.0/source.Width,336.0/source.Height);
        int width=Math.Max(1,(int)Math.Round(source.Width*scale)),height=Math.Max(1,(int)Math.Round(source.Height*scale));
        using var target=new Bitmap(width,height,System.Drawing.Imaging.PixelFormat.Format24bppRgb);
        using(var graphics=Graphics.FromImage(target)) {
            graphics.Clear(Color.Black);graphics.InterpolationMode=InterpolationMode.HighQualityBicubic;
            graphics.PixelOffsetMode=PixelOffsetMode.HighQuality;graphics.DrawImage(source,0,0,width,height);
        }
        using var stream=new MemoryStream();using var quality=new System.Drawing.Imaging.EncoderParameters(1);
        quality.Param[0]=new(System.Drawing.Imaging.Encoder.Quality,88L);
        target.Save(stream,System.Drawing.Imaging.ImageCodecInfo.GetImageEncoders().Single(c=>c.FormatID==System.Drawing.Imaging.ImageFormat.Jpeg.Guid),quality);
        return new(stream.ToArray(),width,height);
    }

    private static byte[] RenderCover(Image source,int size,bool crop)
    {
        using var target=new Bitmap(size,size,System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using(var graphics=Graphics.FromImage(target)) {
            graphics.Clear(Color.Black);
            graphics.InterpolationMode=InterpolationMode.HighQualityBicubic;
            graphics.PixelOffsetMode=PixelOffsetMode.HighQuality;
            // Browser media artwork can be a small landscape thumbnail. Keep it
            // whole on TAB5 instead of cropping and magnifying its short edge.
            var scale=crop?Math.Max((double)size/source.Width,(double)size/source.Height)
                :Math.Min((double)size/source.Width,(double)size/source.Height);
            float width=(float)(source.Width*scale),height=(float)(source.Height*scale);
            graphics.DrawImage(source,(size-width)/2,(size-height)/2,width,height);
        }
        return ToRgb565(target);
    }

    private static byte[] ToRgb565(Bitmap bitmap)
    {
        var result = new byte[bitmap.Width * bitmap.Height * 2];
        var offset = 0;
        var data=bitmap.LockBits(new Rectangle(0,0,bitmap.Width,bitmap.Height),
            System.Drawing.Imaging.ImageLockMode.ReadOnly,System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        try {
            var row=new byte[bitmap.Width*4];
            for(var y=0;y<bitmap.Height;y++) {
                Marshal.Copy(IntPtr.Add(data.Scan0,y*data.Stride),row,0,row.Length);
                for(var x=0;x<bitmap.Width;x++) {
                    int p=x*4;
                    var value=(ushort)(((row[p+2]&0xF8)<<8)|((row[p+1]&0xFC)<<3)|(row[p]>>3));
                    result[offset++]=(byte)value;result[offset++]=(byte)(value>>8);
                }
            }
        } finally {bitmap.UnlockBits(data);}
        return result;
    }
}
