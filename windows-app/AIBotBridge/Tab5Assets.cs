using System.Buffers.Binary;

namespace AIBotBridge;

// Device resources stay in memory. Private user artwork is never bundled with firmware.
internal sealed class Tab5Assets(Func<long>? clock=null)
{
    private long Now=>clock?.Invoke()??Environment.TickCount64;
    private int _coverPasses;
    private long _coverRetryAt;
    private bool _coverFallbackAttempted;
    private readonly HashSet<int> _coverOffsets=[];
    private bool CoverMaySend=>_coverPasses<3&&Now>=_coverRetryAt;
    internal sealed record Asset(int Slot, string Id, int Width, int Height, int Frames, ushort[] Delays, byte[] Packed,string Encoding="rle565");
    private readonly object _gate=new();
    private volatile bool _jpegSupported;
    internal void ObserveFirmware(string version)=>_jpegSupported=Version.TryParse(version.Split('-')[0],out var parsed)&&parsed>=new Version(0,2,80);
    internal bool JpegSupported=>_jpegSupported;
    private long _coverStarted;
    private string _diagnostic="尚无封面传输";
    internal string Diagnostic {get{lock(_gate)return _diagnostic;}}
    private readonly object?[] _sources = new object?[3];
    private readonly Asset?[] _assets = new Asset?[3];
    private readonly List<(Asset Asset, int Offset)> _cycle = [];
    private int _position;
    private string _reported="-,-,-";
    private string _lastReported="";
    internal void Acknowledge(string? ids) {
        if(!ValidIds(ids))return;
        lock(_gate) {
            Volatile.Write(ref _reported,ids!);
            if(_coverStarted>0&&_assets[2] is {} cover&&ids!.Split(',')[2]==cover.Id) {
                _diagnostic=$"CRC={cover.Id}; {cover.Encoding}; {cover.Width}x{cover.Height}; bytes={cover.Packed.Length}; ackMs={Environment.TickCount64-_coverStarted}";
                _coverStarted=0;
            }
        }
    }
    internal static bool ValidIds(string? ids)=>ids is not null&&System.Text.RegularExpressions.Regex.IsMatch(ids,"^(?:[0-9a-f]{8}|-),(?:[0-9a-f]{8}|-),(?:[0-9a-f]{8}|-)$");
    internal string?[] Ids {get{lock(_gate)return _assets.Select(a=>a?.Id).ToArray();}}
    internal Asset[] Assets {get{lock(_gate)return _assets.OfType<Asset>().OrderByDescending(a=>a.Slot).ToArray();}}
    internal bool HasPending {get{lock(_gate){AdvanceCover();var ids=_reported.Split(',');return _assets.OfType<Asset>().Any(a=>ids[a.Slot]!=a.Id&&(a.Slot!=2||CoverMaySend));}}}
    internal (int Passes,bool Fallback,bool MaySend) CoverRetryState {get{lock(_gate)return(_coverPasses,_coverFallbackAttempted,CoverMaySend);}}
    private static bool SameSource(object? a,object? b)=>ReferenceEquals(a,b)||
        a is byte[] left&&b is byte[] right&&left.AsSpan().SequenceEqual(right)||
        a is NowPlayingService.JpegCover l&&b is NowPlayingService.JpegCover r&&l.Width==r.Width&&l.Height==r.Height&&l.Bytes.AsSpan().SequenceEqual(r.Bytes);
    private void AdvanceCover() {
        if(_coverPasses<3||Now<_coverRetryAt||_coverFallbackAttempted||_assets[2] is not {Encoding:"jpeg"} jpeg||_reported.Split(',')[2]==jpeg.Id)return;
        _coverFallbackAttempted=true;
        try {
            if(jpeg.Width is <1 or >336||jpeg.Height is <1 or >336||jpeg.Packed.Length>524288)return;
            using var stream=new MemoryStream(jpeg.Packed,false);using var image=Image.FromStream(stream);
            if(image.Width!=jpeg.Width||image.Height!=jpeg.Height||
                image.PropertyIdList.Contains(0x112)&&image.GetPropertyItem(0x112)?.Value is {Length:>=2} orientation&&BitConverter.ToUInt16(orientation)!=1)return;
            using var bitmap=new Bitmap(image);
            _assets[2]=Create(2,jpeg.Width,jpeg.Height,[1000],PetAssetImporter.EncodeRgb565(bitmap));
            _coverPasses=0;_coverRetryAt=0;_coverOffsets.Clear();_cycle.Clear();_position=0;
            _diagnostic=$"{jpeg.Width}x{jpeg.Height}; JPEG 未确认，尝试同尺寸 rle565";
        }catch(Exception ex) when(ex is ArgumentException or IOException or System.Runtime.InteropServices.ExternalException or OutOfMemoryException) {
            _diagnostic="JPEG 兼容转换失败，保留当前资源并停止重试";
        }
    }
    private static readonly Lazy<PetAnimation> DefaultPet=new(()=> {
        var frames=new List<byte[]>();
        for(int i=0;i<2;i++){using var bitmap=new Bitmap(112,112);using(var graphics=Graphics.FromImage(bitmap)){graphics.Clear(Color.Black);ByteSproutRenderer.Draw(graphics,28,6,true,i*240);}frames.Add(PetAssetImporter.EncodeRgb565(bitmap));}
        return new PetAnimation([240,240],frames.ToArray());
    });
    internal object? Next(StatusSnapshot snapshot)
    {
        lock(_gate) {
        bool changed = false;
        for (int slot=0; slot<3; slot++) {
            var pet = slot<2 ? PetAnimationStore.Shared.Selection(slot==0?"claude":"codex") ?? DefaultPet.Value : null;
            object? source = slot<2 ? pet : _jpegSupported&&snapshot.Music?.Tab5CoverJpeg is {} jpeg?jpeg:snapshot.Music?.Tab5CoverRgb565 ?? snapshot.Music?.CoverRgb565;
            if (slot==2?SameSource(source,_sources[slot]):ReferenceEquals(source,_sources[slot])) continue;
            var next = pet is not null ? Create(slot,pet.Width,pet.Height,pet.Delays,pet.Frames.SelectMany(f=>f).ToArray())
                : source is NowPlayingService.JpegCover coverJpeg ? new(slot,BinaryResourceProtocol.Crc32(coverJpeg.Bytes).ToString("x8"),coverJpeg.Width,coverJpeg.Height,1,[1000],coverJpeg.Bytes,"jpeg")
                : source is byte[] cover && cover.Length==336*336*2 ? Create(slot,336,336,[1000],cover)
                : source is byte[] legacy && legacy.Length==112*112*2 ? Create(slot,112,112,[1000],legacy) : null;
            if(source is not null&&next is null)continue; // Invalid replacement cannot erase last-good artwork.
            _sources[slot]=source;_assets[slot]=next;changed=true;
            if(slot==2) {
                _coverPasses=0;_coverRetryAt=0;_coverFallbackAttempted=false;_coverOffsets.Clear();
                if(next is {} coverAsset){_coverStarted=Environment.TickCount64;_diagnostic=$"CRC={coverAsset.Id}; {coverAsset.Encoding}; bytes={coverAsset.Packed.Length}; 同步中";}
            }
        }
        return NextChunk(changed);
        }
    }
    internal object[] NextBatch(int maximum) {
        lock(_gate) {
            var chunks=new List<object>();
            for(int i=0;i<maximum;i++){var chunk=NextChunk(false);if(chunk is null)break;chunks.Add(chunk);if(_position>=_cycle.Count)break;}
            return chunks.ToArray();
        }
    }
    private object? NextChunk(bool changed) {
        AdvanceCover();
        string reported=Volatile.Read(ref _reported);
        if(changed || reported!=_lastReported || _position>=_cycle.Count) {
            _lastReported=reported;var received=reported.Split(',');
            _cycle.Clear();
            foreach(var asset in _assets.OfType<Asset>())
                if(received[asset.Slot]!=asset.Id)
                for(int offset=0;offset<asset.Packed.Length;offset+=1024) _cycle.Add((asset,offset));
            // Vary each pass so a slower radio cannot repeatedly miss the same fragments.
            Random.Shared.Shuffle(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_cycle));
            _position=0;
        }
        while(_position<_cycle.Count) {
            var (a,start)=_cycle[_position++];
            if(_assets[a.Slot]?.Id!=a.Id||reported.Split(',')[a.Slot]==a.Id)continue;
            if(a.Slot==2) {
                if(!CoverMaySend||!_coverOffsets.Add(start))continue;
                if(_coverOffsets.Count==(a.Packed.Length+1023)/1024) {
                    _coverOffsets.Clear();_coverPasses++;_coverRetryAt=Now+2000;
                    _diagnostic=$"CRC={a.Id}; {a.Encoding}; passes={_coverPasses}; 等待设备确认";
                }
            }
            return Chunk(a,start);
        }
        return null;
    }
    internal static object Chunk(Asset a,int offset) => new {a.Slot,a.Id,a.Width,a.Height,a.Frames,a.Delays,encoding=a.Encoding,total=a.Packed.Length,offset,
        bytes=Convert.ToBase64String(a.Packed,offset,Math.Min(1024,a.Packed.Length-offset))};
    internal static Asset Create(int slot,int width,int height,ushort[] delays,byte[] raw)
    {
        using var buffer=new MemoryStream(); using var writer=new BinaryWriter(buffer);
        for(int pos=0;pos<raw.Length;) {
            ushort value=BinaryPrimitives.ReadUInt16LittleEndian(raw.AsSpan(pos)); int count=1;
            while(count<65535 && pos+(count+1)*2<=raw.Length && BinaryPrimitives.ReadUInt16LittleEndian(raw.AsSpan(pos+count*2))==value) count++;
            writer.Write((ushort)count);writer.Write(value);pos+=count*2;
        }
        var packed=buffer.ToArray();
        return new(slot,BinaryResourceProtocol.Crc32(packed).ToString("x8"),width,height,delays.Length,delays,packed);
    }
}
