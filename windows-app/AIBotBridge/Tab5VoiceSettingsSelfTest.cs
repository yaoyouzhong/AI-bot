namespace AIBotBridge;

internal static class Tab5VoiceSettingsSelfTest
{
    private static readonly Tab5VoiceEnvironment Ready=new(
        [new("dji","DJI Mic Mini"),new("builtin","电脑内置麦克风",true),new("cable-out","CABLE Output (VB-Audio Virtual Cable)")],
        [new("cable-in","CABLE Input (VB-Audio Virtual Cable)")],true,true);
    private static void Check(bool condition,string name){if(!condition)throw new InvalidOperationException("Voice settings: "+name);}
    private static void Wait(Task task){
        var until=Environment.TickCount64+5000;
        while(!task.IsCompleted&&Environment.TickCount64<until){Application.DoEvents();Thread.Sleep(1);}
        Check(task.IsCompleted,"asynchronous operation completed");task.GetAwaiter().GetResult();
    }
    internal static void Run(string directory){
        Directory.CreateDirectory(directory);
        var initial=new Tab5VoiceSettings(false,"builtin","kept-shortcut");var stored=initial;int writes=0;bool busy=false;
        Tab5VoiceSettingsForm Form(Func<Task<Tab5VoiceEnvironment>>? inspect=null)=>new(initial,()=>busy,()=>stored,
            value=>{stored=value;writes++;},preview:true,inspect:inspect??(()=>Task.FromResult(Ready)));
        using(var form=Form()){
            Wait(form.ReloadAsync());Check(writes==0&&form.SelectedInput=="builtin","opening preserves the explicit legacy choice");
            form.SelectInput(Tab5VoiceAudio.SystemSelection);Wait(form.SaveAsync());
            Check(writes==1&&stored.DjiId==Tab5VoiceAudio.SystemSelection&&stored.Shortcut==initial.Shortcut&&stored.Enabled,"system default choice saves without losing shortcut");
        }
        writes=0;stored=initial;
        using(var form=Form()){
            Wait(form.ReloadAsync());form.SelectInput(Tab5VoiceAudio.Tab5Selection);form.Dispose();
            Check(writes==0&&stored==initial,"closing an edited form does not save");
        }
        using(var form=Form()){
            Wait(form.ReloadAsync());form.SelectInput("dji");stored=initial with {DjiId="other"};Wait(form.SaveAsync());
            Check(writes==0&&form.StatusText.Contains("设置已发生变化"),"concurrent settings change is preserved");
        }
        stored=initial;var pending=new TaskCompletionSource<Tab5VoiceEnvironment>(TaskCreationOptions.RunContinuationsAsynchronously);int calls=0;
        using(var form=Form(()=>++calls==1?Task.FromResult(Ready):pending.Task)){
            Wait(form.ReloadAsync());form.SelectInput("dji");var saving=form.SaveAsync();busy=true;pending.SetResult(Ready);Wait(saving);
            Check(writes==0&&form.StatusText.Contains("录音或设置"),"recording beginning during validation prevents save");busy=false;
        }
        calls=0;pending=new(TaskCreationOptions.RunContinuationsAsynchronously);
        using(var form=Form(()=>++calls==1?Task.FromResult(Ready):pending.Task)){
            Wait(form.ReloadAsync());var saving=form.SaveAsync();form.Dispose();pending.SetResult(Ready);Wait(saving);
            Check(writes==0,"late save result after disposal is ignored");
        }
        calls=0;
        using(var form=Form(()=>Task.FromResult(++calls==1?Ready:Ready with {Inputs=Ready.Inputs.Where(d=>d.Id!="builtin").ToArray()}))){
            Wait(form.ReloadAsync());Wait(form.SaveAsync());Check(writes==0&&form.StatusText.Contains("不可用"),"unplugged explicit input does not save or fall back");
        }
        calls=0;pending=new(TaskCreationOptions.RunContinuationsAsynchronously);
        using(var form=Form(()=>++calls==1?pending.Task:Task.FromResult(Ready))){
            var old=form.ReloadAsync();Wait(form.ReloadAsync());pending.SetResult(Ready with {Inputs=[]});Wait(old);
            Check(form.SaveAvailable&&form.SelectedInput=="builtin","late refresh cannot replace newer device metadata");
        }
        using(var form=Form(()=>Task.FromException<Tab5VoiceEnvironment>(new InvalidOperationException("fixture failure")))){
            Wait(form.ReloadAsync());Check(!form.SaveAvailable&&form.StatusText.Contains("原设置保留"),"enumeration failure keeps settings and disables save");
        }
        Check(System.Text.Json.JsonSerializer.Deserialize<Tab5VoiceSettings>("{\"DjiId\":\"builtin\"}")?.Provider=="doubao","legacy settings keep Doubao");
        stored=initial;writes=0;
        var local=Ready with {Outputs=[],DoubaoInstalled=false,DoubaoRunning=false,WindowsRecognizer="synthetic Chinese recognizer"};
        using(var form=Form(()=>Task.FromResult(local))){
            Wait(form.ReloadAsync());form.SelectProvider("windows");Wait(form.SaveAsync());
            Check(writes==1&&stored.Provider=="windows"&&stored.DjiId=="builtin"&&stored.Shortcut==initial.Shortcut,"Windows saves independently of Doubao and cable without losing microphone or shortcut");
        }
        stored=initial;writes=0;
        using(var form=Form(()=>Task.FromResult(local with {WindowsRecognizer=null}))){
            Wait(form.ReloadAsync());form.SelectProvider("windows");Wait(form.SaveAsync());
            Check(writes==0&&form.StatusText.Contains("简体中文识别器"),"missing Windows recognizer cannot save");
        }
        using(var form=Form(()=>Task.FromResult(local))){
            Wait(form.ReloadAsync());form.SelectProvider("windows");form.Dispose();
            Check(writes==0&&stored.Provider=="doubao","cancelled provider edit preserves original backend");
        }
        Check(local.Issue(Tab5VoiceAudio.Tab5Selection,"windows") is null,"Windows TAB5 route needs no virtual cable");
        Check(local.Issue("missing","windows") is not null&&local.Issue("builtin","unknown") is not null,"missing input and unknown backend rejected");
        var system=Tab5VoiceAudio.SystemSelection;
        Check(Tab5VoiceAudio.SelectInput(Ready.Inputs,system)?.Id=="builtin","system selection resolves current default");
        var changed=Ready.Inputs.Select(d=>d with {IsDefault=d.Id=="dji"}).ToArray();
        Check(Tab5VoiceAudio.SelectInput(changed,system)?.Id=="dji","system default resolves again for the next take");
        Check(!Tab5VoiceAudio.Inspect(Ready.Inputs.Select(d=>d with {IsDefault=d.Id=="cable-out"}).ToArray(),Ready.Outputs,system).Ready,"virtual default is rejected as a physical microphone");
        Check(!Tab5VoiceAudio.Inspect([..Ready.Inputs,new("dji-two","DJI Mic 2")],Ready.Outputs,"").Ready,"multiple DJI inputs require an explicit choice");
        Check(Tab5VoiceAudio.Inspect(Ready.Inputs.Where(d=>d.Id!="dji").ToArray(),Ready.Outputs,"").Ready,"automatic falls back to TAB5 when no DJI is present");
        Check(Ready.Issue(Tab5VoiceAudio.Tab5Selection) is null&&(Ready with {DoubaoInstalled=false,DoubaoRunning=false}).Issue(system) is not null,"recognition installation and route are checked separately");
        foreach(var (name,width,height,scale,env) in new[]{
            ("normal",560,480,1f,Ready),("minimum",520,420,1f,Ready),("wide",700,480,1f,Ready),
            ("long-device",560,480,1f,Ready with {Inputs=[new("builtin",new string('长',65),true)]}),
            ("unavailable",520,420,1f,Ready with {Inputs=[],Outputs=[],DoubaoInstalled=false,DoubaoRunning=false})})
            Render(directory,name,width,height,scale,env);
        Render(directory,"windows",560,480,1,local,"windows");
        Render(directory,"windows-missing",520,420,1,local with {WindowsRecognizer=null},"windows");
        Console.WriteLine("VOICE_SETTINGS_PARITY_OK source routing, legacy selection, async refresh/save/close/conflict/unplug, no microphones opened; child-control previews");
    }
    internal static void Preview(string path){
        string directory=Path.GetDirectoryName(Path.GetFullPath(path))!;Directory.CreateDirectory(directory);
        Render(directory,"single",560,480,1,Ready);
        File.Copy(Path.Combine(directory,"voice-settings-single.png"),path,true);
    }
    private static void Render(string directory,string name,int width,int height,float scale,Tab5VoiceEnvironment env,string provider="doubao"){
        var initial=new Tab5VoiceSettings(true,"builtin",Provider:provider);
        // Only a child form is shown inside an unshown control host: no desktop
        // top-level window, activation or user audio configuration is involved.
        using var host=new Panel{Size=new Size((int)(width*scale),(int)(height*scale))};host.CreateControl();
        using var form=new Tab5VoiceSettingsForm(initial,()=>false,()=>initial,_=>throw new InvalidOperationException("preview cannot save"),true,
            inspect:()=>Task.FromResult(env)){TopLevel=false,FormBorderStyle=FormBorderStyle.None,MinimumSize=Size.Empty,Size=new Size(width,height)};
        host.Controls.Add(form);form.Show();Wait(form.ReloadAsync());
        float dpiScale=form.DeviceDpi/96f;form.Size=new Size((int)(width*dpiScale*scale),(int)(height*dpiScale*scale));
        host.Size=form.Size;form.PerformLayout();host.PerformLayout();Application.DoEvents();
        Console.WriteLine($"VOICE_PREVIEW {name} dpi={form.DeviceDpi} size={form.Width}x{form.Height}");
        using var bitmap=new Bitmap(host.Width,host.Height,System.Drawing.Imaging.PixelFormat.Format24bppRgb);
        host.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size));
        // WM_PRINT omits hidden native ComboBox selected fields. Use the real
        // owner-draw routine on an opaque surface (GDI text has no alpha).
        using(var graphics=Graphics.FromImage(bitmap))foreach(var combo in Descendants(form).OfType<ComboBox>()){
            var origin=host.PointToClient(combo.PointToScreen(Point.Empty));
            var bounds=new Rectangle(origin.X+4,origin.Y+2,Math.Max(1,combo.Width-34),Math.Max(1,combo.Height-4));
            using(var background=new SolidBrush(Color.White))graphics.FillRectangle(background,bounds);
            Tab5VoiceSettingsForm.DrawPickerText(graphics,combo,bounds,combo.SelectedItem?.ToString(),combo.Enabled?SettingsWindow.Ink:SettingsWindow.Muted);
        }
        bitmap.Save(Path.Combine(directory,"voice-settings-"+name+".png"));
    }
    private static IEnumerable<Control> Descendants(Control control){foreach(Control child in control.Controls){yield return child;foreach(var descendant in Descendants(child))yield return descendant;}}
}
