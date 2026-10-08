using System.Drawing.Imaging;
using System.Net;
using System.Text;
using System.Text.Json;
namespace AIBotBridge;

// Current product controls, isolated fixture metadata; never downloads or flashes.
internal static class ReleaseMedia
{
    // Run from the repository root, as documented in SCREENSHOTS.md.
    internal static string BridgeVersion => File.ReadAllText("VERSION").Trim();
    internal static string Tab5Version => File.ReadAllText(Path.Combine("versions", "TAB5")).Trim();
    internal static string EspVersion => File.ReadAllText(Path.Combine("firmware", "VERSION")).Trim();
    internal static void Run(string output)
    {
        string releases=JsonSerializer.Serialize(new[]{
            UpdateSelfTest.Release("bridge",BridgeVersion,$"AIBotBridge-{BridgeVersion}-setup-win-x64.exe",[]),
            UpdateSelfTest.Release("tab5",Tab5Version,$"TAB5-upgrade-{Tab5Version}.zip",[]),
            UpdateSelfTest.Release("esp8266",EspVersion,$"AI-bot-{EspVersion}-firmware-materials.zip",[])});
        var handler=new UpdateSelfTest.Handler{Reply=(_,_)=>Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(releases,Encoding.UTF8,"application/json")})};
        using var service=new UpdateService(handler);
        UpdateDevice[] devices=[new("bridge","电脑端 AI-bot","0.6.1","运行中",true,"bridge","安装程序",true),new("tab","M5Stack TAB5","0.2.149-ui","在线",true,"upgrade-tab5","在设备上确认安装",true),new("esp","ESP8266 小屏",EspFirmwareVersion.Legacy,"在线",true,"flash","USB，先备份再升级",true)];
        using(var form=new UpdateCenterForm(()=>devices,(_,_)=>throw new InvalidOperationException("Documentation must not install"),service)){
            Show(form);
            var deadline=DateTime.UtcNow.AddSeconds(10);
            while(service.CheckedAt is null){if(DateTime.UtcNow>deadline)throw new TimeoutException();Application.DoEvents();Thread.Sleep(10);}
            var grid=(DataGridView)form.Controls.Find("updates",true).Single();grid.CurrentCell=grid[0,2];Application.DoEvents();
            Save(form,"update-center");form.Close();
        }
        // Counts match the published packs. These are UI-only fixture records,
        // not installed image data and not evidence of an import operation.
        foreach(var (category,works,frames) in new[]{("painting",402,804),("calligraphy",411,3000)}){
            var directory=Path.Combine(GalleryPack.Root,category);Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory,"PACK.json"),JsonSerializer.Serialize(new GalleryPackManifest(1,"2026.10.07",category,works,frames,[]),JsonDefaults.Options));
        }
        using(var form=new GalleryPackForm()){Show(form);Save(form,"gallery-packs");form.Close();}
        Console.WriteLine("RELEASE_MEDIA_OK current controls; fixture releases and pack counts; no real accounts, download, device or installer");
        void Show(Form form){form.StartPosition=FormStartPosition.Manual;form.Location=new(-30000,-30000);form.ShowInTaskbar=false;form.Show();form.PerformLayout();Application.DoEvents();}
        void Save(Form form,string name){using var bitmap=new Bitmap(form.Width,form.Height);form.DrawToBitmap(bitmap,new(Point.Empty,form.Size));bitmap.Save(Path.Combine(output,name+".png"),ImageFormat.Png);}
    }
}
