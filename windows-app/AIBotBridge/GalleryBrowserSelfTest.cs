using System.Drawing.Imaging;
using System.Net;
using System.Text.Json;

namespace AIBotBridge;
internal static class GalleryBrowserSelfTest
{
    internal static Tab5Gallery Fixture(string root) {
        foreach(string category in new[]{"painting","calligraphy"}) {
            string directory=Path.Combine(root,category);Directory.CreateDirectory(directory);int count=category=="painting"?36:3;
            var works=new List<Tab5Gallery.Artwork>();var guides=new List<object>();
            for(int index=0;index<count;index++) {
                string id=category+index,title=(category=="painting"?"山水作品":"书法作品")+(index+1),author=index%2==0?"甲作者":"乙作者";
                string[] frames=Enumerable.Range(0,index==0?2:1).Select(page=>$"{id}-{page}.jpg").ToArray();
                foreach(string name in frames) {
                    using var bitmap=new Bitmap(1280,720);using(var g=Graphics.FromImage(bitmap)) {
                        g.Clear(Color.FromArgb(224-index%8*4,232-index%7*4,239-index%6*4));
                        using var font=new Font("Microsoft YaHei UI",42);using var brush=new SolidBrush(SettingsWindow.Ink);g.DrawString(title+"\n"+author,font,brush,70,170);
                    }
                    bitmap.Save(Path.Combine(directory,name),ImageFormat.Jpeg);
                }
                works.Add(new(id,category,title,author,"https://example.invalid/"+id,"fixture",frames,id,frames));
                guides.Add(new{id,category,workId=id,title=index==0?title+"（核实中文名）":title,author=index==0?author+"（核实名）":author,period="测试年代",museum="测试藏馆",brief="用于验证浏览界面的测试资料。",overview="简介应与安装包中的作品身份一致，更新后立即重读。",looking=index==1?"":"这是一段已核实内容的测试字段。",
                    transcription=new{status=index==1?"withheld_pending_review":"source_verified",text=new string('文',2000),explanation="仅用于测试完整原文显示。"},sources=new[]{new{id="fixture",url="https://example.invalid/"+id}}});
            }
            File.WriteAllBytes(Path.Combine(directory,"catalog.json"),JsonSerializer.SerializeToUtf8Bytes(works,JsonDefaults.Options));
            File.WriteAllBytes(Path.Combine(directory,"guides.zh.json"),JsonSerializer.SerializeToUtf8Bytes(new{schemaVersion=1,language="zh-CN",works=guides},JsonDefaults.Options));
        }
        return new(Path.Combine(root,"legacy"),root);
    }
    private static void Check(bool ok,string message){if(!ok)throw new InvalidOperationException(message);}
    internal static void Run(string directory,string? actualPacks=null,string? desktopIndex=null) {
        Directory.CreateDirectory(directory);string fixtureRoot=Path.Combine(directory,"fixtures");var gallery=Fixture(fixtureRoot);
        foreach(string category in new[]{"painting","calligraphy"}) {
            var works=gallery.Browse(category).Works;
            Check(works[0].Title.Contains("核实中文名")&&works[0].Author.Contains("核实名"),"Browser catalog does not use its visible guide captions");
            foreach(var date in new[]{new DateOnly(2024,2,29),new DateOnly(2026,1,1),new DateOnly(2026,10,11),new DateOnly(2027,1,1)}) {
                var work=works[Tab5Gallery.DayIndex(date,works.Length)];var page=gallery.BrowsePage(category,work.Id,0);
                var reply=gallery.Handle(JsonSerializer.SerializeToElement(new{category,date=date.ToString("yyyy-MM-dd"),op="manifest",includeGuide=true,guideVersion=2}),true,true);
                var body=JsonSerializer.SerializeToElement(reply.Body,JsonDefaults.Options);
                Check(reply.Status==200&&body.GetProperty("id").GetString()==page.Work.Id,"Browser date does not match daily screensaver");
                Check(body.GetProperty("guide").Deserialize<Tab5ArtGuides.Guide>(JsonDefaults.Options)==page.Guide,"Browser guide differs from device guide");
            }
            Check(gallery.BrowsePage(category,works[0].Id,1).Jpeg.Length>0,"Second page missing");
            Check(gallery.BrowsePage(category,works[0].Id,0).Guide?.Transcription.Length==2000,"Full original truncated");
            Check(gallery.BrowsePage(category,works[1].Id,0).Guide?.Transcription.Length==0,"Unverified original displayed");
        }
        void Reject(System.Action read){bool rejected=false;try{read();}catch(Exception ex) when(ex is ArgumentException or InvalidDataException){rejected=true;}Check(rejected,"Invalid browser input accepted");}
        Reject(()=>gallery.Browse("../private"));Reject(()=>gallery.BrowsePage("painting","unknown",0));Reject(()=>gallery.BrowsePage("painting","painting0",-1));
        string noGuide=Path.Combine(fixtureRoot,"calligraphy","guides.zh.json");File.Delete(noGuide);
        Check(gallery.BrowsePage("calligraphy","calligraphy0",0).Guide is null,"Legacy image-only pack fabricated a guide");
        string package=Path.Combine(directory,"replacement.zip");File.WriteAllBytes(package,GalleryUpdateSelfTest.Package("painting","2026.10.12","更新作品"));
        GalleryPack.Install(package,fixtureRoot);Check(gallery.Browse("painting").Works.Length==1&&gallery.BrowsePage("painting","fixture",0).Guide?.Title=="更新作品","Browser retained replaced catalog or guide");
        Reject(()=>gallery.BrowsePage("painting","painting0",0));
        Console.WriteLine("GALLERY_BROWSER_DATA_OK dates match screensaver including leap/year boundaries; pages, exact full text, verified-only originals, image-only compatibility, atomic import refresh, invalid inputs");

        CheckZoom();Fixture(GalleryPack.Root);Exception? failure=null;bool entered=false;
        using var service=new UpdateService(new UpdateSelfTest.Handler{Reply=(_,_)=>Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent("[]")})});
        using var manager=new GalleryPackForm(service);
        manager.StartPosition=FormStartPosition.Manual;manager.Location=new(-30000,-30000);manager.ShowInTaskbar=false;
        manager.Shown+=async(_,_)=>{
            try {
                await Wait(()=>manager.Controls.Find("gallery-check",true).OfType<Button>().Single().Enabled);
                using var detect=new System.Windows.Forms.Timer{Interval=25};
                detect.Tick+=async(_,_)=>{
                    var browser=Application.OpenForms.OfType<GalleryBrowserForm>().FirstOrDefault();if(browser is null)return;detect.Stop();entered=true;
                    try{browser.Location=new(-30000,-30000);browser.ShowInTaskbar=false;await Interact(browser);}
                    catch(Exception ex){failure=ex;}finally{browser.Close();}
                };
                detect.Start();((Button)manager.Controls.Find("gallery-browse",true).Single()).PerformClick();Check(entered,"Gallery manager browse entry did not open browser");
                if(failure is not null)throw failure;
                foreach(float scale in new[]{1f,1.5f,2f})foreach(bool minimum in new[]{false,true}) {
                    using var browser=new GalleryBrowserForm();browser.StartPosition=FormStartPosition.Manual;browser.Location=new(-30000,-30000);browser.ShowInTaskbar=false;browser.Show(manager);
                    await Wait(()=>browser.PendingLoads==0&&browser.SelectedWorkId is not null);
                    if(minimum)browser.Size=browser.MinimumSize;if(scale!=1)browser.Scale(new SizeF(scale,scale));browser.PerformLayout();await Task.Delay(80);
                    ValidateLayout(browser);Capture(browser,$"layout-{scale:0.0}-{(minimum?"minimum":"normal")}");
                    ((Button)browser.Controls.Find("browse-toggle-reading",true).Single()).PerformClick();browser.PerformLayout();
                    ValidateLayout(browser);Capture(browser,$"reader-layout-{scale:0.0}-{(minimum?"minimum":"normal")}");browser.Close();
                }
                if(actualPacks is not null) {
                    var actual=new Tab5Gallery(Path.Combine(directory,"empty-legacy"),actualPacks);
                    foreach(string category in new[]{"painting","calligraphy"}) {
                        var catalog=actual.Browse(category);Check(catalog.Works.Length>0,"Actual installed collection missing");
                        foreach(var work in catalog.Works)for(int frame=0;frame<work.Frames.Length;frame++) {
                            var page=actual.BrowsePage(category,work.Id,frame);using var stream=new MemoryStream(page.Jpeg);using var image=Image.FromStream(stream);Check(image.Width==1280&&image.Height==720,"Actual installed image invalid");
                        }
                        using var browser=new GalleryBrowserForm(actual,new DateOnly(2026,10,11),new GalleryDesktopImages(desktopIndex??Path.Combine(directory,"no-originals.json")));browser.StartPosition=FormStartPosition.Manual;browser.Location=new(-30000,-30000);browser.ShowInTaskbar=false;browser.Show(manager);
                        ((ComboBox)browser.Controls.Find("browse-category",true).Single()).SelectedIndex=category=="calligraphy"?1:0;
                        await Wait(()=>browser.PendingLoads==0&&browser.SelectedWorkId is not null);if(desktopIndex is not null)Check(browser.Controls.Find("browse-metadata",true).Single().Text.Contains("高清原图"),"Actual browser did not load original");Capture(browser,"installed-"+category);
                        using(var detectFull=new System.Windows.Forms.Timer{Interval=25}) {
                            Exception? actualFailure=null;bool captured=false;
                            detectFull.Tick+=async(_,_)=>{
                                var fullscreen=Application.OpenForms.OfType<GalleryImageFullscreenForm>().FirstOrDefault();if(fullscreen is null)return;detectFull.Stop();
                                try{
                                    fullscreen.WindowState=FormWindowState.Normal;fullscreen.Location=new(-30000,-30000);fullscreen.Size=new(1920,1080);fullscreen.ShowInTaskbar=false;fullscreen.PerformLayout();await Task.Delay(50);
                                    Capture(fullscreen,"installed-fullscreen-"+category);
                                    ((Button)fullscreen.Controls.Find("fullscreen-details",true).Single()).PerformClick();
                                    var reading=fullscreen.Controls.Find("fullscreen-reading",true).OfType<TabControl>().Single();reading.SelectedIndex=category=="calligraphy"?2:0;reading.Focus();Capture(fullscreen,"installed-details-"+category);
                                    CommandKey(fullscreen,Keys.Escape);Check(!reading.Visible,"Actual details did not return to artwork");
                                    fullscreen.View.ZoomAt(2,new((int)(fullscreen.View.ClientSize.Width*.75),(int)(fullscreen.View.ClientSize.Height*.5)));Capture(fullscreen,"installed-zoom-"+category);captured=true;
                                }catch(Exception ex){actualFailure=ex;}finally{fullscreen.Close();}
                            };
                            detectFull.Start();((Button)browser.Controls.Find("browse-fullscreen",true).Single()).PerformClick();
                            if(actualFailure is not null)throw actualFailure;Check(captured,"Actual fullscreen entry did not complete");
                        }
                        browser.Close();
                        Console.WriteLine($"GALLERY_BROWSER_INSTALLED_OK {category} works={catalog.Works.Length}; all pages read and decoded; read-only original files");
                    }
                }
            }catch(Exception ex){failure=ex;}finally{manager.Close();}
        };
        Application.Run(manager);if(failure is not null)throw new InvalidOperationException("Gallery browser validation failed",failure);
        File.WriteAllText(Path.Combine(directory,"report.json"),JsonSerializer.Serialize(new{entry=true,category=true,date=true,search=true,workNavigation=true,pageNavigation=true,completeOriginal=true,fullscreen=true,wheelZoom=true,pan=true,scales=new[]{100,150,200},windowSizes=new[]{"normal","minimum"},deviceCommands=false,firmwareBuilt=false},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine("GALLERY_BROWSER_UI_OK production manager entry; category/date/search; work/page navigation; collapsed reader; fullscreen/wheel/pan/reset/Esc; empty/missing/cancel/refresh states; normal/minimum 100/150/200 percent; no device commands");

        async Task Interact(GalleryBrowserForm browser) {
            T Control<T>(string name) where T:Control=>browser.Controls.Find(name,true).OfType<T>().Single();
            async Task Click(string name){Control<Button>(name).PerformClick();await Wait(()=>browser.PendingLoads==0);}
            await Wait(()=>browser.PendingLoads==0&&browser.SelectedWorkId is not null);
            Check(Control<ComboBox>("browse-category").SelectedIndex==1,"Default is not calligraphy");Control<ComboBox>("browse-category").SelectedIndex=0;await Wait(()=>browser.PendingLoads==0);
            Check(!Descendants(browser).Any(c=>c.Text.Contains("在 Tab5 查看")),"Unrequested device action present");
            Control<DateTimePicker>("browse-date").Value=new DateTime(2026,1,1);await Click("browse-find-date");Check(browser.SelectedWorkId=="painting0","Date selection failed");Capture(browser,"painting-reader");
            var stableImage=Control<GalleryImageView>("browse-image");int emptyFrames=0;EventHandler observe=(_,_)=>{if(!stableImage.HasImage)emptyFrames++;};stableImage.ViewChanged+=observe;
            Check(stableImage.ImeMode==ImeMode.Disable&&Control<ListBox>("browse-works").ImeMode==ImeMode.Disable,"Artwork browsing permits IME composition");
            Check(Control<TextBox>("browse-search").ImeMode!=ImeMode.Disable,"Search IME disabled");
            int revision=stableImage.ImageRevision;
            await Click("browse-next-page");Check(Control<Label>("browse-page-count").Text=="2 / 2 页","Page navigation failed");
            Check(emptyFrames==0&&stableImage.ImageRevision==revision+1,"Normal page turn cleared or replaced image more than once");stableImage.ViewChanged-=observe;
            Control<GalleryImageView>("browse-image").Focus();CommandKey(browser,Keys.Left);await Wait(()=>browser.PendingLoads==0);Check(Control<Label>("browse-page-count").Text=="1 / 2 页","Left key did not turn page");
            CommandKey(browser,Keys.Left);await Wait(()=>browser.PendingLoads==0);Check(Control<Label>("browse-page-count").Text=="2 / 2 页","Left boundary did not wrap");await Click("browse-next-page");Check(Control<Label>("browse-page-count").Text=="1 / 2 页","Mouse next boundary did not wrap");
            Control<TextBox>("browse-search").Focus();CommandKey(browser,Keys.I);Check(!Control<TabControl>("browse-reading").Visible,"Search I hijacked");CommandKey(browser,Keys.Right);Check(Control<Label>("browse-page-count").Text=="1 / 2 页","Search arrow hijacked");
            Control<DateTimePicker>("browse-date").Focus();CommandKey(browser,Keys.Right);Check(Control<Label>("browse-page-count").Text=="1 / 2 页","Date arrow hijacked");
            Control<GalleryImageView>("browse-image").Focus();CommandKey(browser,Keys.Right);await Wait(()=>browser.PendingLoads==0);Check(Control<Label>("browse-page-count").Text=="2 / 2 页","Right key did not turn page");
            Control<ListBox>("browse-works").SelectedIndex=1;await Wait(()=>browser.PendingLoads==0);Check(browser.SelectedWorkId=="painting1"&&!Control<Button>("browse-next-page").Enabled,"Work navigation retained old page");
            var image=Control<GalleryImageView>("browse-image");image.Focus();int singleRevision=image.ImageRevision;CommandKey(browser,Keys.Left);CommandKey(browser,Keys.Right);Check(browser.PendingLoads==0&&image.ImageRevision==singleRevision,"Single page reloaded");image.ZoomAt(3,new(120,80));image.ContextMenuStrip!.Items.Find("browse-fit",false).OfType<ToolStripMenuItem>().Single().PerformClick();Check(image.Zoom==1,"Image menu fit reset failed");
            Check(Control<TextBox>("browse-reading-原文").Text.Contains("暂无已核实原文"),"Unverified text appeared in UI");
            Control<TextBox>("browse-search").Text="核实中文名";await Wait(()=>browser.PendingLoads==0);Check(Control<ListBox>("browse-works").Items.Count==1&&browser.SelectedWorkId=="painting0","Displayed Chinese title search failed");
            Control<TextBox>("browse-search").Text="核实名";await Wait(()=>browser.PendingLoads==0);Check(Control<ListBox>("browse-works").Items.Count==1,"Displayed Chinese artist search failed");
            Control<TextBox>("browse-search").Text="乙作者";await Wait(()=>browser.PendingLoads==0);Check(Control<ListBox>("browse-works").Items.Count==18,"Author search failed");
            Control<TextBox>("browse-search").Text="不存在的作品";await Wait(()=>browser.PendingLoads==0);Check(Control<ListBox>("browse-works").Items.Count==0&&Control<Label>("browse-image-state").Text.Contains("没有匹配"),"Empty search failed");Capture(browser,"empty-search");
            Control<TextBox>("browse-search").Text="";await Wait(()=>browser.PendingLoads==0);
            Check(Control<ListBox>("browse-works").Items.Count==36,"Scrolling directory omits works");
            Control<ListBox>("browse-works").SelectedIndex=35;await Wait(()=>browser.PendingLoads==0);Check(browser.SelectedWorkId=="painting35","Last work cannot be reached by scrolling");
            Control<ComboBox>("browse-category").SelectedIndex=1;await Wait(()=>browser.PendingLoads==0);Control<DateTimePicker>("browse-date").Value=new DateTime(2026,1,1);await Click("browse-find-date");
            Check(browser.SelectedWorkId=="calligraphy0"&&Control<TextBox>("browse-reading-原文").Text.StartsWith(new string('文',2000)),"Category or complete original failed");
            Check(!Control<TabControl>("browse-reading").Visible,"Reader should be collapsed initially");await Click("browse-toggle-reading");
            CheckReadingKeys(browser,"browse",Control<GalleryImageView>("browse-image"));Control<TabControl>("browse-reading").SelectedIndex=2;Capture(browser,"calligraphy-original");await Click("browse-toggle-reading");
            bool fullscreenEntered=false;Exception? fullscreenFailure=null;
            using(var detect=new System.Windows.Forms.Timer{Interval=20}) {
                detect.Tick+=async(_,_)=>{
                    var full=Application.OpenForms.OfType<GalleryImageFullscreenForm>().FirstOrDefault();if(full is null)return;detect.Stop();fullscreenEntered=true;
                    try {
                        Check(full.FormBorderStyle==FormBorderStyle.None&&full.WindowState==FormWindowState.Maximized,"Fullscreen is not maximized/borderless");
                        full.WindowState=FormWindowState.Normal;full.StartPosition=FormStartPosition.Manual;full.Location=new(-30000,-30000);full.Size=new(1280,720);full.PerformLayout();
                        Check(full.View.HasImage,"Fullscreen lost selected image");full.View.ZoomAt(3,new(640,360));Check(full.View.Zoom==3,"Fullscreen zoom failed");Capture(full,"fullscreen-zoom");
                        Capture(full,"full-header-audit");Check(full.View.Width>=full.ClientSize.Width-8&&full.View.Height>=full.ClientSize.Height*.75,"Fullscreen controls reserve too much artwork space: "+full.View.Size+" / "+full.ClientSize);
                        ((Button)full.Controls.Find("fullscreen-fit",true).Single()).PerformClick();Check(full.View.Zoom==1,"Fullscreen reset failed");
                        int fullEmpty=0,fullRevision=full.View.ImageRevision;full.View.ViewChanged+=(_,_)=>{if(!full.View.HasImage)fullEmpty++;};
                        CommandKey(full,Keys.Right);await Wait(()=>browser.PendingLoads==0);Check(full.Controls.Find("fullscreen-page",true).OfType<Label>().Single().Text=="2 / 2 页"&&full.View.HasImage,"Fullscreen right key did not load page");
                        Check(fullEmpty==0&&full.View.ImageRevision==fullRevision+1,"Fullscreen page turn blanked or redundantly copied image");
                        for(int n=0;n<20;n++)CommandKey(full,n%2==0?Keys.Left:Keys.Right);
                        await Wait(()=>browser.PendingLoads==0);Check(fullEmpty==0&&Control<Label>("browse-page-count").Text=="2 / 2 页","Rapid page turns blanked image or committed stale request");
                        CommandKey(full,Keys.Right);await Wait(()=>browser.PendingLoads==0);Check(Control<Label>("browse-page-count").Text=="1 / 2 页","Fullscreen right boundary did not wrap");CommandKey(full,Keys.Left);await Wait(()=>browser.PendingLoads==0);Check(Control<Label>("browse-page-count").Text=="2 / 2 页"&&full.View.PreviousPage&&full.View.NextPage,"Fullscreen left boundary did not wrap");
                        ((Button)full.Controls.Find("fullscreen-details",true).Single()).PerformClick();
                        var reader=full.Controls.Find("fullscreen-reading",true).OfType<TabControl>().Single();Check(reader.Visible,"Fullscreen details did not open");CheckReadingKeys(full,"fullscreen",full.View);reader.SelectedIndex=2;
                        Check(full.Controls.Find("fullscreen-reading-原文",true).OfType<TextBox>().Single().Text.StartsWith(new string('文',2000)),"Fullscreen lost original text");Capture(full,"fullscreen-details");
                        CommandKey(full,Keys.Escape);Check(!reader.Visible&&!full.IsDisposed&&full.View.Zoom==1,"Esc should return to same artwork before leaving fullscreen");
                        CommandKey(full,Keys.Left);await Wait(()=>browser.PendingLoads==0);Check(Control<Label>("browse-page-count").Text=="1 / 2 页"&&full.View.HasImage,"Fullscreen left key failed");Capture(full,"fullscreen-navigation");
                        string missing=Path.Combine(GalleryPack.Root,"calligraphy","calligraphy0-1.jpg");File.Move(missing,missing+".held");
                        try{int beforeFailure=full.View.ImageRevision;CommandKey(full,Keys.Right);await Wait(()=>browser.PendingLoads==0);Check(full.View.ImageRevision==beforeFailure&&fullEmpty==0&&Control<Label>("browse-page-count").Text.Contains("翻页失败"),"Failed page did not preserve previous image");}
                        finally{File.Move(missing+".held",missing);}
                        CommandKey(full,Keys.Right);await Wait(()=>browser.PendingLoads==0);Check(Control<Label>("browse-page-count").Text=="2 / 2 页","Failed page could not be retried");
                        var key=new KeyEventArgs(Keys.Escape);typeof(Control).GetMethod("OnKeyDown",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.Invoke(full,[key]);Check(key.Handled,"Esc did not close fullscreen");
                    }catch(Exception ex){fullscreenFailure=ex;full.Close();}
                };
                detect.Start();Control<Button>("browse-fullscreen").PerformClick();
            }
            Check(fullscreenEntered,"Fullscreen button did not open viewer");if(fullscreenFailure is not null)throw fullscreenFailure;
            Check(Control<GalleryImageView>("browse-image").HasImage,"Fullscreen disposal removed browser image");
            Control<ComboBox>("browse-category").SelectedIndex=0;Control<ComboBox>("browse-category").SelectedIndex=1;await Wait(()=>browser.PendingLoads==0);Check(browser.SelectedWorkId?.StartsWith("calligraphy")==true,"Stale category load replaced latest choice");
            File.Move(Path.Combine(GalleryPack.Root,"calligraphy","calligraphy0-0.jpg"),Path.Combine(GalleryPack.Root,"calligraphy","calligraphy0-0.saved"));
            await Click("browse-refresh");Check(Control<Label>("browse-image-state").Text.Contains("作品读取失败"),"Missing image failure not visible");Capture(browser,"missing-image");
            File.Move(Path.Combine(GalleryPack.Root,"calligraphy","calligraphy0-0.saved"),Path.Combine(GalleryPack.Root,"calligraphy","calligraphy0-0.jpg"));await Click("browse-refresh");
            foreach(var tab in Control<TabControl>("browse-reading").TabPages.Cast<TabPage>())Check(tab.Controls.OfType<TextBox>().Single().ReadOnly,"Reader edits installed text");
        }
        void Capture(Form form,string name){using var image=new Bitmap(form.Width,form.Height);form.DrawToBitmap(image,new(Point.Empty,form.Size));image.Save(Path.Combine(directory,name+".png"));}
    }
    private static async Task Wait(Func<bool> ready){var start=Environment.TickCount64;while(!ready()){if(Environment.TickCount64-start>30000)throw new TimeoutException("Gallery browser did not finish loading");await Task.Delay(20);}}
    private static void CheckReadingKeys(Form form,string prefix,GalleryImageView image){
        var reader=form.Controls.Find(prefix+"-reading",true).OfType<TabControl>().Single();
        Check(image.ImeMode==ImeMode.Disable&&reader.ImeMode==ImeMode.Disable&&reader.TabPages.Cast<TabPage>().SelectMany(p=>p.Controls.OfType<TextBox>()).All(t=>t.ImeMode==ImeMode.Disable),"Reader permits IME composition");
        Check(image.Visible&&reader.Visible,"Details must retain visible artwork");
        if(form is GalleryImageFullscreenForm){
            var size=form.Size;form.Size=new(1920,1080);form.PerformLayout();
            Check(reader.PointToScreen(Point.Empty).X>=image.PointToScreen(Point.Empty).X+image.Width,"Wide reader overlaps artwork");
            Check(reader.Width<image.Width&&reader.Height>form.ClientSize.Height*.7,"Wide reading pane proportions invalid");
            form.Size=size;form.PerformLayout();
            Check(reader.PointToScreen(Point.Empty).Y>=image.PointToScreen(Point.Empty).Y+image.Height,"Narrow reader overlaps artwork");
        }
        int revision=image.ImageRevision;var zoom=image.Zoom;
        reader.SelectedIndex=0;reader.Focus();CommandKey(form,Keys.Left);Check(reader.SelectedIndex==2,"Chapter left boundary did not wrap");CommandKey(form,Keys.Right);Check(reader.SelectedIndex==0,"Chapter right boundary did not wrap");
        CommandKey(form,Keys.Right);Check(reader.SelectedIndex==1,"Right did not select appreciation");
        CommandKey(form,Keys.Right);Check(reader.SelectedIndex==2,"Right did not select original");
        var body=form.Controls.Find(prefix+"-reading-原文",true).OfType<TextBox>().Single();body.Focus();body.Select(0,5);
        CommandKey(form,Keys.Left);Check(reader.SelectedIndex==2,"Text selection arrow changed chapter");
        CommandKey(form,Keys.Alt|Keys.Left);Check(reader.SelectedIndex==1,"Alt left did not change chapter");
        CommandKey(form,Keys.Alt|Keys.Right);Check(reader.SelectedIndex==2,"Alt right did not change chapter");
        body.Select(0,0);body.ScrollToCaret();reader.Focus();int initialY=body.GetPositionFromCharIndex(0).Y;
        CommandKey(form,Keys.PageDown);Check(body.GetPositionFromCharIndex(0).Y<initialY,"PageDown did not scroll body");
        CommandKey(form,Keys.PageUp);Check(body.GetPositionFromCharIndex(0).Y==initialY,"PageUp did not restore body position");
        CommandKey(form,Keys.I);Check(!reader.Visible&&image.ImageRevision==revision&&image.Zoom==zoom,"I did not return to unchanged artwork");
        CommandKey(form,Keys.I);Check(reader.Visible,"I did not reopen details");
    }
    private static bool CommandKey(Form form,Keys key){object[] arguments=[Message.Create(form.Handle,0x100,(IntPtr)key,IntPtr.Zero),key];return (bool)form.GetType().GetMethod("ProcessCmdKey",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.Invoke(form,arguments)!;}
    private sealed class ZoomProbe : GalleryImageView {
        internal void Wheel(int delta,Point location)=>OnMouseWheel(new(MouseButtons.None,0,location.X,location.Y,delta));
        internal void Drag(Point start,Point end){OnMouseDown(new(MouseButtons.Left,1,start.X,start.Y,0));OnMouseMove(new(MouseButtons.Left,0,end.X,end.Y,0));OnMouseUp(new(MouseButtons.Left,1,end.X,end.Y,0));}
        internal void ClickArrow(Point point){OnMouseDown(new(MouseButtons.Left,1,point.X,point.Y,0));OnMouseUp(new(MouseButtons.Left,1,point.X,point.Y,0));}
        internal void DoubleClickArrow(Point point)=>OnMouseDoubleClick(new(MouseButtons.Left,2,point.X,point.Y,0));
    }
    private static void CheckZoom() {
        using var view=new ZoomProbe{Size=new(800,450)};view.SetImage(new Bitmap(1280,720));
        view.Wheel(120,new(400,225));Check(Math.Abs(view.Zoom-1.2)<.001,"Mouse wheel does not zoom");
        view.ZoomAt(3,new(400,225));var anchor=new Point(460,250);var before=view.ImageBounds;
        float x=(anchor.X-before.X)/before.Width,y=(anchor.Y-before.Y)/before.Height;
        view.Wheel(120,anchor);var after=view.ImageBounds;
        Check(Math.Abs(after.X+x*after.Width-anchor.X)<.01&&Math.Abs(after.Y+y*after.Height-anchor.Y)<.01,"Mouse anchor moved during zoom");
        var initial=view.ImageBounds;view.Drag(new(400,225),new(430,245));Check(Math.Abs(view.ImageBounds.X-initial.X-30)<.01&&Math.Abs(view.ImageBounds.Y-initial.Y-20)<.01,"Dragging does not pan image");
        view.Wheel(int.MaxValue,new(400,225));Check(view.Zoom==16,"Zoom upper bound failed");view.Wheel(int.MinValue,new(400,225));Check(view.Zoom==1,"Zoom lower bound failed");
        view.ResetView();Check(view.ImageBounds==new RectangleF(0,0,800,450),"Fit reset failed");
        int page=0,doubles=0;view.NavigatePage=direction=>page+=direction;view.NextPage=true;view.MouseDoubleClick+=(_,_)=>doubles++;
        view.ClickArrow(new(780,225));Check(page==1&&!view.Capture&&view.Zoom==1,"Overlay next arrow dragged instead of paging");
        view.ClickArrow(new(20,225));Check(page==1,"Disabled overlay previous arrow changed page");
        view.PreviousPage=true;view.ClickArrow(new(20,225));Check(page==0,"Overlay previous arrow failed");view.DoubleClickArrow(new(20,225));Check(doubles==0,"Double clicking arrow exits fullscreen");
        view.SetImage(null);view.Wheel(120,anchor);Check(!view.HasImage,"Empty image wheel failed");
        Console.WriteLine("GALLERY_ZOOM_OK actual wheel handler; pointer anchor; drag pan; bounds; fit reset; empty view");
    }
    private static IEnumerable<Control> Descendants(Control root){foreach(Control control in root.Controls){yield return control;foreach(var child in Descendants(control))yield return child;}}
    private static void ValidateLayout(Form form) {
        foreach(var label in Descendants(form).OfType<Label>().Where(l=>l.Visible&&l.AutoSize&&l.Text.Length>0))Check(label.Height+2>=label.GetPreferredSize(new(label.Width,0)).Height,"Clipped label: "+label.Name);
        foreach(var control in Descendants(form).Where(c=>c.Visible&&c is Button or ComboBox or TextBox or ListBox or DateTimePicker or TabControl)) {
            if(control is Button button)Check(TextRenderer.MeasureText(button.Text,button.Font).Width<=button.ClientSize.Width+2,"Clipped button: "+button.Name);
            var bounds=control.RectangleToScreen(control.ClientRectangle);
            for(Control? parent=control.Parent;parent is not null;parent=parent.Parent){var clip=parent.RectangleToScreen(parent.ClientRectangle);clip.Inflate(2,2);Check(clip.Contains(bounds),"Clipped control: "+control.Name+" by "+parent.GetType().Name);}
        }
    }
}
