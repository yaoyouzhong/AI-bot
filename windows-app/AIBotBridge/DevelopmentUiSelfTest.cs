namespace AIBotBridge;
internal static class DevelopmentUiSelfTest
{
    internal static void Run(string directory) {
        AppPaths.BeginPublicSelfTest();Directory.CreateDirectory(directory);
        var value=new Tab5DisplaySettings("12345678",75,35,false,-1,true,15,5,true,[1,1,1,1,1,1,1,0],[0,1,2,3,4,5,6,7]);
        foreach(float scale in new[]{1f,1.5f,2f}) {
            using var display=new Tab5DisplaySettingsForm((_,_)=>Task.FromResult(value));Capture(display,"tab5-display",scale);
            using var minimum=new Tab5DisplaySettingsForm((_,_)=>Task.FromResult(value));minimum.Size=minimum.MinimumSize;Capture(minimum,"tab5-display-minimum",scale);
            using var updateService=new UpdateService(new UpdateSelfTest.Handler());
            using var updates=new UpdateCenterForm(()=>[new("bridge","电脑端 AI-bot","0.5.1","运行中",true,"bridge","安装程序"),new("a","M5Stack TAB5","0.2.53-ui","在线",true,"upgrade-tab5","在设备上确认"),new("b","ESP8266 小屏","兼容协议 v1","在线",true,"flash","连接 USB")],(_,_)=>Task.CompletedTask,updateService);Capture(updates,"updates",scale);
            var observation=new ConnectionObservation(DateTimeOffset.Now,DateTimeOffset.Now.AddMinutes(-2),"有效通信超时",2,1);
            using var health=new BridgeStatusForm(()=>new([new("M5Stack TAB5","在线","Wi-Fi","0.2.53-ui","a",true,observation)],[],1,null),_=>Task.CompletedTask,_=>{});Capture(health,"connection-health",scale);
            using var backup=new ConfigurationBackupForm(()=>{});Capture(backup,"configuration-backup",scale);
            using var backupMinimum=new ConfigurationBackupForm(()=>{});backupMinimum.Size=backupMinimum.MinimumSize;Capture(backupMinimum,"configuration-backup-minimum",scale);
            var notifications=new BridgeNotifications();notifications.Evaluate("completion","demo","任务完成",DateTimeOffset.Now);
            using var notification=new NotificationSettingsForm(notifications);Capture(notification,"notifications",scale);
            using var pins=new Tab5TaskPinsForm([new(Guid.NewGuid().ToString(),"检查任务回复与待处理操作","AI-bot",0)]);Capture(pins,"task-pins",scale);
        }
        void Capture(Form form,string name,float scale) {
            form.StartPosition=FormStartPosition.Manual;form.Location=new(-30000,-30000);form.ShowInTaskbar=false;form.Show();Application.DoEvents();
            if(scale!=1)form.Scale(new SizeF(scale,scale));form.PerformLayout();Application.DoEvents();
            if(form is Tab5DisplaySettingsForm)foreach(var control in Descendants(form).Where(c=>c.Visible&&c is Button or CheckBox or Label or ComboBox or NumericUpDown)) {
                var bounds=control.RectangleToScreen(control.ClientRectangle);
                for(Control? parent=control.Parent;parent is not null;parent=parent.Parent) {
                    var clip=parent.RectangleToScreen(parent.ClientRectangle);clip.Inflate(2,2);
                    if(!clip.Contains(bounds))throw new InvalidOperationException($"{name} {scale}: clipped {control.GetType().Name} {control.Text} {bounds} by {parent.GetType().Name} {clip}; form {form.Size}");
                }
            }
            using var image=new Bitmap(form.Width,form.Height);form.DrawToBitmap(image,new(Point.Empty,form.Size));image.Save(Path.Combine(directory,$"{name}-{scale:0.0}.png"));form.Close();
        }
        Console.WriteLine("DEVELOPMENT_UI_OK settings, update center and connection history captured at 100/150/200 percent");
    }
    private static IEnumerable<Control> Descendants(Control parent){foreach(Control child in parent.Controls){yield return child;foreach(var next in Descendants(child))yield return next;}}
}
