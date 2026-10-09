using Windows.Media.Control;
using Windows.Storage.Streams;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;

namespace AIBotBridge;

internal sealed class NowPlayingService : IDisposable
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
    private readonly NeteasePlaybackReader _netease = new();
    private readonly QqMusicPlaybackReader _qqmusic = new();
    private readonly QqMusicArtwork _qqArtwork = new();
    private string _selectedSource = "";
    private string _diagnostic = "not sampled";
    internal string Diagnostic => Volatile.Read(ref _diagnostic);
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
    private long _sampleTick;
    internal Func<long> PlaybackClock { get; set; } = () => Environment.TickCount64;

    internal MusicSnapshot? Snapshot
    {
        get { lock (_sync) return _snapshot is {} sample ? ProjectClock(sample, (PlaybackClock()-_sampleTick)/1000.0) : null; }
    }

    // Advance only a recently observed, valid playing clock. Pause, unknown data
    // and stale samples must never turn into an invented playback position.
    internal static MusicSnapshot ProjectClock(MusicSnapshot sample,double age) =>
        sample.TimelineAvailable && sample.Playing && age is >= 0 and <= 3
            ? sample with { ElapsedSeconds=sample.DurationSeconds>0
                ? Math.Min(sample.DurationSeconds,sample.ElapsedSeconds+age*sample.PlaybackRate) : sample.ElapsedSeconds+age*sample.PlaybackRate }
            : age>3 ? sample with {TimelineAvailable=false} : sample;

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
        try
        {
            await RefreshAsync(cancellationToken);
            using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(500));
            while (await timer.WaitForNextTickAsync(cancellationToken))
                await RefreshAsync(cancellationToken);
        }
        finally
        {
            await _refreshGate.WaitAsync();
            try { Dispose(); } finally { _refreshGate.Release(); }
        }
    }

    internal async Task RefreshAsync(CancellationToken cancellationToken)
    {
        await _refreshGate.WaitAsync(cancellationToken);
        Task<(QqMusicPlaybackReader.Sample? Sample,DateTimeOffset At)>? qqTask=null;
        try
        {
            // Native QQ Music can work without an SMTC plugin. Read it alongside
            // all public system sessions so a paused default session cannot hide
            // another application's actual playback.
            qqTask=Task.Run(()=>{var sample=_qqmusic.Read();return (Sample:sample,At:DateTimeOffset.UtcNow);},cancellationToken);
            var candidates=new List<MediaPlaybackSource>();string systemDiagnostic="";
            try {
                if(_manager is null){using var request=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);request.CancelAfter(TimeSpan.FromSeconds(2));
                    try{_manager=await GlobalSystemMediaTransportControlsSessionManager.RequestAsync().AsTask(request.Token);}
                    catch(OperationCanceledException) when(!cancellationToken.IsCancellationRequested){systemDiagnostic="SMTC request timed out";}}
                if(_manager is not null){
                    var current=_manager.GetCurrentSession();
                    var sessions=_manager.GetSessions().Take(16).ToArray();
                    var readings=await Task.WhenAll(sessions.Select(s=>ReadSystemAsync(s,ReferenceEquals(s,current),cancellationToken)));
                    candidates.AddRange(readings.OfType<MediaPlaybackSource>());
                }
            }catch(Exception ex) when(ex is UnauthorizedAccessException or InvalidOperationException or COMException){systemDiagnostic="SMTC unavailable: "+ex.GetType().Name;}
            var qq=await qqTask;
            MediaPlaybackSource.AddQq(candidates,qq.Sample,qq.At,_qqmusic.Diagnostic);
            var browserSources=_artwork.PlaybackSources();
            foreach(var browser in browserSources) {
                var matches=candidates.Where(s=>IsBrowser(s.Source)&&s.Music.Title==browser.Music.Title&&s.Music.Artist==browser.Music.Artist&&
                    (s.Music.Album.Length==0||s.Music.Album==browser.Music.Album)).ToArray();
                // Only one enabled tab may enrich a matching system session.
                bool unique=browserSources.Count(s=>s.Music.Title==browser.Music.Title&&s.Music.Artist==browser.Music.Artist)==1;
                if(unique&&matches.Length==1){var matching=matches[0];candidates.Remove(matching);candidates.Add(browser with {SystemCurrent=matching.SystemCurrent,Thumbnail=matching.Thumbnail});}
                else candidates.Add(browser);
            }
            var selected=MediaPlaybackSource.Select(candidates,_selectedSource);
            if(selected is null){_diagnostic="no active music; "+systemDiagnostic+"; "+_qqmusic.Diagnostic;ApplyEmpty();return;}
            _selectedSource=selected.Source;var music=selected.Music;
            // Native NetEase enriches only the selected player's exact song.
            if(NeteasePlaybackReader.IsNetease(selected.Source)) {
                var native=await Task.Run(()=>_netease.Read(selected.Source,music.Title,music.Artist,music.Album),cancellationToken);
                if(native is not null)selected=selected with {
                    Music=music with {Title=native.Track.DisplayTitle,Artist=native.Track.Artist,Album=music.Album.Length==0?native.Track.Album:music.Album,
                        ElapsedSeconds=music.TimelineAvailable?music.ElapsedSeconds:native.Clock.Elapsed,
                        DurationSeconds=music.TimelineAvailable?music.DurationSeconds:native.Clock.Duration,
                        Playing=music.TimelineAvailable?music.Playing:native.Clock.Playing,TimelineAvailable=true},
                    CompactTitle=native.Track.CompactTitle,Diagnostic=selected.Diagnostic+"; "+_netease.Diagnostic};
                else if(music.DurationSeconds<=0&&await Task.Run(()=>NeteaseLocalDuration.Read(selected.Source,music.Title,music.Artist,music.Album),cancellationToken) is {} duration)
                    selected=selected with {Music=music with {DurationSeconds=duration}};
            }
            long sampleTick=PlaybackClock();music=selected.Music;
            // Account for time spent collecting another source, once only.
            music=ProjectClock(music,Math.Max(0,(DateTimeOffset.UtcNow-music.UpdatedAt).TotalSeconds)) with {UpdatedAt=DateTimeOffset.UtcNow};
            CoverImages? coverBitmap=await RenderCoverBitmapAsync(selected.Thumbnail,cancellationToken);
            if(QqMusicPlaybackReader.IsQq(selected.Source)) {
                if(_qqArtwork.Read(selected.ResourceKey,selected.CoverUrl,cancellationToken) is {} bytes)
                    coverBitmap=TryRenderCover(bytes)??coverBitmap;
            }else {
                _qqArtwork.Dispose();
                if(IsBrowser(selected.Source)&&_artwork.Find(music.Title,music.Artist,selected.Source.StartsWith("browser-companion:",StringComparison.Ordinal)?selected.Source:null,music.Album) is {} linked)coverBitmap=TryRenderCover(linked)??coverBitmap;
            }
            _diagnostic="player="+MediaPlaybackSource.Name(selected.Source)+"; "+selected.Diagnostic+"; available="+string.Join(",",candidates.Select(s=>MediaPlaybackSource.Name(s.Source)).Distinct());
            ApplySample(music with {ArtworkWidth=coverBitmap?.SourceWidth??0,ArtworkHeight=coverBitmap?.SourceHeight??0},selected.ResourceKey,
                coverBitmap?.Legacy,coverBitmap?.Tab5,coverBitmap?.Jpeg,selected.CompactTitle,sampleTick);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or InvalidOperationException or COMException)
        {
            _diagnostic="system media read unavailable: "+ex.GetType().Name;
            ApplyEmpty();
        }
        finally{
            // Cancellation of a system read must not dispose a process handle
            // while the bounded native read is still using it.
            try{if(qqTask is not null)try{await qqTask;}catch(OperationCanceledException){}}
            finally{_refreshGate.Release();}
        }
    }

    private static bool IsBrowser(string source)=>source.StartsWith("browser-companion:",StringComparison.Ordinal)||new[]{"chrome","msedge","firefox","brave","opera"}.Any(s=>source.Contains(s,StringComparison.OrdinalIgnoreCase));
    private CoverImages? TryRenderCover(byte[] bytes)
    {
        try{return RenderCoverBytes(bytes);}catch(Exception ex) when(ex is IOException or ArgumentException or ExternalException){return null;}
    }
    private static async Task<MediaPlaybackSource?> ReadSystemAsync(GlobalSystemMediaTransportControlsSession session,bool current,CancellationToken token)
    {
        try {
            using var timeout=CancellationTokenSource.CreateLinkedTokenSource(token);timeout.CancelAfter(TimeSpan.FromSeconds(2));
            var properties=await session.TryGetMediaPropertiesAsync().AsTask(timeout.Token);
            string title=properties?.Title?.Trim()??"";if(title.Length==0)return null;
            var playback=session.GetPlaybackInfo();
            if(playback.PlaybackStatus is not (GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing or GlobalSystemMediaTransportControlsSessionPlaybackStatus.Paused))return null;
            var timeline=session.GetTimelineProperties();bool playing=playback.PlaybackStatus==GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
            double rate=playback.PlaybackRate??1;if(!double.IsFinite(rate)||rate is <0 or >16)rate=1;
            var now=DateTimeOffset.UtcNow;double duration=Math.Max(0,(timeline.EndTime-timeline.StartTime).TotalSeconds),elapsed=Math.Max(0,(timeline.Position-timeline.StartTime).TotalSeconds);
            bool available=timeline.LastUpdatedTime.Year>2000&&timeline.LastUpdatedTime<=now.AddSeconds(1)&&duration is >0 and <=86400&&elapsed<=duration+2;
            if(playing&&available)elapsed+=Math.Max(0,(now-timeline.LastUpdatedTime).TotalSeconds)*rate;
            if(duration>0)elapsed=Math.Min(duration,elapsed);
            var music=new MusicSnapshot(title,properties?.Artist?.Trim()??"",properties?.AlbumTitle?.Trim()??"",playing,elapsed,duration,now){TimelineAvailable=available,PlaybackRate=rate};
            string source=session.SourceAppUserModelId;
            return new(source,title+"\n"+music.Artist+"\n"+music.Album,music,"smtc",current,properties?.Thumbnail);
        }catch(OperationCanceledException) when(token.IsCancellationRequested){throw;}
        catch(OperationCanceledException){return null;}
        catch(Exception ex) when(ex is UnauthorizedAccessException or InvalidOperationException or COMException or TimeoutException){return null;}
    }

    internal void ApplySample(MusicSnapshot sample,string key,byte[]? cover,byte[]? tab5Cover=null,JpegCover? jpeg=null,string? compactTitle=null,long? sampledTick=null)
    {
        bool changed;
        lock(_sync)
        {
            changed=key!=_resourceKey||!_coverBitmap.AsSpan().SequenceEqual(cover??[])||!_tab5CoverBitmap.AsSpan().SequenceEqual(tab5Cover??[]);
            _emptySamples=0;
            if(key!=_resourceKey){_resourceKey=key;_textBitmap=RenderTextBitmap(compactTitle??sample.Title,sample.Artist);_textRevision++;}
            var bytes=cover??[];
            if(!_coverBitmap.AsSpan().SequenceEqual(bytes)){_coverBitmap=bytes;_coverRevision++;}
            var large=tab5Cover??[];
            if(!_tab5CoverBitmap.AsSpan().SequenceEqual(large))_tab5CoverBitmap=large;
            if(jpeg is null)_jpegCover=null;
            else if(_jpegCover is null||!_jpegCover.Bytes.AsSpan().SequenceEqual(jpeg.Bytes))_jpegCover=jpeg;
            // Stable references prevent recompressing unchanged artwork every status tick.
            _snapshot=sample with{CoverRgb565=_coverBitmap.Length>0?_coverBitmap:null,
                Tab5CoverRgb565=_tab5CoverBitmap.Length>0?_tab5CoverBitmap:null,Tab5CoverJpeg=_jpegCover};
            _sampleTick=sampledTick??PlaybackClock();
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
            _netease.Dispose();
            _qqmusic.Dispose();_qqArtwork.Dispose();_selectedSource="";
            _snapshot = new MusicSnapshot(string.Empty, string.Empty, string.Empty, false,
                0, 0, DateTimeOffset.UtcNow);
        }
    }

    public void Dispose(){_netease.Dispose();_qqmusic.Dispose();_qqArtwork.Dispose();}

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

    private async Task<CoverImages?> RenderCoverBitmapAsync(IRandomAccessStreamReference? reference,CancellationToken token)
    {
        if (reference is null) return null;
        try
        {
            using var timeout=CancellationTokenSource.CreateLinkedTokenSource(token);timeout.CancelAfter(TimeSpan.FromSeconds(2));
            using var stream = await reference.OpenReadAsync().AsTask(timeout.Token);
            if (stream.Size is 0 or > 10_000_000) return null;
            using var reader = new DataReader(stream.GetInputStreamAt(0));
            await reader.LoadAsync((uint)stream.Size).AsTask(timeout.Token);
            var bytes = new byte[stream.Size];
            reader.ReadBytes(bytes);
            return RenderCoverBytes(bytes);
        }
        catch(OperationCanceledException) when(!token.IsCancellationRequested){return null;}
        catch (Exception ex) when (ex is IOException or ArgumentException or COMException)
        {
            return null;
        }
    }

    private CoverImages RenderCoverBytes(byte[] bytes) {
        if(_renderedCover is not null&&_coverSource.AsSpan().SequenceEqual(bytes))return _renderedCover;
        using var stream=new MemoryStream(bytes);using var source=Image.FromStream(stream);
        if(source.Width>4096||source.Height>4096||(long)source.Width*source.Height>16_777_216)throw new InvalidDataException("Music artwork dimensions exceed bounds");
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
