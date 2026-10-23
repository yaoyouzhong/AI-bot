using System.Security.Cryptography;
using System.Text.Json;

namespace AIBotBridge;
internal static class AgendaSelfTest
{
    [System.Runtime.InteropServices.DllImport("user32.dll")]private static extern IntPtr SendMessage(IntPtr window,int message,IntPtr w,IntPtr l);
    [System.Runtime.InteropServices.DllImport("gdi32.dll",EntryPoint="GetObjectW")]private static extern int GetFontObject(IntPtr font,int bytes,out int height);
    private static int NativeHeight(Control c){var font=SendMessage(c.Handle,0x31,IntPtr.Zero,IntPtr.Zero);GetFontObject(font,4,out int height);return height;}
    private static void Check(bool ok,string message){if(!ok)throw new InvalidOperationException(message);}
    private static long At(string value)=>AgendaRules.Civil(DateTime.Parse(value,System.Globalization.CultureInfo.InvariantCulture));
    internal static void Run(string directory) {
        var originalClock=AgendaRules.Clock;try{AgendaRules.Clock=new FixtureClock(At("2026-10-15 12:00"));RunCore(directory);}finally{AgendaRules.Clock=originalClock;}
    }
    private sealed class FixtureClock(long now):TimeProvider {
        internal long Seconds=now;
        public override DateTimeOffset GetUtcNow()=>DateTimeOffset.FromUnixTimeSeconds(Seconds);
        public override TimeZoneInfo LocalTimeZone=>TimeZoneInfo.Utc;
    }
    private static void RunCore(string directory) {
        Directory.CreateDirectory(directory);var store=AgendaStore.Shared;
        AgendaItem Item(string title,long due,int repeat=0,int lead=0)=>new(Guid.NewGuid().ToString("N"),Guid.NewGuid().ToString("N"),title,due,repeat,lead,End:due==0?0:due+3600);
        var once=Item("客户会议",At("2026-10-15 15:00"),0,10);store.Save(once,null);once=store.Snapshot().Items.Single();
        Check(AgendaRules.View(once,At("2026-10-15 14:49")).AlertAt==At("2026-10-15 14:50"),"lead reminder mismatch");
        Check(!AgendaRules.View(once,At("2026-10-15 15:59:59")).Complete&&AgendaRules.View(once,At("2026-10-15 16:00")).Complete,"one-off must end exactly at its end time without an action");
        Check(AgendaRules.View(once,At("2026-10-15 14:50")).Occurrence==once.Due,"lead reminder completed the event early");
        var completed=new AgendaAction(once.Id,once.Edit,once.Due,"complete");store.Apply(completed);string revision=store.Snapshot().Revision;store.Apply(completed);
        Check(store.Snapshot().Revision==revision&&new AgendaStore().Snapshot().Items.Single().Done==once.Due,"completion lost after restart or duplicate changed state");
        store.Save(once with{Title="改期会议",Due=At("2026-10-16 15:00"),End=At("2026-10-16 16:00")},once.Edit);bool conflict=false;try{store.Apply(completed);}catch(InvalidOperationException){conflict=true;}
        Check(conflict&&store.Snapshot().Items.Single().Done==0,"stale offline completion overwrote edited schedule");
        var workday=Item("工作日复盘",At("2026-10-16 18:00"),2,5);
        Check(AgendaRules.View(workday,At("2026-10-17 10:00")).Occurrence==At("2026-10-19 18:00"),"weekend repeat did not skip");
        var weekly=Item("周会",At("2026-10-15 09:00"),3);
        Check(AgendaRules.View(weekly,At("2026-10-16 10:00")).Occurrence==At("2026-10-22 09:00"),"weekly anchor changed weekday");
        Check(AgendaRules.View(workday,At("2026-10-16 18:59:59")).Occurrence==workday.Due&&AgendaRules.View(workday,At("2026-10-16 19:00")).Occurrence==At("2026-10-19 18:00"),"workday did not advance automatically at end");
        var overnight=Item("跨午夜日程",At("2026-10-15 23:50"),1,10) with{End=At("2026-10-16 00:20")};
        Check(AgendaRules.View(overnight,At("2026-10-16 00:10")).Occurrence==overnight.Due&&AgendaRules.View(overnight,At("2026-10-16 00:20")).Occurrence==At("2026-10-16 23:50"),"overnight recurrence ended at midnight or failed to advance");
        var legacy=Item("旧预览事项",At("2026-10-15 15:00")) with{End=0};
        Check(AgendaRules.View(legacy,legacy.Due).Complete,"legacy definition without end must expire at its scheduled time");
        foreach(long invalidEnd in new[]{once.Due,once.Due-60,once.Due+86401}) {bool invalid=false;try{AgendaRules.Validate([once with{End=invalidEnd}]);}catch(InvalidOperationException){invalid=true;}Check(invalid,"invalid end time accepted");}
        var midnight=Item("跨日提醒",At("2026-10-15 00:05"),1,10);
        Check(AgendaRules.View(midnight,At("2026-10-15 23:56")).AlertAt==At("2026-10-15 23:55"),"next-day lead reminder missed");
        var snoozed=Item("夜间事项",At("2026-10-15 23:58"),1) with{SnoozeOccurrence=At("2026-10-15 23:58"),SnoozeUntil=At("2026-10-16 00:08")};
        Check(AgendaRules.View(snoozed,At("2026-10-16 00:01")).AlertAt==snoozed.SnoozeUntil,"midnight snooze was discarded");
        var day=Item("每日事项",At("2026-10-15 09:00"),1) with{Done=At("2026-10-15 09:00")};
        Check(AgendaRules.View(day,At("2026-10-15 09:01")).Occurrence==At("2026-10-16 09:00"),"completed repeat did not advance");
        store.Save(workday,null);store.Save(weekly,null);store.Save(Item("整理客户资料",0),null);store.Save(Item("客户会议",At("2026-10-15 15:00"),0,10),null);
        var pending=Item("回访客户",AgendaRules.Now-86460,1);store.Save(pending,null);pending=store.Snapshot().Items.Single(i=>i.Id==pending.Id);
        var olderCompletion=new AgendaAction(pending.Id,pending.Edit,pending.Due,"complete");store.Apply(olderCompletion);long until=AgendaRules.Now+600;
        store.Apply(new(pending.Id,pending.Edit,pending.Due+86400,"snooze",until));store.Apply(olderCompletion);
        Check(new AgendaStore().Snapshot().Items.Single(i=>i.Id==pending.Id).SnoozeUntil==until,"duplicate old completion erased newer snooze");
        var ending=Item("即将结束",AgendaRules.Now-3600) with{End=AgendaRules.Now+300};store.Save(ending,null);ending=store.Snapshot().Items.Single(i=>i.Id==ending.Id);
        bool beyondEnd=false;try{store.Apply(new(ending.Id,ending.Edit,ending.Due,"snooze",AgendaRules.Now+600));}catch(InvalidOperationException){beyondEnd=true;}Check(beyondEnd,"snooze crossed end time");
        // Remove this real-time boundary fixture before writing deterministic preview data.
        store.Remove(ending.Id,ending.Edit);
        var before=store.Snapshot();string savedBefore=File.ReadAllText(UserPreferenceFile.PathFor("agenda.json"));bool writeRejected=false;
        using(var held=new FileStream(UserPreferenceFile.PathFor("agenda.json"),FileMode.Open,FileAccess.ReadWrite,FileShare.None)) {
            try{store.Apply(new(pending.Id,pending.Edit,pending.Due+86400,"complete"));}catch(Exception ex)when(ex is IOException or UnauthorizedAccessException){writeRejected=true;}
        }
        Check(writeRejected&&store.Snapshot().Revision==before.Revision&&File.ReadAllText(UserPreferenceFile.PathFor("agenda.json"))==savedBefore,"failed atomic save changed visible or saved data");
        // Shared fixtures are read by the real firmware scheduler, not a copy of its implementation.
        var official=Item("调休工作日",At("2026-09-18 09:00"),4,1);
        Check(AgendaRules.View(official,At("2026-09-19 12:00")).Occurrence==At("2026-09-20 09:00"),"official Sunday makeup workday missing");
        var autumn=Item("国庆工作日",At("2026-10-01 09:00"),4,3);
        Check(AgendaRules.View(autumn,At("2026-10-01 08:00")).Occurrence==At("2026-10-08 09:00"),"official weekday holiday was not skipped");
        Check(AgendaRules.View(autumn,At("2026-10-10 08:00")).Occurrence==At("2026-10-10 09:00"),"official Saturday makeup workday missing");
        var unpublished=Item("下一年度工作日",At("2027-01-01 09:00"),4,1);
        Check(AgendaRules.View(unpublished,At("2027-01-01 08:00")).CalendarPending,"unpublished year was silently treated as weekdays");
        var cases=new List<object>();
        foreach(var item in new[]{once,workday,weekly,midnight,snoozed,day,overnight,legacy,official,autumn,unpublished,Item("未结束会议",At("2026-10-15 15:00"),0,10),Item("无日期待办",0)})
            foreach(var now in new[]{At("2026-10-15 00:01"),At("2026-10-15 14:50"),At("2026-10-15 15:59:59"),At("2026-10-15 16:00"),At("2026-10-15 23:56"),At("2026-10-16 00:01"),At("2026-10-16 00:20"),At("2026-10-17 12:00"),At("2026-10-19 18:00"),At("2026-09-19 12:00"),At("2026-09-20 08:00"),At("2026-10-01 08:00"),At("2026-10-10 08:00"),At("2027-01-01 08:00")}) {
                var view=AgendaRules.View(item,now);cases.Add(new{version=1,revision=Guid.NewGuid().ToString("N"),items=new[]{item},now,occurrence=view.Occurrence,alertAt=view.AlertAt,endsAt=AgendaRules.EndsAt(item,view.Occurrence),complete=view.Complete,calendarPending=view.CalendarPending,expired=AgendaRules.Expired(item,now)});
            }
        File.WriteAllText(Path.Combine(directory,"scheduler-fixtures.json"),JsonSerializer.Serialize(cases,JsonDefaults.Options));
        using(var rpc=JsonDocument.Parse(JsonSerializer.Serialize(new{op="sync",actions=new[]{completed}},JsonDefaults.Options))) {
            var response=store.Rpc(rpc.RootElement);var body=JsonSerializer.SerializeToElement(response.Body,JsonDefaults.Options);
            Check(response.Status==200&&body.GetProperty("conflicts").GetArrayLength()==1,"stale edit was silently acknowledged");
        }
        TestDeviceEdits(store);Tab5VoiceSelfTest.RunAgendaOwner();TestRpcAsync(directory).GetAwaiter().GetResult();
        // A broken file must stay intact, never replaced by an empty collection.
        string file=UserPreferenceFile.PathFor("agenda.json"),saved=File.ReadAllText(file);File.WriteAllText(file,"{broken");bool rejected=false;
        try{new AgendaStore().Snapshot();}catch(InvalidOperationException){rejected=true;}
        Check(rejected&&File.ReadAllText(file)=="{broken","corrupt agenda was overwritten");
        using(var request=JsonDocument.Parse("{\"op\":\"sync\"}"))Check(new AgendaStore().Rpc(request.RootElement).Status==503,"corrupt agenda returned a successful empty snapshot");
        File.WriteAllText(file,saved);
        ApplicationConfiguration.Initialize();
        var layouts=new List<object>();
        foreach(float scale in new[]{1f,1.5f,2f})foreach(var size in new[]{new Size(640,420),new Size(600,400)}) {
            using var form=new AgendaForm(store){Opacity=0};float deviceScale=form.DeviceDpi/96f;form.ClientSize=new Size((int)(size.Width*deviceScale),(int)(size.Height*deviceScale));form.Show();if(scale!=1)form.Scale(new SizeF(scale,scale));form.PerformLayout();Application.DoEvents();
            IEnumerable<Control> All(Control parent)=>parent.Controls.Cast<Control>().SelectMany(c=>new[]{c}.Concat(All(c)));
            var list=All(form).OfType<ListBox>().Single();int sample=list.Items.IndexOf("客户会议");if(list.Items.Count>0)list.SelectedIndex=sample>=0?sample:0;Application.DoEvents();
            Check(All(form).OfType<DateTimePicker>().Count()==2&&!All(form).OfType<Button>().Any(b=>b.Text.Contains("完成")),"start/end controls or completion removal missing");
            var save=All(form).OfType<Button>().Single(b=>b.Text=="保存事项");var bounds=form.RectangleToClient(save.RectangleToScreen(save.ClientRectangle));Check(form.ClientRectangle.Contains(bounds),"save action clipped");
            foreach(var native in All(form).Where(c=>c is ComboBox or TextBox or DateTimePicker))Check(NativeHeight(native)==-(int)Math.Round(native.Font.SizeInPoints*native.DeviceDpi/72f),"native editor font did not match monitor DPI");
            layouts.Add(new{requestedScale=scale,logicalWidth=size.Width,form.DeviceDpi,form.ClientSize,bodyFont=form.Font.SizeInPoints,saveVisible=true,fonts=All(form).Where(c=>c is Label or Button or ComboBox or TextBox or DateTimePicker).Select(c=>new{type=c.GetType().Name,c.Text,font=c.Font.Name,points=c.Font.SizeInPoints,c.DeviceDpi,nativeHeight=NativeHeight(c)}).ToArray()});
            using var bitmap=new Bitmap(form.Width,form.Height);bitmap.SetResolution(form.DeviceDpi,form.DeviceDpi);form.DrawToBitmap(bitmap,new(Point.Empty,form.Size));bitmap.Save(Path.Combine(directory,$"agenda-windows-{size.Width}-{scale*100:0}.png"));
            if(scale==1&&size.Width==640&&Environment.GetEnvironmentVariable("AIBOT_AGENDA_LIVE_CAPTURE")=="1") {
                form.TopMost=true;form.Opacity=1;form.Show();form.Activate();form.Refresh();Application.DoEvents();Thread.Sleep(500);using var live=new Bitmap(form.Width,form.Height);using var g=Graphics.FromImage(live);g.CopyFromScreen(form.Location,Point.Empty,form.Size);live.Save(Path.Combine(directory,"agenda-windows-live-175.png"));
            }
            form.Close();
        }
        File.WriteAllText(Path.Combine(directory,"windows-result.json"),JsonSerializer.Serialize(new{passed=true,cases=cases.Count,layouts,transportChecks="encrypted_rpc_dispatch_USB_WiFi_BLE_no_physical_IO"},new JsonSerializerOptions{WriteIndented=true}));
        TestRetention(store);
        Console.WriteLine("AGENDA_OK exact automatic end, start/end validation, overnight/weekday recurrence, bounded snooze, restart, stale-edition conflict, corrupt-file preservation, encrypted USB/Wi-Fi/BLE RPC and current-monitor native forms");
    }
    private static void TestRetention(AgendaStore store) {
        var clock=(FixtureClock)AgendaRules.Clock;long start=clock.Seconds;
        foreach(var item in store.Snapshot().Items)store.Remove(item.Id,item.Edit);
        AgendaItem Item(string name,long due,int repeat=0)=>new(Guid.NewGuid().ToString("N"),Guid.NewGuid().ToString("N"),name,due,repeat,0,End:due==0?0:due+60);
        var once=Item("过期保留",start);var repeat=Item("重复保留",start,1);var undated=Item("无时间保留",0);
        store.Save(once,null);store.Save(repeat,null);store.Save(undated,null);
        string previous=File.ReadAllText(UserPreferenceFile.PathFor("agenda.json"));clock.Seconds=start+60;
        using(var held=new FileStream(UserPreferenceFile.PathFor("agenda.json"),FileMode.Open,FileAccess.ReadWrite,FileShare.None))Check(store.Snapshot().Items.Length==3,"read attempted expiration write");
        Check(File.ReadAllText(UserPreferenceFile.PathFor("agenda.json"))==previous&&new AgendaStore().Snapshot().Items.Length==3,"expiry or reboot removed retained item");
        var retainedOnce=store.Snapshot().Items.Single(i=>i.Id==once.Id);store.Save(retainedOnce with{Title="过期仍可编辑"},retainedOnce.Edit);
        var later=Item("稍晚过期",clock.Seconds);store.Save(later,null);clock.Seconds+=60;
        while(store.Snapshot().Items.Length<15)store.Save(Item("未来事项",clock.Seconds+3600),null);
        store.Save(Item("第十六条",0),null);Check(store.Snapshot().Items.Length==16&&store.Snapshot().Items.Any(i=>i.Id==once.Id),"insertion into sixteenth slot pruned history");
        previous=File.ReadAllText(UserPreferenceFile.PathFor("agenda.json"));var incoming=Item("满额新增",0);
        using(var held=new FileStream(UserPreferenceFile.PathFor("agenda.json"),FileMode.Open,FileAccess.ReadWrite,FileShare.None)){bool failed=false;try{store.Save(incoming,null);}catch(Exception ex)when(ex is IOException or UnauthorizedAccessException){failed=true;}Check(failed,"failed replacement save reported success");}
        Check(File.ReadAllText(UserPreferenceFile.PathFor("agenda.json"))==previous&&store.Snapshot().Items.Any(i=>i.Id==once.Id),"failed replacement lost original item");
        store.Save(incoming,null);Check(store.Snapshot().Items.Length==16&&!store.Snapshot().Items.Any(i=>i.Id==once.Id)&&store.Snapshot().Items.Any(i=>i.Id==later.Id),"replacement did not remove exactly oldest expired item");
        var atCapacity=store.Snapshot().Items.Single(i=>i.Id==incoming.Id);store.Save(atCapacity with{Title="满额编辑"},atCapacity.Edit);Check(store.Snapshot().Items.Any(i=>i.Id==later.Id),"editing at capacity pruned history");
        JsonElement Sync(AgendaEdit[] edits,int expected=200){using var request=JsonDocument.Parse(JsonSerializer.Serialize(new{op="sync",workdaySupport=1,retentionSupport=2,edits},JsonDefaults.Options));var result=store.Rpc(request.RootElement);Check(result.Status==expected,"retention sync status");return JsonSerializer.SerializeToElement(result.Body,JsonDefaults.Options);}
        var offline=Item("离线已过期",start);var reply=Sync([new("",offline)]);
        Check(reply.GetProperty("retentionSupport").GetInt32()==2&&store.Snapshot().Items.Any(i=>i.Id==offline.Id)&&!store.Snapshot().Items.Any(i=>i.Id==later.Id),"offline expired edit discarded or wrong victim removed");
        string revision=store.Snapshot().Revision;Sync([new("",offline)]);Check(store.Snapshot().Revision==revision,"lost acknowledgement retried with extra eviction");
        store.Remove(offline.Id,offline.Edit);store.Save(Item("填满活动事项",0),null);
        revision=store.Snapshot().Revision;Sync([new("",Item("无可用空间",0))],409);Check(store.Snapshot().Revision==revision&&store.Snapshot().Items.Any(i=>i.Id==repeat.Id)&&store.Snapshot().Items.Any(i=>i.Id==undated.Id),"full active list lost recurring/undated entries");
        foreach(var item in store.Snapshot().Items)store.Remove(item.Id,item.Edit);
        for(int n=0;n<AgendaRules.Limit;n++)store.Save(Item("多条保留",clock.Seconds+n),null);
        clock.Seconds+=120;var batch=new[]{new AgendaEdit("",Item("批次一",0)),new AgendaEdit("",Item("批次二",0))};
        Sync(batch);Check(store.Snapshot().Items.Length==16&&store.Snapshot().Items.Count(i=>AgendaRules.Expired(i,clock.Seconds))==14,"batch over-pruned retained history");
        revision=store.Snapshot().Revision;Sync(batch);Check(store.Snapshot().Revision==revision,"batch retry pruned more records");
        Check(!Directory.EnumerateFiles(Path.GetDirectoryName(UserPreferenceFile.PathFor("agenda.json"))!,"agenda*.bak").Any(),"retention created a history backup");
        Console.WriteLine("AGENDA_RETENTION_OK under-limit retention, read/reboot, sixteenth insertion, oldest-only atomic replacement, edit retention, failed writes, elapsed offline edits, capacity rejection and idempotent batch sync");clock.Seconds=start;
    }
    internal static void LayoutSimulations(AgendaStore store,string directory) {
        Directory.CreateDirectory(directory);
        if(store.Snapshot().Items.Length==0)store.Save(new(Guid.NewGuid().ToString("N"),Guid.NewGuid().ToString("N"),"删除入口验收",0,0,0),null);
        IEnumerable<Control> All(Control parent)=>parent.Controls.Cast<Control>().SelectMany(c=>new[]{c}.Concat(All(c)));
        var results=new List<object>();
        {
            foreach(float scale in new[]{1f,1.5f,2f})foreach(var size in new[]{new Size(640,420),new Size(600,400)}) {
                var owned=new List<Font>();
                using(var form=new AgendaForm(store){Opacity=0}) {
                    form.ClientSize=size;form.Show();Check(form.DeviceDpi==96,"DPI simulation base must be 96");
                    var fonts=All(form).Select(c=>(Control:c,Font:c.Font)).ToArray();
                    if(scale!=1){form.Scale(new SizeF(scale,scale));foreach(var f in fonts){var scaled=new Font(f.Font.FontFamily,f.Font.SizeInPoints*scale,f.Font.Style);owned.Add(scaled);f.Control.Font=scaled;}}
                    form.PerformLayout();Application.DoEvents();
                    var save=All(form).OfType<Button>().Single(b=>b.Text=="保存事项");Check(form.ClientRectangle.Contains(form.RectangleToClient(save.RectangleToScreen(save.ClientRectangle))),"DPI simulation save clipped");
                    var remove=All(form).OfType<Button>().Single(b=>b.Text=="删除事项");var list=All(form).OfType<ListBox>().Single();Check(!remove.Enabled,"delete must require a selected item");list.SelectedIndex=0;Check(remove.Enabled,"selected item cannot be deleted");
                    for(Control? parent=remove.Parent;parent is not null;parent=parent.Parent)Check(parent.ClientRectangle.Contains(parent.RectangleToClient(remove.RectangleToScreen(remove.ClientRectangle))),"delete action clipped");
                    using(var capture=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(capture,new(Point.Empty,form.Size));capture.Save(Path.Combine(directory,$"agenda-{size.Width}-{scale*100:0}.png"));}
                    foreach(var input in All(form).Where(c=>c is DateTimePicker or ComboBox))for(Control? parent=input.Parent;parent is not null;parent=parent.Parent)
                        Check(parent.ClientRectangle.Contains(parent.RectangleToClient(input.RectangleToScreen(input.ClientRectangle))),$"DPI simulation editor clipped: {input.GetType().Name} {input.Text}; scale={scale} width={size.Width}; parent={parent.GetType().Name} {parent.ClientRectangle}; input={parent.RectangleToClient(input.RectangleToScreen(input.ClientRectangle))}");
                    foreach(var native in All(form).Where(c=>c is ComboBox or TextBox or DateTimePicker))Check(NativeHeight(native)==-(int)Math.Round(native.Font.SizeInPoints*96/72f),"DPI simulation native font mismatch");
                    using var image=new Bitmap(form.Width,form.Height);form.DrawToBitmap(image,new(Point.Empty,form.Size));image.Save(Path.Combine(directory,$"agenda-{size.Width}-{scale*100:0}.png"));
                    results.Add(new{scale,logicalWidth=size.Width,passed=true,note="font_and_geometry_simulation_at_96DPI_not_OS_monitor_change"});form.Close();
                }
                foreach(var font in owned)font.Dispose();
            }
        }
        File.WriteAllText(Path.Combine(directory,"result.json"),JsonSerializer.Serialize(results,new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine("AGENDA_LAYOUT_OK normal/minimum; 100/150/200pct font and geometry simulations; no OS DPI changes");
    }
    private static void TestDeviceEdits(AgendaStore store) {
        var original=store.Snapshot().Items.Select(i=>i.Id).ToHashSet();
        AgendaItem Definition(string title)=>new(Guid.NewGuid().ToString("N"),Guid.NewGuid().ToString("N"),title,AgendaRules.Now+3600,0,0,End:AgendaRules.Now+7200);
        JsonElement Sync(AgendaEdit[] edits,int status=200,int workdaySupport=0) {using var root=JsonDocument.Parse(JsonSerializer.Serialize(new{op="sync",edits,workdaySupport},JsonDefaults.Options));var reply=store.Rpc(root.RootElement);Check(reply.Status==status,"device edit RPC status mismatch");return JsonSerializer.SerializeToElement(reply.Body,JsonDefaults.Options);}
        var workday=Definition("调休工作日") with{Repeat=4};string beforeWorkday=store.Snapshot().Revision;
        Sync([new("",workday)],409);Check(store.Snapshot().Revision==beforeWorkday,"unsupported workday edit changed data");
        var calendar=Sync([new("",workday)],workdaySupport:1);Check(calendar.GetProperty("workdaySupport").GetInt32()==1&&calendar.GetProperty("workdayYears").GetArrayLength()>0,"official calendar missing from supported sync");
        Sync([],409);Check(store.Snapshot().Items.Any(i=>i==workday),"legacy sync removed workday definition");store.Remove(workday.Id,workday.Edit);
        var created=Definition("设备新建");Sync([new("",created)]);string revision=store.Snapshot().Revision;Sync([new("",created)]);
        Check(store.Snapshot().Revision==revision&&new AgendaStore().Snapshot().Items.Any(i=>i==created),"device create was not persisted/idempotent");
        var offline=created with{Edit=Guid.NewGuid().ToString("N"),Title="设备离线修改"};store.Save(created with{Title="电脑同时修改"},created.Edit);
        var fork=Sync([new(created.Edit,offline)]);Check(fork.GetProperty("forks")[0].GetProperty("newId").GetString()==offline.Edit,"concurrent edit did not preserve device copy");
        Check(store.Snapshot().Items.Any(i=>i.Id==created.Id&&i.Title=="电脑同时修改")&&store.Snapshot().Items.Any(i=>i.Id==offline.Edit&&i.Title==offline.Title),"one side of concurrent edit lost");
        revision=store.Snapshot().Revision;Sync([new(created.Edit,offline)]);Check(store.Snapshot().Revision==revision,"lost conflict acknowledgement duplicated/replaced a copy");
        var removed=Definition("删除前事项");Sync([new("",removed)]);store.Remove(removed.Id,removed.Edit);var local=removed with{Edit=Guid.NewGuid().ToString("N"),Title="删除后的本机修改"};Sync([new(removed.Edit,local)]);
        Check(!store.Snapshot().Items.Any(i=>i.Id==removed.Id)&&store.Snapshot().Items.Any(i=>i.Id==local.Edit),"offline edit silently disappeared or undid host deletion");
        var retry=Definition("写入重试");revision=store.Snapshot().Revision;
        using(var held=new FileStream(UserPreferenceFile.PathFor("agenda.json"),FileMode.Open,FileAccess.ReadWrite,FileShare.None))Sync([new("",retry)],503);
        Check(store.Snapshot().Revision==revision,"failed device edit save changed visible data");Sync([new("",retry)]);
        JsonElement Delete(AgendaItem item,int expected=200){using var request=JsonDocument.Parse(JsonSerializer.Serialize(new{op="delete",id=item.Id,edit=item.Edit},JsonDefaults.Options));var reply=store.Rpc(request.RootElement);Check(reply.Status==expected,"delete RPC status mismatch");return JsonSerializer.SerializeToElement(reply.Body,JsonDefaults.Options);}
        var deletable=Definition("设备删除");Sync([new("",deletable)]);Delete(deletable with{Edit=Guid.NewGuid().ToString("N")},409);Check(store.Snapshot().Items.Contains(deletable),"stale deletion changed item");
        using(var held=new FileStream(UserPreferenceFile.PathFor("agenda.json"),FileMode.Open,FileAccess.ReadWrite,FileShare.None))Delete(deletable,503);
        Check(store.Snapshot().Items.Contains(deletable),"failed delete write changed visible item");var receipt=Delete(deletable);Check(receipt.GetProperty("removed").GetProperty("id").GetString()==deletable.Id&&receipt.GetProperty("removed").GetProperty("edit").GetString()==deletable.Edit&&!store.Snapshot().Items.Contains(deletable),"delete receipt or persisted removal mismatch");
        revision=store.Snapshot().Revision;Delete(deletable);Check(store.Snapshot().Revision==revision,"duplicate delete rewrote state");Delete(deletable with{Id="invalid"},400);
        JsonElement SyncDeletes(AgendaDeletion[] deletions,int expected=200,AgendaEdit[]? edits=null) {
            using var request=JsonDocument.Parse(JsonSerializer.Serialize(new{op="sync",workdaySupport=1,deletions,edits=edits??[]},JsonDefaults.Options));var reply=store.Rpc(request.RootElement);Check(reply.Status==expected,"offline deletion sync status mismatch");return JsonSerializer.SerializeToElement(reply.Body,JsonDefaults.Options);
        }
        var offlineDelete=Definition("离线删除");Sync([new("",offlineDelete)]);var tombstone=new AgendaDeletion(offlineDelete.Id,offlineDelete.Edit,"");revision=store.Snapshot().Revision;
        using(var held=new FileStream(UserPreferenceFile.PathFor("agenda.json"),FileMode.Open,FileAccess.ReadWrite,FileShare.None))SyncDeletes([tombstone],503);
        Check(store.Snapshot().Revision==revision&&store.Snapshot().Items.Contains(offlineDelete),"failed deletion sync changed host");
        receipt=SyncDeletes([tombstone]);Check(receipt.GetProperty("deleteSupport").GetInt32()==1&&receipt.GetProperty("deleted")[0].GetProperty("edit").GetString()==offlineDelete.Edit&&!new AgendaStore().Snapshot().Items.Contains(offlineDelete),"offline delete not persisted/acknowledged");
        revision=store.Snapshot().Revision;SyncDeletes([tombstone]);Check(store.Snapshot().Revision==revision,"lost deletion receipt duplicated changes");
        var editedDelete=Definition("离线修改后删除");Sync([new("",editedDelete)]);SyncDeletes([new(editedDelete.Id,Guid.NewGuid().ToString("N"),editedDelete.Edit)]);Check(!store.Snapshot().Items.Any(i=>i.Id==editedDelete.Id),"deleted local edit resurrected host base edition");
        var simultaneous=Definition("删除时电脑已修改");Sync([new("",simultaneous)]);store.Save(simultaneous with{Title="电脑的新内容"},simultaneous.Edit);var newer=store.Snapshot().Items.Single(i=>i.Id==simultaneous.Id);
        receipt=SyncDeletes([new(simultaneous.Id,simultaneous.Edit,"")]);Check(!store.Snapshot().Items.Any(i=>i.Id==simultaneous.Id)&&store.Snapshot().Items.Contains(newer with{Id=newer.Edit})&&receipt.GetProperty("forks").GetArrayLength()==1,"concurrent desktop edit was lost or original deletion ignored");
        revision=store.Snapshot().Revision;SyncDeletes([new(simultaneous.Id,simultaneous.Edit,"")]);Check(store.Snapshot().Revision==revision,"deletion retry recreated concurrent copy");
        // A conflict copy created before a lost edit receipt is also removed.
        SyncDeletes([new(created.Id,offline.Edit,created.Edit)]);Check(!store.Snapshot().Items.Any(i=>i.Id==offline.Edit)&&store.Snapshot().Items.Any(i=>i.Title=="电脑同时修改"),"delete after lost fork receipt lost desktop changes or resurrected device copy");
        var neverSynced=Definition("未同步即删除");SyncDeletes([new(neverSynced.Id,neverSynced.Edit,"")]);Check(!store.Snapshot().Items.Contains(neverSynced),"unsynced deleted item recreated");
        revision=store.Snapshot().Revision;SyncDeletes([new("invalid",neverSynced.Edit,"")],400);SyncDeletes([new(neverSynced.Id,neverSynced.Edit,"")],400,[new("",neverSynced)]);Check(store.Snapshot().Revision==revision,"invalid deletion envelope partially committed");
        Sync([new("",Definition("非法结束") with{End=1})],400);
        while(store.Snapshot().Items.Length<AgendaRules.Limit){var extra=Definition("容量检查");store.Save(extra,null);}
        revision=store.Snapshot().Revision;Sync([new("",Definition("须留在本机"))],409);Check(store.Snapshot().Revision==revision&&store.Snapshot().Items.Length==16,"full host partially applied device edits");
        foreach(var item in store.Snapshot().Items.Where(i=>!original.Contains(i.Id)))store.Remove(item.Id,item.Edit);
    }
    private static async Task TestRpcAsync(string directory) {
        var pairing=new Tab5PairingStore(Path.Combine(directory,"rpc-test-pair.dat"));var pair=pairing.Pair("001122aabbcc",@"USB\AGENDA_TEST");byte[] key=Convert.FromBase64String(pair.Key);
        using var tasks=new Tab5CodexTasks(new Tab5CodexDesktop("unused-agenda-test"),()=>[],new Tab5CodexJournal(Path.Combine(directory,"rpc-draft.dat")));
        using var service=new Tab5Service(pairing,tasks);service.Publish(new(1,"12:00",1000,28800,DateTimeOffset.UtcNow,new("idle",0),new("idle",0)));
        using var frame=JsonDocument.Parse(service.CurrentFrame!);var session=frame.RootElement.GetProperty("session").GetString();
        foreach(var transport in new[]{"USB","Wi-Fi","BLE"}) {
            var definition=new AgendaItem(Guid.NewGuid().ToString("N"),Guid.NewGuid().ToString("N"),"设备链路新建",AgendaRules.Now+3600,0,0,End:AgendaRules.Now+7200);
            string nonce=Guid.NewGuid().ToString("N");byte[] clear=JsonSerializer.SerializeToUtf8Bytes(new{kind="agenda",op="sync",actions=Array.Empty<object>(),edits=new[]{new AgendaEdit("",definition)},session,issuedAt=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()},JsonDefaults.Options);
            byte[] packet=Tab5Protocol.Encrypt(key,nonce,clear);string proof=Tab5Protocol.Proof(key,$"POST|/tab5/v1/rpc|{pair.DeviceId}|{nonce}|{Convert.ToHexString(SHA256.HashData(packet)).ToLowerInvariant()}");
            var reply=await service.RpcAsync(pair.DeviceId,nonce,proof,packet,CancellationToken.None,transport:transport);Check(reply.Status==200&&reply.Packet is not null,"agenda RPC envelope failed "+transport);
            using var body=JsonDocument.Parse(Tab5Protocol.Decrypt(key,nonce,reply.Packet!));Check(body.RootElement.GetProperty("status").GetInt32()==200&&body.RootElement.GetProperty("body").GetProperty("items").GetArrayLength()>0,"agenda RPC content failed "+transport);
            Check(body.RootElement.GetProperty("body").GetProperty("editSupport").GetInt32()==1&&AgendaStore.Shared.Snapshot().Items.Any(i=>i==definition),"encrypted device definition was not applied "+transport);
            File.WriteAllText(Path.Combine(directory,"device-sync.json"),body.RootElement.GetProperty("body").GetRawText());
            var replay=await service.RpcAsync(pair.DeviceId,nonce,proof,packet,CancellationToken.None,transport:transport);using var replayBody=JsonDocument.Parse(Tab5Protocol.Decrypt(key,nonce,replay.Packet!));Check(replayBody.RootElement.GetProperty("status").GetInt32()==409,"agenda replay accepted");
            var denied=await service.RpcAsync(pair.DeviceId,Guid.NewGuid().ToString("N"),new string('0',64),packet,CancellationToken.None,transport:transport);Check(denied.Status==401,"invalid authentication accepted");
            string deleteNonce=Guid.NewGuid().ToString("N");byte[] deleteClear=JsonSerializer.SerializeToUtf8Bytes(new{kind="agenda",op="sync",workdaySupport=1,deletions=new[]{new AgendaDeletion(definition.Id,definition.Edit,"")},session,issuedAt=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()},JsonDefaults.Options);
            byte[] deletePacket=Tab5Protocol.Encrypt(key,deleteNonce,deleteClear);string deleteProof=Tab5Protocol.Proof(key,$"POST|/tab5/v1/rpc|{pair.DeviceId}|{deleteNonce}|{Convert.ToHexString(SHA256.HashData(deletePacket)).ToLowerInvariant()}");
            var deleteReply=await service.RpcAsync(pair.DeviceId,deleteNonce,deleteProof,deletePacket,CancellationToken.None,transport:transport);Check(deleteReply.Status==200&&deleteReply.Packet is not null,"encrypted deletion failed "+transport);
            using var deleteBody=JsonDocument.Parse(Tab5Protocol.Decrypt(key,deleteNonce,deleteReply.Packet!));var deleteReceipt=deleteBody.RootElement.GetProperty("body").GetProperty("deleted")[0];Check(deleteBody.RootElement.GetProperty("status").GetInt32()==200&&deleteReceipt.GetProperty("id").GetString()==definition.Id&&deleteReceipt.GetProperty("edit").GetString()==definition.Edit&&!AgendaStore.Shared.Snapshot().Items.Contains(definition),"encrypted deletion receipt mismatch "+transport);
        }
    }
}
