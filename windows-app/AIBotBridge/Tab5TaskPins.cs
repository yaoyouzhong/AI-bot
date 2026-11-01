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
    private readonly DataGridView _list=new();
    internal string EditFingerprint=>string.Join("|",_list.Rows.Cast<DataGridViewRow>().Where(r=>r.Cells[0].Value is true).Select(r=>(string)r.Tag!));
    internal Tab5TaskPinsForm(IReadOnlyList<Tab5CodexTask>? catalog=null) {
        Text="TAB5 · 常用任务";Font=SettingsWindow.BodyFont();AutoScaleMode=AutoScaleMode.Dpi;AutoScaleDimensions=new(96,96);
        ClientSize=new(680,540);MinimumSize=new(540,440);
        var tasks=catalog??Tab5CodexCatalog.Recent();var pins=Tab5TaskPins.Load().ToHashSet();
        var visible=tasks.Select(t=>t.Id).ToHashSet();var retained=pins.Where(id=>!visible.Contains(id)).ToArray();
        var layout=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new(24),ColumnCount=1,RowCount=3};
        layout.RowStyles.Add(new(SizeType.AutoSize));layout.RowStyles.Add(new(SizeType.Percent,100));layout.RowStyles.Add(new(SizeType.AutoSize));layout.ColumnStyles.Add(new(SizeType.Percent,100));Controls.Add(layout);
        layout.Controls.Add(new Label{Text=Tab5TaskPins.Error??(tasks.Count==0?"暂无可选任务":"勾选后在 TAB5 优先显示，最多 32 项。"),AutoSize=true,Dock=DockStyle.Fill,ForeColor=SettingsWindow.Muted,Margin=new(0,0,0,12)},0,0);
        _list.Name="task-pin-list";_list.Dock=DockStyle.Fill;_list.Margin=Padding.Empty;
        _list.AllowUserToAddRows=_list.AllowUserToDeleteRows=_list.AllowUserToResizeRows=false;
        _list.RowHeadersVisible=false;_list.ScrollBars=ScrollBars.Vertical;_list.MultiSelect=false;
        _list.SelectionMode=DataGridViewSelectionMode.FullRowSelect;
        _list.AutoSizeRowsMode=DataGridViewAutoSizeRowsMode.AllCells;
        _list.DefaultCellStyle.WrapMode=DataGridViewTriState.True;_list.DefaultCellStyle.Padding=new Padding(6,5,6,5);
        _list.DefaultCellStyle.Alignment=DataGridViewContentAlignment.MiddleLeft;
        _list.ColumnHeadersHeightSizeMode=DataGridViewColumnHeadersHeightSizeMode.AutoSize;
        _list.Columns.Add(new DataGridViewCheckBoxColumn{Name="pinned",HeaderText="",Width=36,FlatStyle=FlatStyle.Flat,DefaultCellStyle=new DataGridViewCellStyle{Padding=Padding.Empty,Alignment=DataGridViewContentAlignment.MiddleCenter},AutoSizeMode=DataGridViewAutoSizeColumnMode.None,Resizable=DataGridViewTriState.False});
        _list.Columns.Add(new DataGridViewTextBoxColumn{Name="project",HeaderText="项目",ReadOnly=true,AutoSizeMode=DataGridViewAutoSizeColumnMode.Fill,FillWeight=25,MinimumWidth=90});
        _list.Columns.Add(new DataGridViewTextBoxColumn{Name="task",HeaderText="任务",ReadOnly=true,AutoSizeMode=DataGridViewAutoSizeColumnMode.Fill,FillWeight=75,MinimumWidth=160});
        foreach(DataGridViewColumn column in _list.Columns)column.SortMode=DataGridViewColumnSortMode.NotSortable;
        foreach(var task in tasks){int index=_list.Rows.Add(pins.Contains(task.Id),task.Folder,task.Title);_list.Rows[index].Tag=task.Id;_list.Rows[index].Cells[2].ToolTipText=task.Title;}
        layout.Controls.Add(_list,0,1);
        var footer=new TableLayoutPanel{Dock=DockStyle.Fill,AutoSize=true,ColumnCount=2,Margin=new(0,16,0,0)};
        footer.ColumnStyles.Add(new(SizeType.Percent,100));footer.ColumnStyles.Add(new(SizeType.AutoSize));
        var count=new Label{AutoSize=true,Anchor=AnchorStyles.Left,ForeColor=SettingsWindow.Muted,Margin=Padding.Empty};footer.Controls.Add(count,0,0);
        void UpdateCount(){int total=retained.Length+_list.Rows.Cast<DataGridViewRow>().Count(r=>r.Cells[0].Value is true);count.Text=$"已选 {total} / 32";}
        _list.CurrentCellDirtyStateChanged+=(_,_)=>{if(_list.IsCurrentCellDirty)_list.CommitEdit(DataGridViewDataErrorContexts.Commit);};
        _list.CellValueChanged+=(_,_)=>UpdateCount();UpdateCount();
        var actions=new FlowLayoutPanel{AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,WrapContents=false,Margin=Padding.Empty};
        var save=new Button{Text="保存",Margin=new(0,0,8,0)};var cancel=new Button{Text="取消",DialogResult=DialogResult.Cancel,CausesValidation=false,Margin=Padding.Empty};
        SettingsWindow.StyleButton(save,true);SettingsWindow.StyleButton(cancel);actions.Controls.AddRange([save,cancel]);footer.Controls.Add(actions,1,0);layout.Controls.Add(footer,0,2);
        cancel.Click+=(_,_)=>Close();CancelButton=cancel;
        save.Click+=(_,_)=>{try{_list.EndEdit();var selected=retained.Concat(_list.Rows.Cast<DataGridViewRow>().Where(r=>r.Cells[0].Value is true).Select(r=>(string)r.Tag!)).ToArray();Tab5TaskPins.Save(selected);DialogResult=DialogResult.OK;Close();}catch(Exception ex){MessageBox.Show(this,ex.Message,"未能保存");}};
        SettingsWindow.AddTitle(layout,"常用任务");SettingsWindow.FitScreen(this);UnsavedChanges.Track(this);
    }
}
