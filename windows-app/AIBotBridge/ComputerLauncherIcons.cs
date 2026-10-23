using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace AIBotBridge;

// Local shell artwork only. Never transmit filesystem paths or execute the target.
internal sealed class ComputerLauncherIcons
{
    private readonly object _gate=new();
    private readonly Dictionary<string,(string Key,ComputerLauncherView View)> _cache=[];
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]
    private struct ShellInfo {
        public IntPtr Icon;public int Index;public uint Attributes;
        [MarshalAs(UnmanagedType.ByValTStr,SizeConst=260)]public string DisplayName;
        [MarshalAs(UnmanagedType.ByValTStr,SizeConst=80)]public string TypeName;
    }
    [DllImport("shell32.dll",CharSet=CharSet.Unicode)]
    private static extern IntPtr SHGetFileInfo(string path,uint attributes,out ShellInfo info,uint size,uint flags);
    [DllImport("user32.dll")]private static extern bool DestroyIcon(IntPtr icon);
    internal static string? Read(string path,bool folder) {
        try {
            using Icon? icon=folder?FolderIcon():Icon.ExtractAssociatedIcon(path);
            if(icon is null)return null;
            using var source=icon.ToBitmap();using var bitmap=new Bitmap(32,32);
            using(var g=Graphics.FromImage(bitmap)) {
                g.Clear(Color.Transparent);g.InterpolationMode=InterpolationMode.HighQualityBicubic;
                g.DrawImage(source,new Rectangle(0,0,32,32));
            }
            byte[] pixels=new byte[32*32*4];
            for(int y=0;y<32;y++)for(int x=0;x<32;x++) {
                Color c=bitmap.GetPixel(x,y);int p=(y*32+x)*4;
                pixels[p]=c.B;pixels[p+1]=c.G;pixels[p+2]=c.R;pixels[p+3]=c.A;
            }
            return Convert.ToBase64String(pixels);
        }catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or ArgumentException or ExternalException){return null;}
    }
    private static Icon? FolderIcon() {
        SHGetFileInfo("folder",0x10,out var info,(uint)Marshal.SizeOf<ShellInfo>(),0x100|0x10);
        if(info.Icon==IntPtr.Zero)return null;
        try{using var borrowed=Icon.FromHandle(info.Icon);return (Icon)borrowed.Clone();}
        finally{DestroyIcon(info.Icon);}
    }
    internal (string Revision,ComputerLauncherView[] Views) Get(ComputerLauncher[] launchers,string knownRevision) {
        lock(_gate) {
            foreach(string id in _cache.Keys.Where(id=>!launchers.Any(x=>x.Id==id)).ToArray())_cache.Remove(id);
            var views=launchers.Select(item=> {
                bool folder=Directory.Exists(item.Path);
                string key=item.Path+"|"+item.Name+"|"+folder+"|"+(folder?0:File.GetLastWriteTimeUtc(item.Path).Ticks);
                if(!_cache.TryGetValue(item.Id,out var saved)||saved.Key!=key) {
                    saved=(key,new ComputerLauncherView(item.Id,item.Name,folder?"folder":"app",Read(item.Path,folder)));
                    _cache[item.Id]=saved;
                }
                return saved.View;
            }).ToArray();
            string revision=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("|",views.Select(v=>v.Id+v.Name+v.Kind+v.Icon))))).ToLowerInvariant();
            return(revision,revision==knownRevision?views.Select(v=>v with{Icon=null}).ToArray():views);
        }
    }
}
