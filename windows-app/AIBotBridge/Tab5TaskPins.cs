namespace AIBotBridge;
internal static class Tab5TaskPins
{
    private static readonly object Gate=new();
    private static string[]? _cache;
    internal static string? Error {get;private set;}
    internal static string[] Load() {
        lock(Gate){if(_cache is not null)return _cache.ToArray();
            try{var pins=UserPreferenceFile.Read<string[]>("tab5-task-pins.json")??[];Validate(pins);_cache=pins;Error=null;}
            catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or InvalidOperationException){Error="常用任务配置未能读取："+ex.Message;_cache=[];}
            return _cache.ToArray();}
    }
    internal static void Validate(string[] pins){if(pins.Length>32||pins.Any(id=>!Guid.TryParseExact(id,"D",out _))||pins.Distinct(StringComparer.Ordinal).Count()!=pins.Length)throw new InvalidOperationException("常用任务最多 32 项，且任务标识不能重复。");}
    internal static void Save(string[] pins){Validate(pins);lock(Gate){UserPreferenceFile.Write("tab5-task-pins.json",pins);_cache=pins.ToArray();Error=null;}}
    internal static Tab5CodexTask[] Select(IReadOnlyList<Tab5CodexTask> catalog,int limit,string? overview,string? viewed,HashSet<string> ready) {
        var pins=Load().ToHashSet();var essentials=catalog.GroupBy(t=>t.ProjectId).Select(g=>g.First().Id).ToHashSet();
        var recent=catalog.OrderByDescending(t=>t.UpdatedAt).Take(5).Select(t=>t.Id).ToHashSet();
        return catalog.OrderByDescending(t=>t.Id==overview).ThenByDescending(t=>t.Id==viewed)
            .ThenByDescending(t=>recent.Contains(t.Id))
            .ThenByDescending(t=>pins.Contains(t.Id)).ThenByDescending(t=>essentials.Contains(t.Id)).ThenByDescending(t=>ready.Contains(t.Id))
            .Take(limit).OrderByDescending(t=>pins.Contains(t.Id)).Select(t=>t with {Pinned=pins.Contains(t.Id)}).ToArray();
    }
}
internal sealed class Tab5TaskPinsForm : Form
{
    internal Tab5TaskPinsForm(IReadOnlyList<Tab5CodexTask>? catalog=null) {
        Text="TAB5 · 常用任务";Font=new("Microsoft YaHei UI",9);AutoScaleMode=AutoScaleMode.Dpi;AutoScaleDimensions=new(96,96);ClientSize=new(560,390);MinimumSize=new(460,320);
        var tasks=catalog??Tab5CodexCatalog.Recent();var pins=Tab5TaskPins.Load().ToHashSet();
        var layout=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new(16),ColumnCount=1,RowCount=3};layout.RowStyles.Add(new(SizeType.AutoSize));layout.RowStyles.Add(new(SizeType.Percent,100));layout.RowStyles.Add(new(SizeType.AutoSize));layout.ColumnStyles.Add(new(SizeType.Percent,100));Controls.Add(layout);
        layout.Controls.Add(new Label{Text=Tab5TaskPins.Error??(tasks.Count==0?"暂无可选任务":""),AutoSize=true,Dock=DockStyle.Fill,Visible=Tab5TaskPins.Error is not null||tasks.Count==0},0,0);
        var list=new CheckedListBox{Dock=DockStyle.Fill,CheckOnClick=true,HorizontalScrollbar=true,IntegralHeight=false};foreach(var task in tasks)list.Items.Add($"{task.Folder} · {task.Title}",pins.Contains(task.Id));layout.Controls.Add(list,0,1);
        var save=new Button{Text="保存",AutoSize=true,Anchor=AnchorStyles.Right};layout.Controls.Add(save,0,2);
        save.Click+=(_,_)=>{try{var visible=tasks.Select(t=>t.Id).ToHashSet();var selected=pins.Where(id=>!visible.Contains(id)).Concat(list.CheckedIndices.Cast<int>().Select(i=>tasks[i].Id)).ToArray();Tab5TaskPins.Save(selected);DialogResult=DialogResult.OK;}catch(Exception ex){MessageBox.Show(this,ex.Message,"未能保存");}};SettingsWindow.FitScreen(this);
    }
}
