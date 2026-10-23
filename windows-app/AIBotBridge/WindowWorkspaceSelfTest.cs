namespace AIBotBridge;

internal static class WindowWorkspaceSelfTest
{
    internal static void Run(string directory) {
        Directory.CreateDirectory(directory);
        void Check(bool condition,string why){if(!condition)throw new InvalidOperationException(why);}
        using var owner=new Form{Text="设备中心测试",ShowInTaskbar=false,StartPosition=FormStartPosition.Manual,Location=new(-30000,-30000)};
        var workspace=new WindowWorkspace();owner.FormClosing+=(_,e)=>{if(!e.Cancel&&!workspace.CloseAll())e.Cancel=true;};owner.Show();
        using var child=new Form{Text="ESP8266 小屏 · 数据设置",ShowInTaskbar=false,StartPosition=FormStartPosition.Manual,Location=new(-30000,-30000)};
        var input=new TextBox{Text="original"};child.Controls.Add(input);
        var confirm=UnsavedChanges.Confirm;int prompts=0;bool discard=false;
        try {
            UnsavedChanges.Confirm=_=>{prompts++;return discard;};UnsavedChanges.Track(child);
            var result=workspace.ShowAsync("device:esp:data",child,owner);Application.DoEvents();
            Check(owner.Enabled&&child.Owner==owner&&!child.Modal,"Ordinary child blocked owner or lost ownership");
            Check(workspace.Activate("device:esp:data")&&owner.OwnedForms.Length==1,"Repeated action duplicated window");
            input.Text="edited";owner.Close();Application.DoEvents();
            Check(!owner.IsDisposed&&!child.IsDisposed&&prompts>0,"Owner close discarded unsaved settings");
            UnsavedChanges.MarkSaved(child);child.DialogResult=DialogResult.OK;child.Close();Application.DoEvents();
            Check(result.IsCompletedSuccessfully&&result.Result==DialogResult.OK&&!workspace.Activate("device:esp:data"),"Modeless completion/save result lost");
            using var busy=new Form{ShowInTaskbar=false,StartPosition=FormStartPosition.Manual,Location=new(-30000,-30000)};
            bool working=true;busy.FormClosing+=(_,e)=>e.Cancel=working;workspace.Present("device:tab:upgrade",busy,owner);
            Check(!workspace.CloseAll()&&!busy.IsDisposed&&owner.Enabled,"Busy operation closed or locked owner");
            working=false;Check(workspace.CloseAll(),"Finished operation could not close");
            using(var service=new Tab5Service(new Tab5PairingStore())) {
                using var connection=new Tab5ConnectionForm(service,loadNetworks:false){ShowInTaskbar=false,StartPosition=FormStartPosition.Manual,Location=new(-30000,-30000)};
                workspace.Present("device:tab:connection",connection,owner);service.OtaTransferActive(true);
                Check(!workspace.CloseAll()&&!connection.IsDisposed,"Real TAB5 upgrade window closed during transfer");
                service.OtaTransferActive(false);Check(workspace.CloseAll(),"TAB5 window remained blocked after transfer");
            }
            var store=new DeviceRegistryStore(Path.Combine(directory,"devices.json"));store.Migrate(null,false,false);
            var esp=DeviceRegistryStore.Create(HardwareKind.Esp8266,"ESP8266 小屏",null);store.Add(esp);
            using var data=new DeviceDataForm(store,esp.Id){ShowInTaskbar=false,StartPosition=FormStartPosition.Manual,Location=new(-30000,-30000)};
            UnsavedChanges.TrackKnown(data);var saved=workspace.ShowAsync("device:esp:data",data,owner);Application.DoEvents();
            var toggle=data.Controls.Find("provider-deepseek",true).OfType<CheckBox>().Single();toggle.Checked=!toggle.Checked;bool expected=toggle.Checked;
            ((Button)data.Controls.Find("save-data",true).Single()).PerformClick();Application.DoEvents();
            Check(saved.IsCompletedSuccessfully&&saved.Result==DialogResult.OK&&store.Snapshot.Devices.Single(d=>d.Id==esp.Id).Providers.Contains("deepseek")==expected,"Modeless data save failed or targeted wrong device");
            discard=true;owner.Close();Check(owner.IsDisposed,"Owner could not close after children finished");
        } finally {UnsavedChanges.Confirm=confirm;}
        Console.WriteLine("WINDOW_WORKSPACE_OK modeless owner enabled, same-key reuse, immutable target, save result, unsaved close veto, busy-close protection, owner cleanup; synthetic only");
    }
}
