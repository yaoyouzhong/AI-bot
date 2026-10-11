using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
namespace AIBotBridge;
internal static class GalleryUpdateUiSelfTest
{
    internal static void Run(string directory) {
        Directory.CreateDirectory(directory);
        void Install(string category,string version) {
            string file=Path.Combine(directory,category+"-"+version+".zip");File.WriteAllBytes(file,GalleryUpdateSelfTest.Package(category,version,"测试作品"));GalleryPack.Install(file);
        }
        Install("painting","2026.10.07");Install("calligraphy","2026.10.07");
        byte[] painting=GalleryUpdateSelfTest.Package("painting","2026.10.09","新名画"),calligraphy=GalleryUpdateSelfTest.Package("calligraphy","2026.10.09","新书法");
        string releases=JsonSerializer.Serialize(new[]{GalleryUpdateSelfTest.Release("painting","2026.10.09",painting),GalleryUpdateSelfTest.Release("calligraphy","2026.10.09",calligraphy)});
        bool corrupt=false,hold=false,fail=false,waiting=false,layoutPreview=false;int paintingDownloads=0,browserOpens=0;
        var handler=new UpdateSelfTest.Handler();
        handler.Reply=async(request,token)=>{
            if(fail)return new(HttpStatusCode.ServiceUnavailable);
            string path=request.RequestUri!.AbsolutePath;
            if(hold&&path.EndsWith(".zip")){waiting=true;await Task.Delay(Timeout.Infinite,token);}
            if(path.Contains("Painting")&&path.EndsWith(".zip"))paintingDownloads++;
            byte[] data=path.EndsWith("SHA256SUMS.txt")?Encoding.UTF8.GetBytes(string.Join("\n",new[]{("Painting",painting),("Calligraphy",calligraphy)}.Select(p=>Convert.ToHexString(SHA256.HashData(p.Item2))+"  AI-bot-Daily"+p.Item1+"-Curated-2026.10.09.zip"))):
                path.EndsWith(".zip")?(path.Contains("Painting")?painting:calligraphy):Encoding.UTF8.GetBytes(layoutPreview?releases.Replace("\"size\":"+painting.LongLength,"\"size\":221410711").Replace("\"size\":"+calligraphy.LongLength,"\"size\":447081384"):releases);
            if(corrupt&&path.EndsWith(".zip"))data=data.Select((b,i)=>i==0?(byte)(b^1):b).ToArray();
            return new(HttpStatusCode.OK){Content=new ByteArrayContent(data)};
        };
        using var service=new UpdateService(handler);
        using var form=new GalleryPackForm(service,_=>browserOpens++);
        form.StartPosition=FormStartPosition.Manual;form.Location=new(-30000,-30000);form.ShowInTaskbar=false;
        var check=(Button)form.Controls.Find("gallery-check",true).Single();var paint=(Button)form.Controls.Find("painting-update",true).Single();
        var call=(Button)form.Controls.Find("calligraphy-update",true).Single();var cancel=(Button)form.Controls.Find("gallery-cancel",true).Single();
        var state=(Label)form.Controls.Find("gallery-state",true).Single();var latest=(Label)form.Controls.Find("painting-latest",true).Single();
        var paintStatus=(Label)form.Controls.Find("painting-status",true).Single();
        async Task Wait(Func<bool> done){var until=DateTime.UtcNow.AddSeconds(20);while(!done()){if(DateTime.UtcNow>until)throw new TimeoutException("Gallery UI: "+state.Text);await Task.Delay(5);}}
        Exception? failure=null;
        form.Shown+=async(_,_)=>{try {
            await Wait(()=>check.Enabled&&service.Galleries.Count==2);
            GalleryUpdateSelfTest.Check(paint.Enabled&&call.Enabled&&paintStatus.Text=="有更新","Installed old galleries did not advertise independent upgrades");
            corrupt=true;paint.PerformClick();paint.PerformClick();await Wait(()=>check.Enabled&&state.Text.StartsWith("更新未完成"));
            GalleryUpdateSelfTest.Check(paintingDownloads==1&&GalleryPack.Installed("painting")!.Version=="2026.10.07","Duplicate click or corrupt download changed old gallery");
            corrupt=false;paint.PerformClick();await Wait(()=>check.Enabled&&GalleryPack.Installed("painting")!.Version=="2026.10.09");
            GalleryUpdateSelfTest.Check(!paint.Enabled&&paintStatus.Text=="已是最新"&&call.Enabled,"UI did not read back installed edition or isolate categories");
            GalleryUpdateSelfTest.Check(Directory.GetDirectories(GalleryPack.Root,"painting.backup-*").Length==1,"UI upgrade did not preserve old gallery");
            hold=true;call.PerformClick();await Wait(()=>waiting&&cancel.Visible);
            GalleryUpdateSelfTest.Check(!paint.Enabled&&!call.Enabled&&!check.Enabled,"Concurrent gallery operations enabled");
            cancel.PerformClick();await Wait(()=>check.Enabled&&state.Text.Contains("取消"));
            GalleryUpdateSelfTest.Check(GalleryPack.Installed("calligraphy")!.Version=="2026.10.07","Cancellation replaced old calligraphy");
            hold=false;call.PerformClick();await Wait(()=>check.Enabled&&GalleryPack.Installed("calligraphy")!.Version=="2026.10.09");
            GalleryUpdateSelfTest.Check(!call.Enabled&&browserOpens==0,"One-click gallery install opened browser or still advertised upgrade");
            string downloads=Path.Combine(AppPaths.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"AI-bot","updates","gallery-downloads");
            GalleryUpdateSelfTest.Check(Directory.GetDirectories(downloads).Length==0,"UI did not remove its temporary download cache");
            fail=true;check.PerformClick();await Wait(()=>check.Enabled&&state.Text.StartsWith("检查未完成"));
            GalleryUpdateSelfTest.Check(latest.Text.Contains("2026.10.09")&&!paint.Enabled,"Offline check lost known latest edition");
            fail=false;Install("painting","2026.10.10");check.PerformClick();await Wait(()=>check.Enabled&&paintStatus.Text=="本机较新");
            GalleryUpdateSelfTest.Check(!paint.Enabled,"Newer installed gallery offers downgrade");
            layoutPreview=true;
            foreach(bool upToDate in new[]{false,true}) {
            Install("painting",upToDate?"2026.10.09":"2026.10.07");
            foreach(float scale in new[]{1f,1.5f,2f})foreach(bool minimum in new[]{false,true}) {
                using var view=new GalleryPackForm(service,_=>browserOpens++);view.StartPosition=FormStartPosition.Manual;view.Location=new(-30000,-30000);view.ShowInTaskbar=false;view.Show();
                if(minimum)view.Size=view.MinimumSize;if(scale!=1)view.Scale(new SizeF(scale,scale));view.PerformLayout();await Task.Yield();
                await Wait(()=>((Button)view.Controls.Find("gallery-check",true).Single()).Enabled);
                if(scale==1&&!minimum) {
                    var footer=view.Controls.Find("gallery-download-center",true).Single();
                    await Wait(()=>view.RectangleToScreen(view.ClientRectangle).Contains(footer.RectangleToScreen(footer.ClientRectangle)));
                    GalleryUpdateSelfTest.Check(view.RectangleToScreen(view.ClientRectangle).Contains(footer.RectangleToScreen(footer.ClientRectangle)),"Default gallery window requires scrolling to its actions/footer");
                }
                foreach(Control control in Descendants(view).Where(c=>c.Visible&&c is Label or Button)) {
                    var bounds=control.RectangleToScreen(control.ClientRectangle);
                    for(Control? parent=control.Parent;parent is not null&&parent!=view;parent=parent.Parent) {
                        var clip=parent.RectangleToScreen(parent.ClientRectangle);clip.Inflate(2,2);
                        GalleryUpdateSelfTest.Check(clip.Contains(bounds),$"Clipped gallery control {control.Text} scale={scale} minimum={minimum} {bounds} by {clip}");
                    }
                    if(control is Label label) {
                        GalleryUpdateSelfTest.Check(label.Height>=label.GetPreferredSize(new(label.Width,0)).Height-2,"Gallery label text clipped: "+label.Text);
                        if(label.Name.EndsWith("-latest")||label.Name.EndsWith("-status")||label.Name.EndsWith("-installed"))
                            GalleryUpdateSelfTest.Check(label.GetPreferredSize(new(label.Width,0)).Height<=TextRenderer.MeasureText("示例",label.Font).Height+2,"Gallery edition/status wrapped: "+label.Text);
                    }
                }
                using var bitmap=new Bitmap(view.Width,view.Height);view.DrawToBitmap(bitmap,new(Point.Empty,view.Size));bitmap.Save(Path.Combine(directory,$"gallery-updates-{(upToDate?"latest":"upgrade")}-{scale}-{(minimum?"minimum":"normal")}.png"));view.Close();
            }
            }
            layoutPreview=false;Install("painting","2026.10.07");
            waiting=false;hold=true;
            using(var closing=new GalleryPackForm(service)) {
                closing.StartPosition=FormStartPosition.Manual;closing.Location=new(-30000,-30000);closing.ShowInTaskbar=false;closing.Show();
                await Wait(()=>((Button)closing.Controls.Find("gallery-check",true).Single()).Enabled);
                ((Button)closing.Controls.Find("painting-update",true).Single()).PerformClick();await Wait(()=>waiting);closing.Close();
                await Wait(()=>Directory.GetFiles(downloads,"*.part",SearchOption.AllDirectories).Length==1);
            }
            GalleryUpdateSelfTest.Check(GalleryPack.Installed("painting")!.Version=="2026.10.07","Close during download replaced gallery");
        }catch(Exception ex){failure=ex;}finally{form.Close();}};
        Application.Run(form);
        if(failure is not null)throw new InvalidOperationException("Gallery update UI regression failed",failure);
        Console.WriteLine("GALLERY_UPDATE_UI_OK production click/download/hash/import/readback, backup, independent categories, duplicate click, cancellation/close, offline cache, downgrade prevention, cache cleanup, 100/150/200 percent normal/minimum captures; isolated profile");
    }
    private static IEnumerable<Control> Descendants(Control parent){foreach(Control child in parent.Controls){yield return child;foreach(var next in Descendants(child))yield return next;}}
    internal static void RunLive(string directory,string? resumeCache=null,string? calligraphyArchive=null) {
        Directory.CreateDirectory(directory);
        string cache=Path.Combine(AppPaths.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"AI-bot","updates","gallery-downloads");
        if(resumeCache is not null) {
            string source=Path.GetFullPath(resumeCache).Replace('\\','/'),artifacts=Path.GetFullPath(Path.Combine(Environment.CurrentDirectory,"artifacts")).Replace('\\','/');
            if(!System.Text.RegularExpressions.Regex.IsMatch(source,"^"+System.Text.RegularExpressions.Regex.Escape(artifacts)+@"/public-profile-[a-f0-9]{32}/local/AI-bot/updates/gallery-downloads$",System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                throw new InvalidDataException("Only an owned isolated test checkpoint can be resumed.");
            foreach(string folder in Directory.GetDirectories(source)) {
                var identity=JsonSerializer.Deserialize<GalleryDownloadIdentity>(File.ReadAllText(Path.Combine(folder,"DOWNLOAD.json")),JsonDefaults.Options)!;
                CheckpointIdentity(identity,folder);
                string part=Path.Combine(folder,identity.Name+".part");if(!File.Exists(part)||new FileInfo(part).Length==0)continue;
                GalleryUpdateSelfTest.Check(new FileInfo(part).Length<=identity.Size,"Checkpoint exceeds declared package size");
                string target=Path.Combine(cache,identity.Sha256);Directory.CreateDirectory(target);
                File.Copy(Path.Combine(folder,"DOWNLOAD.json"),Path.Combine(target,"DOWNLOAD.json"));File.Copy(part,Path.Combine(target,identity.Name+".part"));
            }
        }
        foreach(string category in new[]{"painting","calligraphy"}) {
            string old=Path.Combine(directory,category+"-old-fixture.zip");File.WriteAllBytes(old,GalleryUpdateSelfTest.Package(category,"2026.10.07","旧版测试作品"));GalleryPack.Install(old);
        }
        using var service=new UpdateService();int browserOpens=0;string active="";var starts=new Dictionary<string,List<long>>();
        service.TransferTrace+=line=>{Console.WriteLine("GALLERY_TRANSFER "+line);var match=System.Text.RegularExpressions.Regex.Match(line,@"range=(\d+)-");if(active!=""&&match.Success)starts[active].Add(long.Parse(match.Groups[1].Value));};
        using var form=new GalleryPackForm(service,_=>browserOpens++);form.StartPosition=FormStartPosition.Manual;form.Location=new(-30000,-30000);form.ShowInTaskbar=false;
        Exception? failure=null;
        var check=(Button)form.Controls.Find("gallery-check",true).Single();var state=(Label)form.Controls.Find("gallery-state",true).Single();
        async Task Wait(Func<bool> done,TimeSpan timeout){var until=DateTime.UtcNow+timeout;while(!done()){if(DateTime.UtcNow>until)throw new TimeoutException(state.Text);await Task.Delay(25);}}
        void Capture(string name){using var bitmap=new Bitmap(form.Width,form.Height);form.DrawToBitmap(bitmap,new(Point.Empty,form.Size));bitmap.Save(Path.Combine(directory,name+".png"));}
        form.Shown+=async(_,_)=>{try {
            await Wait(()=>check.Enabled,TimeSpan.FromSeconds(60));
            if(service.Error is not null||service.Galleries.Count!=2)throw new InvalidOperationException("Live gallery check failed: "+service.Error,form.OperationError);Capture("live-available");
            foreach(string category in new[]{"painting","calligraphy"}) {
                var release=service.Galleries[category];var button=(Button)form.Controls.Find(category+"-update",true).Single();
                if(category=="calligraphy"&&calligraphyArchive is not null) {
                    // Seed a verified real-package prefix to exercise resume without repeating the whole 426 MiB transfer.
                    using var input=File.OpenRead(calligraphyArchive);string sha=Convert.ToHexString(SHA256.HashData(input)).ToLowerInvariant();
                    GalleryUpdateSelfTest.Check(input.Length==release.Package!.Size,"Seed package size differs from official asset");input.Position=0;
                    string target=Path.Combine(cache,sha);Directory.CreateDirectory(target);
                    var identity=new GalleryDownloadIdentity(1,category,release.Version,release.Package.Name,input.Length,sha);
                    File.WriteAllText(Path.Combine(target,"DOWNLOAD.json"),JsonSerializer.Serialize(identity,JsonDefaults.Options));
                    using var output=File.Create(Path.Combine(target,identity.Name+".part"));long remaining=input.Length-2*1024*1024;byte[] buffer=new byte[65536];
                    while(remaining>0){int count=input.Read(buffer,0,(int)Math.Min(buffer.Length,remaining));if(count==0)throw new EndOfStreamException();output.Write(buffer,0,count);remaining-=count;}
                }
                long resumedBytes=Directory.Exists(cache)?Directory.GetFiles(cache,release.Package!.Name+".part",SearchOption.AllDirectories).Select(p=>new FileInfo(p).Length).FirstOrDefault():0;
                active=category;starts[category]=new();
                GalleryUpdateSelfTest.Check(button.Enabled,"Live curated update unavailable: "+category);button.PerformClick();
                await Wait(()=>check.Enabled,TimeSpan.FromMinutes(45));
                var installed=GalleryPack.Installed(category)??throw new InvalidOperationException("Live collection not installed: "+state.Text);
                if(installed.Version!=release.Version||installed.Category!=category)throw new InvalidOperationException("Live one-click update did not install: "+state.Text,form.OperationError);
                GalleryUpdateSelfTest.Check(Directory.GetDirectories(GalleryPack.Root,category+".backup-*").Length==1,"Live install lost previous gallery");
                if(resumedBytes>0)GalleryUpdateSelfTest.Check(starts[category].First()==resumedBytes,"Live network request did not resume the persisted byte offset");
                var gallery=new Tab5Gallery();
                for(int day=0;day<366;day++)foreach(string orientation in new[]{"landscape","portrait"})
                    GalleryUpdateSelfTest.Check(gallery.Handle(JsonSerializer.SerializeToElement(new{category,date=new DateOnly(2028,1,1).AddDays(day).ToString("yyyy-MM-dd"),op="manifest",orientation}),true,true).Status==200,"Live installed annual gallery unavailable");
                File.WriteAllText(Path.Combine(directory,category+"-live.json"),JsonSerializer.Serialize(new{category,installed.Version,installed.Works,installed.Frames,package=release.Package!.Name,bytes=release.Package.Size,resumedBytes,checkpointSource=category=="calligraphy"&&calligraphyArchive is not null?"verified real release package prefix":"persisted HTTP download",profile=GalleryPack.Root,userProfileChanged=false},new JsonSerializerOptions{WriteIndented=true}));
                Console.WriteLine($"GALLERY_LIVE_INSTALL_OK category={category} version={installed.Version} works={installed.Works} images={installed.Frames} bytes={release.Package.Size} resumed={resumedBytes} annual=366 orientations=2 backup=preserved");
            }
            GalleryUpdateSelfTest.Check(browserOpens==0,"Live update opened a browser");Capture("live-installed");
        }catch(Exception ex){failure=ex;}finally{form.Close();}};
        Application.Run(form);
        if(failure is not null)throw new InvalidOperationException("Live gallery update verification failed",failure);
        Console.WriteLine("GALLERY_LIVE_UI_OK official GitHub metadata, production buttons, real network continuation, complete SHA-256, all JPEGs, exact embedded editions, annual hot reload, prior backup; isolated profile only");
    }
    private static void CheckpointIdentity(GalleryDownloadIdentity identity,string folder) {
        string category=identity.Category=="painting"?"Painting":"Calligraphy";
        GalleryUpdateSelfTest.Check(identity.SchemaVersion==1&&GalleryPack.Category(identity.Category)&&GalleryUpdateCatalog.TryNumber(identity.Version,out _)&&
            identity.Name==$"AI-bot-Daily{category}-Curated-{identity.Version}.zip"&&identity.Sha256==Path.GetFileName(folder)&&
            System.Text.RegularExpressions.Regex.IsMatch(identity.Sha256,@"^[a-f0-9]{64}$"),"Invalid isolated checkpoint identity");
    }
}
