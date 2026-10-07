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
        string releases=JsonSerializer.Serialize(new[]{UpdateSelfTest.Release("esp8266","0.6.0",name,package),
            UpdateSelfTest.Release("tab5","0.2.133-ui","TAB5-upgrade-0.2.133-ui.zip",[]),
            UpdateSelfTest.Release("bridge","0.5.1","AIBotBridge-0.5.1-setup-win-x64.exe",[])});
        var handler=new UpdateSelfTest.Handler();bool hold=false;var waiting=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        handler.Reply=async(request,token)=>{
            string path=request.RequestUri!.AbsolutePath;
            if(hold&&path.EndsWith(".zip"))await waiting.Task.WaitAsync(token);
            byte[] data=path.EndsWith("SHA256SUMS.txt")?Encoding.UTF8.GetBytes(hash+"  "+name+"\n"):path.EndsWith(".zip")?package:Encoding.UTF8.GetBytes(releases);
            return new(HttpStatusCode.OK){Content=new ByteArrayContent(data)};
        };
        using var service=new UpdateService(handler);
        UpdateDevice[] devices=[new("bridge","电脑端 AI-bot","0.5.1","运行中",true,"bridge","安装程序"),new("tab","M5Stack TAB5","0.2.131-ui","在线",true,"upgrade-tab5","在设备上确认安装"),new("esp","ESP8266 小屏","0.5.0","在线",true,"flash","USB，先备份再升级")];
        int handoffs=0;string? target=null;
        using var form=new UpdateCenterForm(()=>devices,(d,p)=>{p.VerifyUnchanged();target=d.Id;handoffs++;return Task.CompletedTask;},service);
        form.StartPosition=FormStartPosition.Manual;form.Location=new(-30000,-30000);form.ShowInTaskbar=false;form.Show();
        var grid=(DataGridView)form.Controls.Find("updates",true).Single();var action=(Button)form.Controls.Find("download",true).Single();var state=(Label)form.Controls.Find("state",true).Single();
        void Wait(Func<bool> done){var until=DateTime.UtcNow.AddSeconds(20);while(!done()){if(DateTime.UtcNow>until)throw new TimeoutException("Update UI: "+state.Text);Application.DoEvents();Thread.Sleep(5);}Application.DoEvents();}
        Wait(()=>service.CheckedAt is not null);grid.CurrentCell=grid[0,2];Application.DoEvents();
        if(!action.Enabled)throw new Exception("Expected ESP upgrade action");action.PerformClick();Wait(()=>handoffs==1);
        if(target!="esp")throw new Exception("Wrong device handoff");
        Wait(()=>action.Enabled);hold=true;action.PerformClick();Wait(()=>!action.Enabled);
        devices=devices.Where(d=>d.Id!="esp").ToArray();waiting.SetResult();Wait(()=>state.Text.StartsWith("更新未继续"));
        if(handoffs!=1)throw new Exception("Removed device still received update");
        grid.CurrentCell=grid[0,0];Application.DoEvents();if(action.Enabled)throw new Exception("Equal bridge version update enabled");
        foreach(float scale in new[]{1f,1.5f,2f}) {
            using var view=new UpdateCenterForm(()=>devices,(_,_)=>Task.CompletedTask,service);view.StartPosition=FormStartPosition.Manual;view.Location=new(-30000,-30000);view.ShowInTaskbar=false;view.Show();
            if(scale!=1)view.Scale(new SizeF(scale,scale));view.PerformLayout();Application.DoEvents();
            using var bitmap=new Bitmap(view.Width,view.Height);view.DrawToBitmap(bitmap,new(Point.Empty,view.Size));bitmap.Save(Path.Combine(directory,$"updates-{scale}.png"));view.Close();
        }
        form.Close();Console.WriteLine("UPDATE_UI_OK actual check/download/button handoff, device removal during download blocked, equal version disabled, 1x/1.5x/2x captures; no real installation");
    }
}
