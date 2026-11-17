using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
namespace AIBotBridge;
internal static class UpdateUiSelfTest
{
    internal static void Run(string directory) {
        Directory.CreateDirectory(directory);byte[] firmware=new byte[2048];firmware[0]=0xe9;
        byte[] package=UpdateSelfTest.Zip(new(){["VERSION"]="0.6.0"u8.ToArray(),["firmware.bin"]=firmware});
        string name="AI-bot-0.6.0-firmware-materials.zip",hash=Convert.ToHexString(SHA256.HashData(package)).ToLowerInvariant();
        byte[] installer="MZ offline test fixture"u8.ToArray();
        string installerName="AIBotBridge-0.5.1-setup-win-x64.exe";
        string installerHash=Convert.ToHexString(SHA256.HashData(installer)).ToLowerInvariant();
        string releases=JsonSerializer.Serialize(new[]{UpdateSelfTest.Release("esp8266","0.6.0",name,package),
            UpdateSelfTest.Release("tab5","0.2.133-ui","TAB5-upgrade-0.2.133-ui.zip",[]),
            UpdateSelfTest.Release("bridge","0.5.1",installerName,installer),
            UpdateSelfTest.Release("gallery","2026.11.01","AI-bot-gallery-painting-standard-2026.11.01.zip",[1])});
        string installedPath=Path.Combine(GalleryPack.Root,"painting","PACK.json");
        Directory.CreateDirectory(Path.GetDirectoryName(installedPath)!);
        File.WriteAllText(installedPath,JsonSerializer.Serialize(new GalleryPackManifest(1,"2026.10.01","painting",1,1,[]),JsonDefaults.Options));
        var handler=new UpdateSelfTest.Handler();bool hold=false;var waiting=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        handler.Reply=async(request,token)=>{
            string path=request.RequestUri!.AbsolutePath;
            if(hold&&path.EndsWith("SHA256SUMS.txt"))await waiting.Task.WaitAsync(token);
            byte[] data=path.EndsWith("SHA256SUMS.txt")
                ?Encoding.UTF8.GetBytes(path.Contains("/bridge-v")?installerHash+"  "+installerName+"\n":hash+"  "+name+"\n")
                :path.EndsWith(".zip")?package:path.EndsWith(".exe")?installer:Encoding.UTF8.GetBytes(releases);
            return new(HttpStatusCode.OK){Content=new ByteArrayContent(data)};
        };
        using var service=new UpdateService(handler);
        var (manager,esp)=EspUpdateSelfTest.Fixture();EspUpdateSelfTest.Usb(manager,"0.5.0");
        UpdateDevice[] devices=[new("bridge","电脑端 AI-bot","0.5.1","运行中",true,"bridge","安装程序"),new("tab","M5Stack TAB5","0.2.131-ui","在线",true,"upgrade-tab5","在设备上确认安装"),UpdateDevice.From(esp,manager.View(esp))];
        int handoffs=0;string? target=null;
        using var form=new UpdateCenterForm(()=>devices,(d,p)=>{p.VerifyUnchanged();target=d.Id;if(d.Component=="bridge")return Task.FromResult(false);handoffs++;return Task.FromResult(true);},service);
        form.StartPosition=FormStartPosition.Manual;form.Location=new(-30000,-30000);form.ShowInTaskbar=false;
        var grid=(DataGridView)form.Controls.Find("updates",true).Single();var action=(Button)form.Controls.Find("download",true).Single();var state=(Label)form.Controls.Find("state",true).Single();
        var notes=(LinkLabel)form.Controls.Find("release-notes",true).Single();var requirement=(Label)form.Controls.Find("requirement",true).Single();
        async Task Wait(Func<bool> done){var until=DateTime.UtcNow.AddSeconds(20);while(!done()){if(DateTime.UtcNow>until)throw new TimeoutException("Update UI: "+state.Text);await Task.Delay(5);}}
        Exception? failure=null;
        // Use the same persistent UI message loop as production. DoEvents-only
        // pumping can uninstall the WinForms synchronization context between awaits.
        form.Shown+=async(_,_)=>{try {
        await Wait(()=>service.CheckedAt is not null);
        string? SelectedName()=>grid.CurrentRow?.Cells[0].Value?.ToString();
        devices[0]=devices[0] with {Version="0.5.0"};
        using(var focused=new UpdateCenterForm(()=>devices,(_,_)=>Task.FromResult(true),service)){
            focused.StartPosition=FormStartPosition.Manual;focused.Location=new(-30000,-30000);focused.ShowInTaskbar=false;focused.Show();
            var list=(DataGridView)focused.Controls.Find("updates",true).Single();
            focused.FocusUpdate(esp.Id);
            if(list.CurrentRow?.Cells[0].Value?.ToString()!=esp.Name)throw new Exception("Notification did not select the named device");
            focused.FocusUpdate(null);
            if(list.CurrentRow?.Cells[0].Value?.ToString()!=devices[0].Name||list.Rows[0].Cells[3].Value?.ToString()!="有更新")
                throw new Exception("Available updates were not prioritized");
        }
        form.FocusUpdate(UpdateCenterForm.GalleryId);
        await Wait(()=>SelectedName()=="屏保图库"&&action.Enabled&&action.Text=="管理图库…");
        if(grid.CurrentRow?.Cells[3].Value?.ToString()!="有更新"||!requirement.Text.Contains("2026.10.01 → 2026.11.01")||notes.Visible)
            throw new Exception("Gallery row did not show its update and version details");
        using(var capture=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(capture,new(Point.Empty,form.Size));capture.Save(Path.Combine(directory,"updates-gallery.png"));}
        bool openedGallery=false;
        form.BeginInvoke(()=> {
            if(Application.OpenForms.OfType<GalleryPackForm>().SingleOrDefault() is not {} manager)return;
            openedGallery=true;manager.Close();
        });
        action.PerformClick();
        if(!openedGallery||target is not null)throw new Exception("Gallery action went to the device installer instead of gallery management");
        form.FocusUpdate("bridge");
        await Wait(()=>action.Enabled&&SelectedName()==devices[0].Name);
        action.PerformClick();
        await Wait(()=>state.Text=="安装包已下载，尚未安装。");
        if(target!="bridge"||handoffs!=0)throw new Exception("Cancelled installer reported as handed off");
        using(var capture=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(capture,new(Point.Empty,form.Size));capture.Save(Path.Combine(directory,"installer-cancelled.png"));}
        devices[0]=devices[0] with {Version="0.5.1"};
        // Use row identities from here on: available rows can move as the device reconnects.
        void Select(string name){
            var row=grid.Rows.Cast<DataGridViewRow>().Single(r=>r.Cells[0].Value?.ToString()==name);
            grid.CurrentCell=row.Cells[0];
        }
        Select(esp.Name);await Wait(()=>action.Enabled);
        action.PerformClick();await Wait(()=>handoffs==1);
        if(target!=esp.Id)throw new Exception("Wrong device handoff");
        EspUpdateSelfTest.Usb(manager,null);devices[2]=UpdateDevice.From(esp,manager.View(esp));
        await Wait(()=>action.Enabled&&action.Text=="手动准备小屏升级");
        if(grid.CurrentRow?.Cells[3].Value?.ToString()?.Contains("有更新")==true)throw new Exception("Unknown version advertised as newer");
        using(var capture=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(capture,new(Point.Empty,form.Size));capture.Save(Path.Combine(directory,"legacy-esp.png"));}
        action.PerformClick();await Wait(()=>handoffs==2);
        await Wait(()=>action.Enabled);hold=true;waiting=new(TaskCreationOptions.RunContinuationsAsynchronously);action.PerformClick();await Wait(()=>!action.Enabled);
        EspUpdateSelfTest.Usb(manager,"0.5.0");devices[2]=UpdateDevice.From(esp,manager.View(esp));waiting.SetResult();await Wait(()=>state.Text.StartsWith("更新未继续"));
        if(handoffs!=2)throw new Exception("Changed version during download accepted");
        waiting=new(TaskCreationOptions.RunContinuationsAsynchronously);
        await Wait(()=>action.Enabled);hold=true;action.PerformClick();await Wait(()=>!action.Enabled);
        devices=devices.Where(d=>d.Id!=esp.Id).ToArray();waiting.SetResult();await Wait(()=>state.Text.StartsWith("更新未继续")&&grid.Rows.Count==3);
        if(handoffs!=2)throw new Exception("Removed device still received update");
        Select(devices[0].Name);await Wait(()=>!action.Visible);
        if(action.Enabled||requirement.Visible||!notes.Enabled)throw new Exception("Equal version advertises an upgrade or repeats status");
        foreach(int index in new[]{0,1}) {
            devices[index]=devices[index] with {Version=index==0?"99.0.0":"99.0.0-ui"};
            Select(devices[index].Name);await Wait(()=>grid.CurrentRow?.Cells[3].Value?.ToString()=="版本较新"&&!action.Visible&&notes.Text=="查看全部更新说明");
            if(action.Enabled||requirement.Visible||!notes.Enabled)throw new Exception("Newer local version advertises an upgrade");
            int before=handoffs;action.PerformClick();if(handoffs!=before)throw new Exception("Hidden downgrade action reached installer");
        }
        foreach(float scale in new[]{1f,1.5f,2f}) {
            using var view=new UpdateCenterForm(()=>devices,(_,_)=>Task.FromResult(true),service);view.StartPosition=FormStartPosition.Manual;view.Location=new(-30000,-30000);view.ShowInTaskbar=false;view.Show();
            var list=(DataGridView)view.Controls.Find("updates",true).Single();var detail=view.Controls.Find("update-actions",true).Single();
            await Wait(()=>list.Rows.Count==devices.Length+1&&!view.Controls.Find("download",true).Single().Visible);
            if(scale!=1)view.Scale(new SizeF(scale,scale));view.PerformLayout();await Task.Yield();
            int bottom=list.RectangleToScreen(list.ClientRectangle).Bottom;
            if(detail.RectangleToScreen(detail.ClientRectangle).Top-bottom>24*scale)throw new Exception("Unused grid space separates actions from rows");
            var footer=view.Controls.Find("update-footer",true).Single();
            int allowance=footer.Parent!.Padding.Bottom+2;
            await Wait(()=>view.RectangleToScreen(view.ClientRectangle).Bottom-footer.RectangleToScreen(footer.ClientRectangle).Bottom<=allowance);
            int empty=view.RectangleToScreen(view.ClientRectangle).Bottom-footer.RectangleToScreen(footer.ClientRectangle).Bottom;
            if(empty>allowance)throw new Exception("Unused space remains above or below footer: "+empty);
            using var bitmap=new Bitmap(view.Width,view.Height);view.DrawToBitmap(bitmap,new(Point.Empty,view.Size));bitmap.Save(Path.Combine(directory,$"updates-{scale}.png"));view.Close();
        }
        UpdateNotesSelfTest.Run(directory);
        }catch(Exception ex){failure=ex;}finally{form.Close();}};
        Application.Run(form);
        if(failure is not null)throw new InvalidOperationException("Update UI regression failed",failure);
        Console.WriteLine("UPDATE_UI_OK notification focus, gallery row and management, available-first ordering, cancelled installer, production ESP view, known and legacy downloads, changed/removed target blocked, equal/newer versions hide upgrade and repeated status, aggregate notes, content-height window, 1x/1.5x/2x captures; no real installation");
    }
}
