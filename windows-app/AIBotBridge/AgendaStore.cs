using System.Globalization;
using System.Text;
using System.Text.Json;

namespace AIBotBridge;

// Civil seconds deliberately match the calibrated TAB5 RTC, independently of UTC/DST.
internal sealed record AgendaItem(string Id,string Edit,string Title,long Due,int Repeat,int Lead,long Done=0,long SnoozeOccurrence=0,long SnoozeUntil=0,long End=0);
internal sealed record AgendaDocument(int Version,string Revision,AgendaItem[] Items);
internal sealed record AgendaAction(string Id,string Edit,long Occurrence,string Op,long Until=0);
internal sealed record AgendaEdit(string? BaseEdit,AgendaItem Item);
internal sealed record AgendaFork(string Id,string NewId);
internal sealed record AgendaDeletion(string Id,string Edit,string? BaseEdit);
internal sealed record AgendaView(AgendaItem Item,long Occurrence,long AlertAt,bool Complete,bool CalendarPending=false);
internal static class AgendaRules
{
    internal const int Limit=16;
    internal static long Civil(DateTime local)=>new DateTimeOffset(DateTime.SpecifyKind(local,DateTimeKind.Utc)).ToUnixTimeSeconds();
    internal static DateTime Date(long value)=>DateTimeOffset.FromUnixTimeSeconds(value).UtcDateTime;
    internal static TimeProvider Clock {get;set;}=TimeProvider.System;
    internal static long Now=>Civil(Clock.GetLocalNow().DateTime);
    // End=0 supports definitions from the earlier, unshipped preview.
    internal static long EndsAt(AgendaItem item,long occurrence)=>occurrence+(item.End==0?0:item.End-item.Due);
    internal static bool Expired(AgendaItem item,long now)=>now>0&&item.Due>0&&item.Repeat==0&&now>=EndsAt(item,item.Due);
    internal static bool ValidOccurrence(AgendaItem item,long occurrence) {
        if(item.Due==0)return occurrence==1;
        if(occurrence<item.Due||(occurrence-item.Due)%86400!=0)return false;
        var day=Date(occurrence).DayOfWeek;
        return item.Repeat switch {0=>occurrence==item.Due,1=>true,2=>day is not (DayOfWeek.Saturday or DayOfWeek.Sunday),3=>(occurrence-item.Due)%(7*86400)==0,4=>Tab5Calendar.IsWorkday(Date(occurrence))==true,_=>false};
    }
    internal static AgendaView View(AgendaItem item,long now) {
        if(item.Due==0)return new(item,1,0,item.Done>=1);
        long occurrence=item.Due;
        if(item.Repeat!=0) {
            long end=EndsAt(item,item.Due);
            occurrence=item.Due+(now>=end?(now-end)/86400+1:0)*86400;
            occurrence=Math.Max(occurrence,item.Done>0?item.Done+86400:0);
            bool found=false;
            for(int i=0;i<370;i++,occurrence+=86400) {
                if(item.Repeat==4&&Tab5Calendar.IsWorkday(Date(occurrence)) is null)return new(item,item.Due,long.MaxValue,false,true);
                if(ValidOccurrence(item,occurrence)&&occurrence>item.Done){found=true;break;}
            }
            if(!found)return new(item,item.Due,long.MaxValue,false,true);
        }
        if(item.Repeat!=0&&item.SnoozeOccurrence>item.Done&&item.SnoozeUntil>0&&EndsAt(item,item.SnoozeOccurrence)>now)occurrence=item.SnoozeOccurrence;
        long alert=occurrence-item.Lead*60;
        if(item.SnoozeOccurrence==occurrence&&item.SnoozeUntil>0)alert=item.SnoozeUntil;
        return new(item,occurrence,alert,item.Repeat==0&&(item.Done>=occurrence||now>=EndsAt(item,occurrence)));
    }
    internal static void Validate(AgendaItem[] items) {
        if(items.Length>Limit||items.Any(i=>i is null)||items.Select(i=>i.Id).Distinct().Count()!=items.Length)throw new InvalidOperationException("最多保存 16 项，事项标识不能重复。");
        foreach(var i in items) {
            if(!Guid.TryParseExact(i.Id,"N",out _)||!Guid.TryParseExact(i.Edit,"N",out _)||string.IsNullOrWhiteSpace(i.Title)||i.Title.Length>32||Encoding.UTF8.GetByteCount(i.Title)>96||i.Title.Any(char.IsControl))
                throw new InvalidOperationException("事项名称需为 1–32 个字符，不能包含换行。");
            if(i.Repeat is <0 or >4||i.Lead is <0 or >60||i.Due!=0&&(i.Due<Civil(new(2000,1,1))||i.Due>Civil(new(2099,12,31,23,59,59)))||i.Due==0&&(i.Repeat!=0||i.Lead!=0))
                throw new InvalidOperationException("请检查日期、重复方式和提前提醒时间。");
            if(i.End!=0&&(i.Due==0||i.End<=i.Due||i.End-i.Due>86400||i.End>Civil(new(2100,1,1,23,59,59))))
                throw new InvalidOperationException("结束时间须晚于开始时间，单次日程最长 24 小时。");
            bool StoredOccurrence(long value)=>i.Repeat==4?value>=i.Due&&(value-i.Due)%86400==0:ValidOccurrence(i,value);
            if(i.Done!=0&&!StoredOccurrence(i.Done)||i.SnoozeOccurrence!=0&&!StoredOccurrence(i.SnoozeOccurrence)||i.SnoozeUntil<0||i.SnoozeUntil>Civil(new(2100,2,1)))throw new InvalidDataException("事项处理状态无效。");
        }
    }
    internal static string RepeatLabel(int repeat)=>new[]{"不重复","每天","周一至周五","每周","工作日"}[repeat];
    internal static string Description(AgendaView view)=>view.CalendarPending?"工作日安排待更新，提醒暂停":view.Item.Due==0?"待办 · 无时间":Date(view.Occurrence).ToString("MM-dd HH:mm",CultureInfo.InvariantCulture)+"–"+Date(EndsAt(view.Item,view.Occurrence)).ToString(EndsAt(view.Item,view.Occurrence)/86400==view.Occurrence/86400?"HH:mm":"MM-dd HH:mm",CultureInfo.InvariantCulture);
}

internal sealed class AgendaStore
{
    internal static AgendaStore Shared {get;}=new();
    private readonly object _gate=new();
    private AgendaDocument? _document;
    internal string? Error {get;private set;}
    private AgendaDocument ReadRaw() {
        if(_document is not null)return _document;
        try {
            var value=UserPreferenceFile.Read<AgendaDocument>("agenda.json")??new(1,Guid.NewGuid().ToString("N"),[]);
            if(value.Version!=1||!Guid.TryParseExact(value.Revision,"N",out _)||value.Items is null)throw new InvalidDataException("事项文件版本无效。");
            AgendaRules.Validate(value.Items);Error=null;return _document=value;
        }catch(Exception ex)when(ex is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException or ArgumentException){Error="日程文件无法读取，原文件已保留："+ex.Message;throw new InvalidOperationException(Error,ex);}
    }
    private AgendaDocument Read()=>ReadRaw();
    // Reclaim only the slots required by an insertion. The caller commits the
    // removals and additions together; reads, timers and edits never prune.
    private static bool MakeRoom(List<AgendaItem> items,ISet<string> protectedIds) {
        int needed=items.Count-AgendaRules.Limit;if(needed<=0)return true;
        var victims=items.Where(i=>!protectedIds.Contains(i.Id)&&AgendaRules.Expired(i,AgendaRules.Now))
            .OrderBy(i=>AgendaRules.EndsAt(i,i.Due)).ThenBy(i=>i.Id,StringComparer.Ordinal).Take(needed).ToArray();
        if(victims.Length!=needed)return false;
        foreach(var victim in victims)items.Remove(victim);return true;
    }
    internal AgendaDocument Snapshot(){lock(_gate){var d=Read();return d with{Items=d.Items.ToArray()};}}
    internal object Summary(){try{var d=Snapshot();return new{version=1,revision=d.Revision,count=d.Items.Length};}catch(InvalidOperationException){return new{version=1,revision="",count=0};}}
    private void Commit(AgendaItem[] items) {
        AgendaRules.Validate(items);var next=new AgendaDocument(1,Guid.NewGuid().ToString("N"),items);
        UserPreferenceFile.Write("agenda.json",next);_document=next;Error=null;
    }
    internal void Save(AgendaItem item,string? expectedEdit) {
        lock(_gate){var d=Read();var old=d.Items.SingleOrDefault(i=>i.Id==item.Id);
            if(old?.Edit!=expectedEdit)throw new InvalidOperationException("事项已在其他窗口修改，请重新选择后编辑。");
            // Editing the definition creates a new generation; old offline actions cannot apply to it.
            item=item with{Edit=Guid.NewGuid().ToString("N"),Done=0,SnoozeOccurrence=0,SnoozeUntil=0};
            if(old is null&&AgendaRules.Expired(item,AgendaRules.Now))throw new InvalidOperationException("结束时间已过，请选择未来时间。");
            var items=(old is null?d.Items.Append(item):d.Items.Select(i=>i.Id==item.Id?item:i)).ToList();
            if(!MakeRoom(items,new HashSet<string>{item.Id}))throw new InvalidOperationException("已满 16 条，没有可清理的过期事项，请先手动删除。");
            Commit(items.ToArray());}
    }
    internal void Remove(string id,string edit){lock(_gate){var d=Read();if(!d.Items.Any(i=>i.Id==id&&i.Edit==edit))throw new InvalidOperationException("事项已变化，请刷新后重试。");Commit(d.Items.Where(i=>i.Id!=id).ToArray());}}
    internal void Apply(AgendaAction action) {lock(_gate){var d=Read();var item=d.Items.SingleOrDefault(i=>i.Id==action.Id&&i.Edit==action.Edit)??throw new InvalidOperationException("事项已修改或移除，旧操作未应用。");
        if(!AgendaRules.ValidOccurrence(item,action.Occurrence))throw new InvalidOperationException("事项日期已变化。");
        var next=action.Op switch {
            "complete" when action.Occurrence<=item.Done=>item,
            "complete"=>item with{Done=action.Occurrence,SnoozeOccurrence=item.SnoozeOccurrence>action.Occurrence?item.SnoozeOccurrence:0,SnoozeUntil=item.SnoozeOccurrence>action.Occurrence?item.SnoozeUntil:0},
            "snooze" when item.Done<action.Occurrence&&item.Due!=0&&action.Until>0&&action.Until<=AgendaRules.Now+86400&&action.Until>=action.Occurrence-item.Lead*60&&action.Until<AgendaRules.EndsAt(item,action.Occurrence) =>item with{SnoozeOccurrence=action.Occurrence,SnoozeUntil=item.SnoozeOccurrence==action.Occurrence?Math.Max(item.SnoozeUntil,action.Until):action.Until},
            "snooze" when item.Due!=0&&AgendaRules.Now>=AgendaRules.EndsAt(item,action.Occurrence)=>item,
            "snooze" when item.Done>=action.Occurrence=>item,
            _=>throw new InvalidOperationException("提醒操作无效或延后时间过长。")};
        if(next!=item)Commit(d.Items.Select(i=>i.Id==item.Id?next:i).ToArray());}}
    internal (int Status,object Body) Rpc(JsonElement root) {
        try{return root.TryGetProperty("op",out var op)&&op.ValueKind==JsonValueKind.String&&op.GetString()=="delete"?Delete(root):Sync(root);}
        catch(Exception ex)when(ex is IOException or UnauthorizedAccessException or InvalidOperationException){return(503,new{error="agenda_unavailable"});}
    }
    private (int Status,object Body) Delete(JsonElement root) {
        string? Field(string name)=>root.TryGetProperty(name,out var value)&&value.ValueKind==JsonValueKind.String?value.GetString():null;
        string? id=Field("id"),edit=Field("edit");
        if(!Guid.TryParseExact(id,"N",out _)||!Guid.TryParseExact(edit,"N",out _))return(400,new{error="invalid_agenda_delete"});
        lock(_gate) {
            var current=Read();var item=current.Items.SingleOrDefault(i=>i.Id==id);
            if(item is not null&&item.Edit!=edit)return(409,new{error="agenda_changed_keep_local"});
            if(item is not null)Commit(current.Items.Where(i=>i.Id!=id).ToArray());
            // A lost receipt can be retried with a new authenticated nonce.
            return(200,new{version=1,removed=new{id,edit}});
        }
    }
    private (int Status,object Body) Sync(JsonElement root) {
        if(!root.TryGetProperty("op",out var op)||op.ValueKind!=JsonValueKind.String||op.GetString()!="sync")return(400,new{error="invalid_agenda_operation"});
        bool workdaySupport=root.TryGetProperty("workdaySupport",out var support)&&support.ValueKind==JsonValueKind.Number&&support.TryGetInt32(out int capability)&&capability==1;
        if(!workdaySupport&&Snapshot().Items.Any(i=>i.Repeat==4))return(409,new{error="agenda_workdays_requires_update_keep_local"});
        var conflicts=new List<string>();var forks=new List<AgendaFork>();AgendaAction[] parsed=[];AgendaEdit[] edits=[];AgendaDeletion[] deletions=[];
        if(root.TryGetProperty("deletions",out var removals)) {
            if(removals.ValueKind!=JsonValueKind.Array||removals.GetArrayLength()>AgendaRules.Limit)return(400,new{error="invalid_agenda_deletions"});
            deletions=removals.Deserialize<AgendaDeletion[]>(JsonDefaults.Options)??[];
            if(deletions.Any(d=>d is null||!Guid.TryParseExact(d.Id,"N",out _)||!Guid.TryParseExact(d.Edit,"N",out _)||!string.IsNullOrEmpty(d.BaseEdit)&&!Guid.TryParseExact(d.BaseEdit,"N",out _))||deletions.Select(d=>d.Id).Distinct().Count()!=deletions.Length)return(400,new{error="invalid_agenda_deletions"});
        }
        if(root.TryGetProperty("actions",out var actions)) {
            if(actions.ValueKind!=JsonValueKind.Array||actions.GetArrayLength()>AgendaRules.Limit*2)return(400,new{error="invalid_agenda_actions"});
            // Parse and validate the entire envelope before applying any action.
            parsed=actions.Deserialize<AgendaAction[]>(JsonDefaults.Options)??[];
            if(parsed.Any(a=>a is null||a.Op is not ("complete" or "snooze")))return(400,new{error="invalid_agenda_actions"});
        }
        if(root.TryGetProperty("edits",out var definitions)) {
            if(definitions.ValueKind!=JsonValueKind.Array||definitions.GetArrayLength()>AgendaRules.Limit)return(400,new{error="invalid_agenda_edits"});
            edits=definitions.Deserialize<AgendaEdit[]>(JsonDefaults.Options)??[];
            if(!workdaySupport&&edits.Any(e=>e?.Item?.Repeat==4))return(409,new{error="agenda_workdays_requires_update_keep_local"});
            if(edits.Any(e=>e is null||e.Item is null||!string.IsNullOrEmpty(e.BaseEdit)&&!Guid.TryParseExact(e.BaseEdit,"N",out _)))return(400,new{error="invalid_agenda_edits"});
            try{AgendaRules.Validate(edits.Select(e=>e.Item).ToArray());}catch(InvalidOperationException){return(400,new{error="invalid_agenda_edits"});}
        }
        if(edits.Any(e=>deletions.Any(d=>d.Id==e.Item.Id))||parsed.Any(a=>deletions.Any(d=>d.Id==a.Id)))return(400,new{error="agenda_deleted_item_has_edits"});
        lock(_gate) {
            var current=Read();var items=current.Items.ToList();
            foreach(var removal in deletions) {
                var old=items.SingleOrDefault(i=>i.Id==removal.Id);
                if(old is not null) {
                    items.Remove(old);
                    if(old.Edit!=removal.Edit&&old.Edit!=removal.BaseEdit) {
                        // Keep a concurrent desktop edit under its deterministic
                        // edition ID. Retrying a lost receipt cannot recreate it.
                        var copy=old with{Id=old.Edit};var existing=items.SingleOrDefault(i=>i.Id==copy.Id);
                        if(existing is not null&&existing!=copy)return(409,new{error="agenda_delete_conflict_keep_local"});
                        if(existing is null)items.Add(copy);forks.Add(new(old.Id,copy.Id));
                    }
                }
                // An earlier device edit might have been forked by the host
                // before its receipt was lost. Delete that exact edition too.
                items.RemoveAll(i=>i.Id==removal.Edit&&i.Edit==removal.Edit);
            }
            foreach(var change in edits) {
                var proposed=change.Item with{Done=0,SnoozeOccurrence=0,SnoozeUntil=0};var old=items.SingleOrDefault(i=>i.Id==proposed.Id);
                bool SameDefinition(AgendaItem a,AgendaItem b)=>a.Id==b.Id&&a.Edit==b.Edit&&a.Title==b.Title&&a.Due==b.Due&&a.End==b.End&&a.Repeat==b.Repeat&&a.Lead==b.Lead;
                if(old is not null&&SameDefinition(old,proposed))continue; // Lost acknowledgements are safe to retry.
                if(old?.Edit==(string.IsNullOrEmpty(change.BaseEdit)?null:change.BaseEdit)) {
                    if(old is not null)items.Remove(old);items.Add(proposed);continue;
                }
                // Concurrent edit/deletion: preserve the device version as a distinct copy.
                // Its edition is also the deterministic copy ID, making retries idempotent.
                var copy=proposed with{Id=proposed.Edit};var existing=items.SingleOrDefault(i=>i.Id==copy.Id);
                if(existing is not null&&!SameDefinition(existing,copy))return(409,new{error="agenda_copy_conflict_keep_local"});
                if(existing is null)items.Add(copy);forks.Add(new(proposed.Id,copy.Id));
            }
            // Incoming offline edits (including elapsed ones) must all be
            // acknowledged. Only older host entries may supply extra slots.
            var protectedIds=edits.SelectMany(e=>new[]{e.Item.Id,e.Item.Edit}).ToHashSet();
            foreach(var fork in forks)protectedIds.Add(fork.NewId);
            if(!MakeRoom(items,protectedIds))return(409,new{error="agenda_full_keep_local"});
            if(!items.SequenceEqual(current.Items))Commit(items.ToArray());
            foreach(var raw in parsed) {
                var fork=forks.FirstOrDefault(f=>f.Id==raw.Id&&f.NewId==raw.Edit);var action=fork is null?raw:raw with{Id=fork.NewId};
                try{Apply(action);}catch(InvalidOperationException){conflicts.Add(action.Id);}
            }
            var d=Snapshot();return(200,new{version=1,editSupport=1,deleteSupport=1,retentionSupport=2,deleted=deletions.Select(x=>new{x.Id,x.Edit}),workdaySupport=1,revision=d.Revision,items=d.Items,conflicts,forks,
                workdayYears=Enumerable.Range(DateTime.Now.Year-1,3).Select(Tab5Calendar.HolidayYear).OfType<Tab5HolidayYear>()
                    .Select(y=>new{y.Year,y.Holidays,y.Workdays}).ToArray()});
        }
    }
}
