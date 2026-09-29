namespace AIBotBridge;

internal static class SettingsLayoutSelfTest
{
    internal static void CheckHiddenLaunch(string output)
    {
        using var form=new MigratedWeather.WeatherSettingsForm(new MigratedWeather.WeatherMonitor());
        form.UsePreviewData();form.ShowInTaskbar=false;form.StartPosition=FormStartPosition.Manual;
        form.Location=new Point(-30000,-30000);
        form.Show();Application.DoEvents();bool before=SettingsWindow.IsWindowVisible(form.Handle);
        SettingsWindow.Present(form);Application.DoEvents();bool after=SettingsWindow.IsWindowVisible(form.Handle);
        using var file=new FileStream(output,FileMode.CreateNew);
        System.Text.Json.JsonSerializer.Serialize(file,new{nativeVisibleBefore=before,nativeVisibleAfter=after,dpi=form.DeviceDpi});
        form.Close();if(!after)Environment.ExitCode=1;
    }
    internal static void Run(string directory)
    {
        if(!AppPaths.IsPublicSelfTest)AppPaths.BeginPublicSelfTest();
        directory=Path.GetFullPath(directory); Directory.CreateDirectory(directory);
        if(Environment.GetEnvironmentVariable("AIBOT_TAB5_LAYOUT_ONLY")=="1") {
            using var tab5Service=new Tab5Service(new Tab5PairingStore(Path.Combine(directory,"unused-pairing.dat")));
            if(Environment.GetEnvironmentVariable("AIBOT_TAB5_LAYOUT_OTA") is {Length:>0} otaFixture)tab5Service.OfferOta(otaFixture);
            using var tab5Form=new Tab5ConnectionForm(tab5Service,loadNetworks:false);
            using var wifiReply=System.Text.Json.JsonDocument.Parse("{\"version\":1,\"deviceId\":\"test-device\",\"networks\":[{\"ssid\":\"office-guest\",\"label\":\"单位\",\"connected\":true,\"selected\":true},{\"ssid\":\"home\",\"label\":\"家里\",\"connected\":false,\"selected\":false}]}");
            var rows=Tab5Service.ParseWifiList(wifiReply.RootElement,"test-device");
            if(rows.Length!=2||rows[0].Label!="单位"||!rows[0].Connected)throw new Exception("Saved network parser failed");
            bool mismatch=false;try{Tab5Service.ParseWifiList(wifiReply.RootElement,"other-device");}catch(IOException){mismatch=true;}
            if(!mismatch)throw new Exception("Wrong device Wi-Fi list accepted");
            var savedWifi=Descendants(tab5Form).OfType<ListView>().Single();
            foreach(var row in rows)savedWifi.Items.Add(new ListViewItem(new[]{row.Ssid,row.Label,row.Connected?"已连接":""}){Tag=row});
            var tabControl=Descendants(tab5Form).OfType<TabControl>().Single();
            for(int i=0;i<tabControl.TabCount;i++){tabControl.SelectedIndex=i;Capture(tab5Form,directory,"tab5-tab-"+i);}
            tab5Form.Size=tab5Form.MinimumSize;
            for(int i=0;i<tabControl.TabCount;i++){tabControl.SelectedIndex=i;Capture(tab5Form,directory,"tab5-narrow-tab-"+i);}
            Tab5Service.ValidateWifiLabel("synthetic-ssid","单位访客网");Tab5Service.ValidateWifiLabel("synthetic-ssid","");
            foreach(string invalid in new[]{new string('x',49),"bad\nlabel"}) {
                bool rejected=false;try{Tab5Service.ValidateWifiLabel("synthetic-ssid",invalid);}catch(ArgumentException){rejected=true;}
                if(!rejected)throw new Exception("Invalid Wi-Fi label accepted");
            }
            tab5Form.Show();Application.DoEvents();savedWifi.Items[0].Selected=true;Application.DoEvents();
            if(!Descendants(tab5Form).OfType<TextBox>().Any(t=>t.ReadOnly&&t.Text=="office-guest")||!Descendants(tab5Form).OfType<TextBox>().Any(t=>t.Text=="单位"))throw new Exception("Saved network selection did not populate fields");
            tab5Form.Close();tab5Form.Close();tab5Form.Dispose();
            Console.WriteLine("TAB5_LAYOUT_OK four tabs at normal/minimum size; horizontal bounds, label input validation; repeated close/dispose safe");return;
        }
        using(var weather=new MigratedWeather.WeatherSettingsForm(new MigratedWeather.WeatherMonitor())) {
            weather.UsePreviewData();
            Capture(weather,directory,"weather-default");
            weather.Size=weather.MinimumSize;
            Capture(weather,directory,"weather-narrow");
            // A taller error must wrap and remain reachable without moving the footer.
            weather.Font=new Font(weather.Font.FontFamily,12F);
            Capture(weather,directory,"weather-large-text");
        }
        using(var settings=new SettingsForm(BridgeSettings.CreatePublicSelfTestSettings())) {
            Capture(settings,directory,"settings"); settings.Size=settings.MinimumSize;
            Capture(settings,directory,"settings-narrow");
        }
        using(var control=new CycleSettingsForm(new SerialPublisher(null))) {
            Capture(control,directory,"device"); control.Size=control.MinimumSize;
            Capture(control,directory,"device-narrow");
        }
        using(var appearance=new DeviceAppearanceForm("pet",_=>{},_=>{},_=>{},_=>{})) {
            Capture(appearance,directory,"appearance");appearance.Size=appearance.MinimumSize;
            Capture(appearance,directory,"appearance-narrow");
        }
        using(var service=new Tab5Service(new Tab5PairingStore(Path.Combine(directory,"unused-pairing.dat"))))
        using(var connection=new Tab5ConnectionForm(service,loadNetworks:false)) {
            Capture(connection,directory,"tab5"); connection.Size=connection.MinimumSize;
            Capture(connection,directory,"tab5-narrow");
        }
        using(var cycle=new CycleSettingsForm())Capture(cycle,directory,"cycle");
        using(var about=new AboutForm())Capture(about,directory,"about");
        using(var pets=new PetGalleryForm(_=>{},load:false))Capture(pets,directory,"pets");

        using(var flash=new FirmwareFlashForm(preview:true)) {
            Capture(flash,directory,"flash");flash.Show();flash.ShowCompletedPreview();Capture(flash,directory,"flash-details");
        }
        using(var birthdays=new BirthdaySettingsForm(preview:true)) {
            Capture(birthdays,directory,"birthdays");birthdays.Size=birthdays.MinimumSize;
            Capture(birthdays,directory,"birthdays-narrow");
        }
        BirthdaySettingsForm.SelfTest();
        using(var trend=new QuotaTrendForm()) {
            Capture(trend,directory,"trend");trend.Size=trend.MinimumSize;Capture(trend,directory,"trend-narrow");
        }
        using(var runtime=new BridgeRuntime(startRefresh:false))
        using(var authorization=new DomesticQuotaAuthForm(runtime,initializeBrowser:false)) {
            Capture(authorization,directory,"authorization");authorization.Size=authorization.MinimumSize;
            Capture(authorization,directory,"authorization-narrow");
        }
        foreach(var provider in MigratedDomestic.DomesticProviderCatalog.All)
        using(var domestic=new MigratedDomestic.DomesticQuotaAuthForm(new MigratedDomestic.DomesticQuotaService(),hideOnUserClose:false,initialProviderId:provider.Id,initializeBrowser:false)) {
            Capture(domestic,directory,"provider-"+provider.Id);domestic.Size=domestic.MinimumSize;Capture(domestic,directory,"provider-"+provider.Id+"-narrow");
        }
        using(var runtime=new BridgeRuntime(startRefresh:false))
        using(var mirror=new MirrorForm(runtime.Capture,()=>"codex"))Capture(mirror,directory,"mirror");
        using(var draft=new Tab5VoiceDraft(()=>"LeftAltSpace")) {draft.InitializeDraft();Capture(draft,directory,"voice-draft");}
        using(var voice=new Tab5VoiceHost(new Tab5VoiceSettings(false,"","LeftAltSpace")))
        using(var form=voice.CreateSettings(preview:true)) {
            Capture(form,directory,"voice");form.Size=form.MinimumSize;Capture(form,directory,"voice-narrow");
        }

        int calls=0; bool closed=false;
        ContextMenuStrip? menu=null;
        using var window=new Form { StartPosition=FormStartPosition.Manual,Location=new Point(-30000,-30000),ShowInTaskbar=false };
        menu=TrayMenu.Build(_=>{calls++;closed=!menu!.Visible;SettingsWindow.Present(window);},_=>{},()=>"auto",()=>"test");
        using(menu) {
            menu.Show(new Point(-30000,-30000)); Application.DoEvents();
            var content=menu.Items.OfType<ToolStripMenuItem>().Single(i=>i.Text=="内容设置");
            var weather=content.DropDownItems.OfType<ToolStripMenuItem>().Single(i=>i.Text=="设置天气");
            weather.DropDownItems[0].PerformClick();
            if(calls!=0)throw new InvalidOperationException("Window opened before menu close completed.");
            Application.DoEvents();
            if(calls!=1||!closed||!window.Visible)throw new InvalidOperationException("First click did not open the form exactly once.");
            window.WindowState=FormWindowState.Minimized; SettingsWindow.Present(window); Application.DoEvents();
            if(window.WindowState!=FormWindowState.Normal)throw new InvalidOperationException("Minimized form not restored.");
            window.Close();
        }
        CheckClosing();
        Console.WriteLine("SETTINGS_LAYOUT_OK all settings, authorization shells, flash, trend and voice; first-click, minimized restore, horizontal bounds, fixed footer; no login/save/flash/recording");
    }

    static void CheckClosing()
    {
        Func<Form>[] factories=[
            ()=>{var form=new MigratedWeather.WeatherSettingsForm(new MigratedWeather.WeatherMonitor());form.UsePreviewData();return form;},
            ()=>new SettingsForm(BridgeSettings.CreatePublicSelfTestSettings()),
            ()=>new CycleSettingsForm(), ()=>new AboutForm()];
        foreach(var factory in factories)foreach(bool modal in new[]{false,true})foreach(int action in new[]{0,1,2}) {
            using var form=factory();form.ShowInTaskbar=false;form.StartPosition=FormStartPosition.Manual;form.Location=new Point(-30000,-30000);
            bool closed=false;form.FormClosed+=(_,_)=>closed=true;
            void Cancel() {
                if(action==0)((Button)form.CancelButton!).PerformClick();
                else if(action==1)typeof(Form).GetMethod("ProcessDialogKey",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.Invoke(form,[Keys.Escape]);
                else form.Close();
            }
            if(modal) {form.Shown+=(_,_)=>form.BeginInvoke(Cancel);form.ShowDialog();}
            else {form.Show();Application.DoEvents();Cancel();Application.DoEvents();}
            if(!closed||form.Visible)throw new InvalidOperationException($"{form.GetType().Name} cancel/escape/close failed: modal={modal} action={action}");
        }
        Console.WriteLine("SETTINGS_CLOSE_OK 24 cases: Cancel, Escape, Close for modeless and modal weather/settings/cycle/about");
    }

    static void Capture(Form form,string directory,string name)
    {
        form.ShowInTaskbar=false; form.StartPosition=FormStartPosition.Manual;form.Location=new Point(-30000,-30000);
        form.Show(); form.PerformLayout(); Application.DoEvents();
        CheckHorizontal(form,name);
        foreach(var button in Descendants(form).OfType<Button>().Where(b=>b.Visible))
            if(button.Height>Math.Ceiling(44*form.DeviceDpi/96d))throw new InvalidOperationException(name+": stretched action button "+button.Text);
        if(form is MigratedWeather.WeatherSettingsForm) {
            var fields=Descendants(form).OfType<TextBox>().ToArray();
            if(fields.Select(f=>f.Width).Distinct().Count()!=1)throw new InvalidOperationException(name+": unequal field widths");
            var footer=Descendants(form).Single(c=>c.Name=="weather-actions");
            var rect=form.RectangleToClient(footer.RectangleToScreen(footer.ClientRectangle));
            if(!form.ClientRectangle.Contains(rect))throw new InvalidOperationException(name+": footer outside window");
            foreach(var label in Descendants(form).OfType<Label>().Where(l=>l.Visible)) {
                var preferred=label.GetPreferredSize(new Size(label.Width,0));
                if(label.Height+2<preferred.Height)throw new InvalidOperationException(name+": clipped label "+label.Text);
            }
        }
        using var bitmap=new Bitmap(form.Width,form.Height);
        form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,form.Size)); bitmap.Save(Path.Combine(directory,name+".png"));
        form.Hide();
    }
    static IEnumerable<Control> Descendants(Control parent)
    {
        foreach(Control child in parent.Controls) {yield return child;foreach(var nested in Descendants(child))yield return nested;}
    }
    static void CheckHorizontal(Control parent,string name)
    {
        foreach(Control child in parent.Controls) {
            if(!child.Visible)continue;
            if(child.Left < -2 || child.Right>parent.ClientSize.Width+2)
                throw new InvalidOperationException($"{name}: {child.GetType().Name} {child.Bounds} outside {parent.GetType().Name} {parent.ClientSize}");
            CheckHorizontal(child,name);
        }
    }
}
