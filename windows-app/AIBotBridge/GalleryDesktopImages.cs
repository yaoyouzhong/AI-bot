using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using Windows.Graphics.Imaging;
using Windows.Storage;
namespace AIBotBridge;

// Desktop-only opt-in index. It is not read by device transport or gallery import.
internal sealed class GalleryDesktopImages
{
    internal sealed record Source(string Path,string Sha256,int Width,int Height);
    internal sealed record Part(string Source,double[] Crop,double[] Dest,int Rotation);
    internal sealed record Page(string Category,string Id,string WorkId,string Frame,string PreviewSha256,int Width,int Height,Part[] Parts);
    internal sealed record Index(int Schema,Dictionary<string,Source> Sources,Page[] Pages);
    internal sealed record Result(Bitmap Image,int Width,int Height);
    private readonly Dictionary<string,Page> _pages=[];
    private readonly Dictionary<string,Source> _sources=[];
    private readonly Dictionary<string,(long Length,long Stamp)> _verified=[];
    internal string? LastFailure {get;private set;}
    private readonly SemaphoreSlim _decode=new(1);
    private readonly bool _useInstalled;
    private readonly Dictionary<string,(string Path,GalleryDesktopImages Loader)> _installed=[];
    internal GalleryDesktopImages(string? path=null){
        _useInstalled=path is null;
        path??=System.IO.Path.Combine(AppContext.BaseDirectory,"desktop-originals.json");
        try{
            var file=new FileInfo(path);if(!file.Exists||file.Length>16*1024*1024)return;
            var index=JsonSerializer.Deserialize<Index>(File.ReadAllBytes(path),JsonDefaults.Options);
            if(index is null||index.Schema!=1||index.Pages.Length>10000||index.Sources.Count>10000)return;
            foreach(var page in index.Pages){
                if(page.Width<=0||page.Height<=0||page.Width>200000||page.Height>200000||page.Parts.Length is <1 or >12)throw new InvalidDataException();
                foreach(var part in page.Parts){
                    if(!index.Sources.TryGetValue(part.Source,out var source)||!System.IO.Path.IsPathFullyQualified(source.Path)||source.Path.StartsWith(@"\\")||source.Width<=0||source.Height<=0||source.Width>200000||source.Height>200000||part.Rotation is not (0 or 180))throw new InvalidDataException();
                    if(!ValidBounds(part.Crop,source.Width,source.Height)||!ValidBounds(part.Dest,page.Width,page.Height))throw new InvalidDataException();
                }
                _pages.Add(page.Category+"/"+page.Id+"/"+page.Frame,page);
            }
            foreach(var source in index.Sources)_sources.Add(source.Key,source.Value);
        }catch(Exception e) when(e is InvalidDataException or IOException or UnauthorizedAccessException or JsonException or ArgumentException or NullReferenceException){LastFailure=e.GetType().Name+": "+e.Message;_pages.Clear();_sources.Clear();}
    }
    private static bool ValidBounds(double[]? b,int width,int height)=>b is {Length:4}&&b.All(double.IsFinite)&&b[0]>=0&&b[1]>=0&&b[2]>0&&b[3]>0&&b[0]+b[2]<=width+.01&&b[1]+b[3]<=height+.01;
    internal async Task<Result?> LoadAsync(Tab5Gallery.BrowserPage preview,int frame,CancellationToken token){
        if(_useInstalled && GalleryHdPack.Current(preview.Work.Category) is {} directory){
            GalleryDesktopImages loader;
            lock(_installed){
                if(!_installed.TryGetValue(preview.Work.Category,out var saved)||saved.Path!=directory){saved=(directory,new GalleryDesktopImages(System.IO.Path.Combine(directory,"desktop-originals.json")));_installed[preview.Work.Category]=saved;}
                loader=saved.Loader;
            }
            // An imported category takes precedence; missing/mismatched assets fall back to the standard preview.
            return await loader.LoadAsync(preview,frame,token);
        }
        string key=preview.Work.Category+"/"+preview.Work.Id+"/"+preview.Work.Frames[frame];
        if(!_pages.TryGetValue(key,out var page)||page.WorkId!=(preview.Work.WorkId??preview.Work.Source)||!Convert.ToHexString(SHA256.HashData(preview.Jpeg)).Equals(page.PreviewSha256,StringComparison.OrdinalIgnoreCase))return null;
        await _decode.WaitAsync(token);
        Bitmap? output=null;
        try{
            token.ThrowIfCancellationRequested();
            double scale=Math.Min(1,Math.Min(8192d/Math.Max(page.Width,page.Height),Math.Sqrt(16000000d/((double)page.Width*page.Height))));
            int width=Math.Max(1,(int)Math.Round(page.Width*scale)),height=Math.Max(1,(int)Math.Round(page.Height*scale));
            output=new Bitmap(width,height,PixelFormat.Format32bppArgb);
            using(var canvas=Graphics.FromImage(output)){
                canvas.Clear(Color.FromArgb(231,224,209));canvas.InterpolationMode=System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                foreach(var part in page.Parts){
                    token.ThrowIfCancellationRequested();var source=_sources[part.Source];var info=new FileInfo(source.Path);
                    if(!info.Exists||info.Length is <16 or >2147483648L)return null;
                    var stamp=(info.Length,info.LastWriteTimeUtc.Ticks);
                    if(!_verified.TryGetValue(source.Path,out var saved)||saved!=stamp){
                        using var file=File.OpenRead(source.Path);var digest=await SHA256.HashDataAsync(file,token);
                        if(!Convert.ToHexString(digest).Equals(source.Sha256,StringComparison.OrdinalIgnoreCase))return null;
                        _verified[source.Path]=stamp;
                    }
                    var storage=await StorageFile.GetFileFromPathAsync(source.Path).AsTask(token);using var stream=await storage.OpenReadAsync().AsTask(token);
                    var decoder=await BitmapDecoder.CreateAsync(stream).AsTask(token);
                    if(decoder.PixelWidth!=source.Width||decoder.PixelHeight!=source.Height)return null;
                    double sample=Math.Min(1,Math.Min(part.Dest[2]*scale/part.Crop[2],part.Dest[3]*scale/part.Crop[3]));
                    uint sw=(uint)Math.Max(1,Math.Round(source.Width*sample)),sh=(uint)Math.Max(1,Math.Round(source.Height*sample));
                    double sx=(double)sw/source.Width,sy=(double)sh/source.Height;
                    uint x=(uint)Math.Floor(part.Crop[0]*sx),y=(uint)Math.Floor(part.Crop[1]*sy);
                    uint w=Math.Min(sw-x,(uint)Math.Max(1,Math.Ceiling((part.Crop[0]+part.Crop[2])*sx)-x)),h=Math.Min(sh-y,(uint)Math.Max(1,Math.Ceiling((part.Crop[1]+part.Crop[3])*sy)-y));
                    if((long)w*h>17000000)throw new InvalidDataException("Desktop decode limit");
                    var transform=new BitmapTransform{ScaledWidth=sw,ScaledHeight=sh,Bounds=new BitmapBounds{X=x,Y=y,Width=w,Height=h},InterpolationMode=BitmapInterpolationMode.Fant};
                    var data=await decoder.GetPixelDataAsync(BitmapPixelFormat.Bgra8,BitmapAlphaMode.Straight,transform,ExifOrientationMode.IgnoreExifOrientation,ColorManagementMode.ColorManageToSRgb).AsTask(token);
                    token.ThrowIfCancellationRequested();byte[] pixels=data.DetachPixelData();if(pixels.Length!=(long)w*h*4)throw new InvalidDataException();
                    using var partImage=new Bitmap((int)w,(int)h,PixelFormat.Format32bppArgb);
                    var bits=partImage.LockBits(new Rectangle(0,0,(int)w,(int)h),ImageLockMode.WriteOnly,PixelFormat.Format32bppArgb);
                    try{for(int row=0;row<(int)h;row++)Marshal.Copy(pixels,row*(int)w*4,bits.Scan0+row*bits.Stride,(int)w*4);}finally{partImage.UnlockBits(bits);}
                    if(part.Rotation==180)partImage.RotateFlip(RotateFlipType.Rotate180FlipNone);
                    canvas.DrawImage(partImage,new RectangleF((float)(part.Dest[0]*scale),(float)(part.Dest[1]*scale),(float)(part.Dest[2]*scale),(float)(part.Dest[3]*scale)));
                }
            }
            token.ThrowIfCancellationRequested();var result=new Result(output,page.Width,page.Height);output=null;return result;
        }catch(Exception e) when(e is InvalidDataException or IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or COMException or ExternalException){LastFailure=e.GetType().Name+": "+e.Message;return null;}
        finally{output?.Dispose();_decode.Release();}
    }
}
