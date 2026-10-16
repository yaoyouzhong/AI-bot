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
    internal static string EspVersion {
        get { using var catalog=JsonDocument.Parse(File.ReadAllText("download-catalog.json"));
            return catalog.RootElement.GetProperty("downloads").GetProperty("esp8266").GetProperty("version").GetString()!; }
    }
    internal static void Run(string output, bool evergreen=false)
    {
        using var catalog=JsonDocument.Parse(File.ReadAllText("download-catalog.json"));
        var downloads=catalog.RootElement.GetProperty("downloads");
        object GalleryAsset(string category) {var item=downloads.GetProperty(category);return new{name=item.GetProperty("file").GetString(),size=item.GetProperty("size").GetInt64(),digest="sha256:"+item.GetProperty("sha256").GetString(),browser_download_url=$"https://github.com/yaoyouzhong/AI-bot/releases/download/{item.GetProperty("tag").GetString()}/{item.GetProperty("file").GetString()}"};}
        string sums=string.Join("\n",new[]{"painting","calligraphy"}.Select(category=>{var item=downloads.GetProperty(category);return item.GetProperty("sha256").GetString()+"  "+item.GetProperty("file").GetString();}));
        string releases=JsonSerializer.Serialize(new object[]{
            UpdateSelfTest.Release("bridge",BridgeVersion,$"AIBotBridge-{BridgeVersion}-setup-win-x64.exe",[]),
            UpdateSelfTest.Release("tab5",Tab5Version,$"TAB5-upgrade-{Tab5Version}.zip",[]),
            UpdateSelfTest.Release("esp8266",EspVersion,$"AI-bot-{EspVersion}-firmware-materials.zip",[]),
            new{tag_name=downloads.GetProperty("painting").GetProperty("tag").GetString(),draft=false,prerelease=false,body="Published collection fixture",published_at="2026-10-09T00:00:00Z",assets=new object[]{GalleryAsset("painting"),GalleryAsset("calligraphy"),new{name="SHA256SUMS.txt",size=sums.Length,browser_download_url="https://github.com/yaoyouzhong/AI-bot/releases/download/"+downloads.GetProperty("painting").GetProperty("tag").GetString()+"/SHA256SUMS.txt"}}}});
        var handler=new UpdateSelfTest.Handler{Reply=(request,_)=>Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(request.RequestUri!.AbsolutePath.EndsWith("/SHA256SUMS.txt")?sums:releases,Encoding.UTF8,"application/json")})};
        using var service=new UpdateService(handler);
        UpdateDevice[] devices=[new("bridge","电脑端 AI-bot","0.6.1","运行中",true,"bridge","安装程序",true),new("tab","M5Stack TAB5","0.2.149-ui","在线",true,"upgrade-tab5","在设备上确认安装",true),new("esp","ESP8266 小屏",EspFirmwareVersion.Legacy,"在线",true,"flash","USB，先备份再升级",true)];
        using(var form=new UpdateCenterForm(()=>devices,(_,_)=>throw new InvalidOperationException("Documentation must not install"),service)){
            Show(form);
            var deadline=DateTime.UtcNow.AddSeconds(10);
            while(service.CheckedAt is null){if(DateTime.UtcNow>deadline)throw new TimeoutException();Application.DoEvents();Thread.Sleep(10);}
            var grid=(DataGridView)form.Controls.Find("updates",true).Single();grid.CurrentCell=grid[0,evergreen?1:2];Application.DoEvents();
            if(evergreen) {
                // Presentation-only placeholders in native controls; updater data stays valid.
                for(int i=0;i<grid.Rows.Count;i++) {
                    grid[1,i].Value=i==2?"未提供":"已安装";
                    grid[2,i].Value="可用更新";
                }
            }
            Save(form,"update-center");form.Close();
        }
        // Counts match the published packs. These are UI-only fixture records,
        // not installed image data and not evidence of an import operation.
        foreach(var (category,works,frames) in new[]{("painting",366,826),("calligraphy",366,3802)}){
            var directory=Path.Combine(GalleryPack.Root,category);Directory.CreateDirectory(directory);
            string edition=downloads.GetProperty(category).GetProperty("version").GetString()!;
            File.WriteAllText(Path.Combine(directory,"PACK.json"),JsonSerializer.Serialize(new GalleryPackManifest(1,edition,category,works,frames,[]),JsonDefaults.Options));
        }
        using(var form=new GalleryPackForm(service,_=>throw new InvalidOperationException("Documentation must not open links"))){Show(form);Save(form,"gallery-packs");form.Close();}
        if(evergreen) {
            var store=new DeviceRegistryStore(Path.Combine(AppPaths.GetFolderPath(Environment.SpecialFolder.ApplicationData),"promo-devices.json"));
            store.Migrate(null,false,false);
            var tab=DeviceRegistryStore.Create(HardwareKind.Tab5,"M5Stack TAB5","001122334455");
            var esp=DeviceRegistryStore.Create(HardwareKind.Esp8266,"ESP8266 小屏",null);
            store.Add(tab);store.Add(esp);
            using var center=new DeviceCenterForm(store,d=>new DeviceView(true,"在线（演示）","演示连接","已安装","数据已确认（演示）","待机（演示）",d.Kind==HardwareKind.Tab5?"已连接（演示）":null,"USB"),(_,_)=>{},(_,_)=>Task.CompletedTask,()=>{},_=>{});
            Show(center);
            foreach(var (device,name) in new[]{(tab,"center-detail"),(esp,"esp-detail")}) {
                center.SelectDevice(device.Id);Application.DoEvents();
                var tabs=center.Controls.OfType<TabControl>().Single();
                using var bitmap=new Bitmap(tabs.Width,395);
                tabs.DrawToBitmap(bitmap,new(Point.Empty,tabs.Size));
                bitmap.Save(Path.Combine(output,name+".png"),ImageFormat.Png);
            }
            center.Close();
        }
        Console.WriteLine("RELEASE_MEDIA_OK current controls; fixture releases and pack counts; no real accounts, download, device or installer");
        void Show(Form form){form.StartPosition=FormStartPosition.Manual;form.Location=new(-30000,-30000);form.ShowInTaskbar=false;form.Show();form.PerformLayout();Application.DoEvents();}
        void Save(Form form,string name){if(evergreen)Normalize(form);using var bitmap=new Bitmap(form.Width,form.Height);form.DrawToBitmap(bitmap,new(Point.Empty,form.Size));bitmap.Save(Path.Combine(output,name+".png"),ImageFormat.Png);}
        void Normalize(Control parent){foreach(Control child in parent.Controls){if(child is Label)child.Text=System.Text.RegularExpressions.Regex.Replace(child.Text,@"\b\d+\.\d+\.\d+(?:-ui)?\b","已安装");Normalize(child);}}
    }
}
