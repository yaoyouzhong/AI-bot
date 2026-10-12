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
    internal static void Run(string directory,string? actualPacks=null) {
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
                    ValidateLayout(browser);Capture(browser,$"layout-{scale:0.0}-{(minimum?"minimum":"normal")}");browser.Close();
                }
                if(actualPacks is not null) {
                    var actual=new Tab5Gallery(Path.Combine(directory,"empty-legacy"),actualPacks);
                    foreach(string category in new[]{"painting","calligraphy"}) {
                        var catalog=actual.Browse(category);Check(catalog.Works.Length>0,"Actual installed collection missing");
                        foreach(var work in catalog.Works)for(int frame=0;frame<work.Frames.Length;frame++) {
                            var page=actual.BrowsePage(category,work.Id,frame);using var stream=new MemoryStream(page.Jpeg);using var image=Image.FromStream(stream);Check(image.Width==1280&&image.Height==720,"Actual installed image invalid");
                        }
                        using var browser=new GalleryBrowserForm(actual,new DateOnly(2026,10,11));browser.StartPosition=FormStartPosition.Manual;browser.Location=new(-30000,-30000);browser.ShowInTaskbar=false;browser.Show(manager);
                        if(category=="calligraphy")((ComboBox)browser.Controls.Find("browse-category",true).Single()).SelectedIndex=1;
                        await Wait(()=>browser.PendingLoads==0&&browser.SelectedWorkId is not null);Capture(browser,"installed-"+category);
                        using(var fullscreen=new GalleryImageFullscreenForm(((GalleryImageView)browser.Controls.Find("browse-image",true).Single()).CopyImage()!,browser.Text)) {
                            fullscreen.StartPosition=FormStartPosition.Manual;fullscreen.Location=new(-30000,-30000);fullscreen.WindowState=FormWindowState.Normal;fullscreen.Size=new(1280,720);fullscreen.ShowInTaskbar=false;fullscreen.Show(manager);fullscreen.PerformLayout();await Task.Delay(50);
                            Capture(fullscreen,"installed-fullscreen-"+category);fullscreen.View.ZoomAt(2,new((int)(fullscreen.View.ClientSize.Width*.75),(int)(fullscreen.View.ClientSize.Height*.5)));Capture(fullscreen,"installed-zoom-"+category);fullscreen.Close();
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
            Check(!Descendants(browser).Any(c=>c.Text.Contains("在 Tab5 查看")),"Unrequested device action present");
            Control<DateTimePicker>("browse-date").Value=new DateTime(2026,1,1);await Click("browse-find-date");Check(browser.SelectedWorkId=="painting0","Date selection failed");Capture(browser,"painting-reader");
            await Click("browse-next-page");Check(Control<Label>("browse-page-count").Text=="2 / 2 页","Page navigation failed");
            Control<ListBox>("browse-works").SelectedIndex=1;await Wait(()=>browser.PendingLoads==0);Check(browser.SelectedWorkId=="painting1"&&!Control<Button>("browse-next-page").Enabled,"Work navigation retained old page");
            var image=Control<GalleryImageView>("browse-image");image.ZoomAt(3,new(120,80));image.ContextMenuStrip!.Items.Find("browse-fit",false).OfType<ToolStripMenuItem>().Single().PerformClick();Check(image.Zoom==1,"Image menu fit reset failed");
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
            Control<TabControl>("browse-reading").SelectedIndex=2;Capture(browser,"calligraphy-original");await Click("browse-toggle-reading");
            bool fullscreenEntered=false;Exception? fullscreenFailure=null;
            using(var detect=new System.Windows.Forms.Timer{Interval=20}) {
                detect.Tick+=(_,_)=>{
                    var full=Application.OpenForms.OfType<GalleryImageFullscreenForm>().FirstOrDefault();if(full is null)return;detect.Stop();fullscreenEntered=true;
                    try {
                        Check(full.FormBorderStyle==FormBorderStyle.None&&full.WindowState==FormWindowState.Maximized,"Fullscreen is not maximized/borderless");
                        full.WindowState=FormWindowState.Normal;full.StartPosition=FormStartPosition.Manual;full.Location=new(-30000,-30000);full.Size=new(1280,720);full.PerformLayout();
                        Check(full.View.HasImage,"Fullscreen lost selected image");full.View.ZoomAt(3,new(640,360));Check(full.View.Zoom==3,"Fullscreen zoom failed");Capture(full,"fullscreen-zoom");
                        ((Button)full.Controls.Find("fullscreen-fit",true).Single()).PerformClick();Check(full.View.Zoom==1,"Fullscreen reset failed");
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
    private sealed class ZoomProbe : GalleryImageView {
        internal void Wheel(int delta,Point location)=>OnMouseWheel(new(MouseButtons.None,0,location.X,location.Y,delta));
        internal void Drag(Point start,Point end){OnMouseDown(new(MouseButtons.Left,1,start.X,start.Y,0));OnMouseMove(new(MouseButtons.Left,0,end.X,end.Y,0));OnMouseUp(new(MouseButtons.Left,1,end.X,end.Y,0));}
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
        view.ResetView();Check(view.ImageBounds==new RectangleF(0,0,800,450),"Fit reset failed");view.SetImage(null);view.Wheel(120,anchor);Check(!view.HasImage,"Empty image wheel failed");
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
