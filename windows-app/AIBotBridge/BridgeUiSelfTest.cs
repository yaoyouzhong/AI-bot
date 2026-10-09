namespace AIBotBridge;

// Native Forms and synthetic callbacks: never change the Windows startup registration.
internal static class BridgeUiSelfTest
{
    internal static void Run(string directory)
    {
        Directory.CreateDirectory(directory);Exception? failure=null;
        using var loop=new Form{ShowInTaskbar=false,StartPosition=FormStartPosition.Manual,Location=new(-30000,-30000),Size=new(1,1)};
        loop.Shown+=async(_,_)=>{try{await StartupAsync();StatusLayout();}catch(Exception ex){failure=ex;}finally{loop.Close();}};
        Application.Run(loop);if(failure is not null)throw new InvalidOperationException("Bridge UI validation failed",failure);
        Console.WriteLine("BRIDGE_UI_OK startup pending/reentry/reload/verified enable/disable/failure/readback; status default shows at least eight complete rows; minimum/scaled layouts and footer checked; synthetic settings only");

        async Task StartupAsync()
        {
            bool enabled=false,fail=false,mismatch=false;int writes=0;bool? requested=null;
            var gate=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var store=new DeviceRegistryStore(Path.Combine(directory,"devices.json"));store.Migrate(null,false,false);
            using var form=new DeviceCenterForm(store,_=>new(false,"离线","",""),(_,_)=>{},(_,_)=>Task.CompletedTask,()=>{},_=>{},_=>enabled,async target=>{
                writes++;requested=target;await gate.Task;if(fail)throw new IOException("演示：系统拒绝保存。");if(!mismatch)enabled=target;
            });
            Show(form);form.ShowPage("bridge-settings");Application.DoEvents();Save(form,"startup-off");
            foreach(var card in Descendants(form).OfType<DeviceActionButton>().Where(c=>c.Visible)){
                var viewport=card.Parent!.Parent!;Check(viewport.ClientRectangle.Contains(viewport.RectangleToClient(card.RectangleToScreen(card.ClientRectangle))),"Default bridge settings hide a settings card");
            }
            var control=(StartupSettingControl)form.Controls.Find("startup-setting",true).Single();
            var toggle=(CheckBox)form.Controls.Find("startup-toggle",true).Single();
            var state=(Label)form.Controls.Find("startup-state",true).Single();
            var feedback=(Label)form.Controls.Find("startup-feedback",true).Single();
            Check(state.Text=="已关闭"&&!toggle.Checked,"Initial startup state/action are unclear");
            toggle.AccessibilityObject.DoDefaultAction();Application.DoEvents();
            Check(requested==true&&writes==1&&!toggle.Enabled&&toggle.Checked&&state.Text=="正在开启…","No immediate pending feedback");
            await control.ChangeAsync();Check(writes==1,"Repeated click changed startup while pending");
            form.Reload();Application.DoEvents();Check(ReferenceEquals(control,form.Controls.Find("startup-setting",true).Single())&&!toggle.Enabled,"Reload lost the pending startup action");
            Save(form,"startup-pending");gate.SetResult();
            var deadline=DateTime.UtcNow.AddSeconds(5);while(!toggle.Enabled){if(DateTime.UtcNow>deadline)throw new TimeoutException("Startup acknowledgement timed out");await Task.Delay(10);}
            Check(enabled&&state.Text=="已开启"&&toggle.Checked&&feedback.Text.StartsWith("已开启"),"Enable did not acknowledge its verified result");Save(form,"startup-on");
            await control.ChangeAsync();Check(requested==false&&!enabled&&feedback.Text.StartsWith("已关闭"),"Disable did not use an explicit target/acknowledgement");Save(form,"startup-disabled");
            fail=true;await control.ChangeAsync();Check(!enabled&&!toggle.Checked&&state.Text=="已关闭"&&toggle.Enabled&&feedback.Text.StartsWith("未能开启"),"Failure appeared successful or left the checkbox stuck");Save(form,"startup-error");
            fail=false;mismatch=true;await control.ChangeAsync();Check(!enabled&&feedback.Text.Contains("未生效"),"Unchanged system state appeared successful");
            enabled=true;control.RefreshState();Check(state.Text=="已开启"&&toggle.Checked&&feedback.Text=="","External state was not refreshed or retained a stale acknowledgement");
            foreach(float scale in new[]{1.5f,2f}){form.Scale(new SizeF(scale/(scale==2?1.5f:1f),scale/(scale==2?1.5f:1f)));form.Size=form.MinimumSize;Application.DoEvents();Save(form,$"startup-on-{scale:0.0}-minimum");}
            form.Close();
        }

        void StatusLayout()
        {
            string[] sources=["天气","股票","Codex 额度","Claude 额度","通义千问 额度","Kimi 额度","MiniMax 额度","DeepSeek 额度","智谱 额度","阶跃星辰 额度","百度 额度","小米 额度"];
            var health=new ConnectionObservation(DateTimeOffset.Now,null,"",0,0);
            BridgeDeviceStatus[] devices=[new("M5Stack TAB5","在线","Wi-Fi","0.2.150-ui",Enabled:true,Health:health),new("ESP8266 小屏","在线","USB","0.5.0",Enabled:true,Health:health)];
            foreach(float scale in new[]{1f,1.5f,2f})foreach(int count in new[]{0,2,8}){
                var view=new BridgeStatusView(Enumerable.Range(0,count).Select(i=>devices[i%2] with{Name=i<2?devices[i].Name:$"演示设备 {i+1}"}).ToArray(),sources.Select(s=>new BridgeDataStatus(s,"数据可用","10-08 14:23:21")).ToArray(),count,null);
                using var form=new BridgeStatusForm(()=>view);Show(form);if(scale!=1)form.Scale(new SizeF(scale,scale));form.RefreshStatus();Application.DoEvents();
                var data=(DataGridView)form.Controls.Find("status-data",true).Single();
                Save(form,$"status-{count}-devices-{scale:0.0}");
                Check(data.DisplayedRowCount(false)>=8,$"Default status window shows only {data.DisplayedRowCount(false)} whole rows at scale {scale}, devices {count}");
                form.Size=form.MinimumSize;form.RefreshStatus();Application.DoEvents();
                Save(form,$"status-{count}-devices-{scale:0.0}-minimum");Check(data.DisplayedRowCount(false)>=3,$"Minimum status window shows {data.DisplayedRowCount(false)} rows at scale {scale}, devices {count}");form.Close();
            }
        }
        void Save(Form form,string name)
        {
            using var bitmap=new Bitmap(form.Width,form.Height);form.DrawToBitmap(bitmap,new(Point.Empty,form.Size));bitmap.Save(Path.Combine(directory,name+".png"));
            // Other Device Center cards deliberately scroll; the complete startup row must remain visible.
            Control scope=form.Controls.Find("startup-setting",true).SingleOrDefault()??form;
            foreach(var control in Descendants(scope).Append(scope).Where(c=>c.Visible&&c is Button or CheckBox or Label or StartupSettingControl)){
                var bounds=control.RectangleToScreen(control.ClientRectangle);
                for(Control? parent=control.Parent;parent is not null;parent=parent.Parent){var clip=parent.RectangleToScreen(parent.ClientRectangle);clip.Inflate(2,2);Check(clip.Contains(bounds),$"Clipped {control.Text} {bounds} by {clip} in {name}");}
            }
        }
    }
    private static void Show(Form form){form.ShowInTaskbar=false;form.StartPosition=FormStartPosition.Manual;form.Location=new(-30000,-30000);form.Show();Application.DoEvents();}
    private static IEnumerable<Control> Descendants(Control parent){foreach(Control child in parent.Controls){yield return child;foreach(var nested in Descendants(child))yield return nested;}}
    private static void Check(bool ok,string message){if(!ok)throw new InvalidOperationException(message);}
}
