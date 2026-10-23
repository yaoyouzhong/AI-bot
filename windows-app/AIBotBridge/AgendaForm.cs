namespace AIBotBridge;

internal sealed class AgendaForm:Form
{
    private readonly AgendaStore _store;
    private readonly ListBox _list=new(){Dock=DockStyle.Fill,IntegralHeight=false,DrawMode=DrawMode.OwnerDrawFixed,BorderStyle=BorderStyle.FixedSingle};
    private readonly ComboBox _filter=new(){DropDownStyle=ComboBoxStyle.DropDownList,Dock=DockStyle.Fill};
    private readonly TextBox _title=new(){Dock=DockStyle.Fill,MaxLength=32};
    private readonly CheckBox _timed=new(){Text="设置日程时间",AutoSize=true,Checked=true};
    private readonly DateTimePicker _date=new(){Format=DateTimePickerFormat.Custom,CustomFormat="yyyy-MM-dd HH:mm",Dock=DockStyle.Fill};
    private readonly DateTimePicker _end=new(){Format=DateTimePickerFormat.Custom,CustomFormat="yyyy-MM-dd HH:mm",Dock=DockStyle.Fill};
    private readonly ComboBox _repeat=new(){DropDownStyle=ComboBoxStyle.DropDownList,Dock=DockStyle.Fill};
    private readonly ComboBox _lead=new(){DropDownStyle=ComboBoxStyle.DropDownList,Dock=DockStyle.Fill};
    private readonly Label _status=new(){Dock=DockStyle.Fill,AutoSize=true,ForeColor=Color.DimGray};
    private readonly Button _snooze=new(){Text="10 分钟后再提醒"},_remove=new(){Text="删除事项"};
    private readonly System.Windows.Forms.Timer _clock=new(){Interval=1000};
    private AgendaItem? _editing;
    private AgendaView[] _views=[];
    private readonly int[] _leads=[0,1,3,5,10,15,30,60];
    internal AgendaForm(AgendaStore? store=null)
    {
        SuspendLayout();
        _store=store??AgendaStore.Shared;Text="日程待办";Font=SettingsWindow.BodyFont();
        ClientSize=new(640,420);MinimumSize=new(600,400);StartPosition=FormStartPosition.CenterScreen;BackColor=Color.White;ForeColor=Color.FromArgb(32,43,59);
        var root=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new(14),ColumnCount=2,RowCount=5};root.ColumnStyles.Add(new(SizeType.Percent,45));root.ColumnStyles.Add(new(SizeType.Percent,55));
        root.RowStyles.Add(new(SizeType.AutoSize));root.RowStyles.Add(new(SizeType.AutoSize));root.RowStyles.Add(new(SizeType.Percent,100));root.RowStyles.Add(new(SizeType.AutoSize));root.RowStyles.Add(new(SizeType.AutoSize));Controls.Add(root);
        var heading=new Label{Text="日程待办",AutoSize=true,Font=new(Font.FontFamily,17,FontStyle.Bold),Margin=new(0,0,0,8)};root.Controls.Add(heading,0,0);root.SetColumnSpan(heading,2);
        _filter.Items.AddRange(["今日","全部"]);_filter.SelectedIndex=1;_filter.Margin=Padding.Empty;
        var listTools=new TableLayoutPanel{Dock=DockStyle.Fill,AutoSize=true,ColumnCount=2,Margin=new(0,0,20,8)};listTools.ColumnStyles.Add(new(SizeType.Percent,100));listTools.ColumnStyles.Add(new(SizeType.AutoSize));
        listTools.Controls.Add(_filter,0,0);SettingsWindow.StyleButton(_remove);_remove.Margin=new(8,0,0,0);listTools.Controls.Add(_remove,1,0);root.Controls.Add(listTools,0,1);
        var add=Action("新建事项",BeginNew);add.Margin=new(0,0,0,8);root.Controls.Add(add,1,1);
        _list.Margin=new(0,0,20,0);root.Controls.Add(_list,0,2);
        var editor=new TableLayoutPanel{Dock=DockStyle.Fill,AutoScroll=true,ColumnCount=1,Padding=new(8),BackColor=Color.FromArgb(246,248,251)};editor.ColumnStyles.Add(new(SizeType.Percent,100));root.Controls.Add(editor,1,2);
        int row=0;
        void Field(string caption,Control input){var group=new TableLayoutPanel{Dock=DockStyle.Top,AutoSize=true,ColumnCount=1,Margin=new(0,0,0,4)};group.ColumnStyles.Add(new(SizeType.Percent,100));if(caption.Length>0)group.Controls.Add(new Label{Text=caption,AutoSize=true,Margin=new(0,0,0,4)},0,0);input.Margin=Padding.Empty;input.Font=Font;group.Controls.Add(input,0,caption.Length>0?1:0);editor.Controls.Add(group,0,row++);editor.RowStyles.Add(new(SizeType.AutoSize));}
        Field("事项名称",_title);Field("",_timed);
        // Two compact labelled rows use the same full date format, including overnight endings.
        var times=new TableLayoutPanel{Dock=DockStyle.Top,AutoSize=true,ColumnCount=2,Margin=Padding.Empty};times.ColumnStyles.Add(new(SizeType.AutoSize));times.ColumnStyles.Add(new(SizeType.Percent,100));
        times.RowStyles.Add(new(SizeType.Absolute));times.RowStyles.Add(new(SizeType.Absolute));
        times.Controls.Add(new Label{Text="开始",AutoSize=true,Anchor=AnchorStyles.Left,Margin=new(0,0,8,4)},0,0);times.Controls.Add(_date,1,0);
        times.Controls.Add(new Label{Text="结束",AutoSize=true,Anchor=AnchorStyles.Left,Margin=new(0,0,8,0)},0,1);times.Controls.Add(_end,1,1);_date.Margin=new(0,0,0,6);_end.Margin=Padding.Empty;Field("",times);
        _date.MinDate=_end.MinDate=new(2000,1,1);_date.MaxDate=new(2099,12,31,23,59,59);_end.MaxDate=new(2100,1,1,23,59,59);
        _repeat.Items.AddRange(["不重复","每天","周一至周五","每周","工作日"]);_repeat.SelectedIndex=0;
        _lead.Items.AddRange(["准时提醒","提前 1 分钟","提前 3 分钟","提前 5 分钟","提前 10 分钟","提前 15 分钟","提前 30 分钟","提前 60 分钟"]);_lead.SelectedIndex=0;
        var timing=new TableLayoutPanel{Dock=DockStyle.Top,AutoSize=true,ColumnCount=2,Margin=Padding.Empty};timing.ColumnStyles.Add(new(SizeType.Percent,50));timing.ColumnStyles.Add(new(SizeType.Percent,50));timing.RowStyles.Add(new(SizeType.AutoSize));timing.RowStyles.Add(new(SizeType.Absolute));
        timing.Controls.Add(new Label{Text="重复",AutoSize=true,Margin=new(0,0,8,4)},0,0);timing.Controls.Add(new Label{Text="提醒",AutoSize=true,Margin=new(8,0,0,4)},1,0);
        _repeat.Margin=new(0,0,8,0);_lead.Margin=new(8,0,0,0);_repeat.Font=_lead.Font=Font;timing.Controls.Add(_repeat,0,1);timing.Controls.Add(_lead,1,1);Field("",timing);
        var save=Action("保存事项",Save,true);save.Anchor=AnchorStyles.Right;save.Margin=new(0,8,0,0);root.Controls.Add(save,1,3);
        var actions=new FlowLayoutPanel{Dock=DockStyle.Top,AutoSize=true,WrapContents=true,Margin=new(0,10,0,0)};
        SettingsWindow.StyleButton(_snooze);actions.Controls.Add(_snooze);root.Controls.Add(actions,0,3);
        _status.Margin=new(0,8,0,0);root.Controls.Add(_status,0,4);root.SetColumnSpan(_status,2);root.SizeChanged+=(_,_)=>_status.MaximumSize=new(Math.Max(100,root.ClientSize.Width-root.Padding.Horizontal),0);_filter.Font=Font;
        _list.FontChanged+=(_,_)=>_list.ItemHeight=TextRenderer.MeasureText("事项",_list.Font).Height*2+18;_list.ItemHeight=TextRenderer.MeasureText("事项",_list.Font).Height*2+18;
        _list.DrawItem+=(_,e)=>{e.DrawBackground();if(e.Index<0||e.Index>=_views.Length)return;var v=_views[e.Index];bool selected=(e.State&DrawItemState.Selected)!=0;var color=selected?SystemColors.HighlightText:ForeColor;
            var bounds=new Rectangle(e.Bounds.X+10,e.Bounds.Y+6,e.Bounds.Width-20,e.Bounds.Height/2);using var bold=new Font(e.Font??Font,FontStyle.Bold);
            TextRenderer.DrawText(e.Graphics,v.Item.Title,bold,bounds,color,TextFormatFlags.SingleLine|TextFormatFlags.EndEllipsis);
            bounds.Y+=e.Bounds.Height/2;TextRenderer.DrawText(e.Graphics,(v.Complete?"已结束 · ":"")+AgendaRules.Description(v),e.Font??Font,bounds,selected?color:Color.DimGray,TextFormatFlags.SingleLine|TextFormatFlags.EndEllipsis);e.DrawFocusRectangle();};
        _list.SelectedIndexChanged+=(_,_)=>{if(_list.SelectedIndex<0)return;_editing=_views[_list.SelectedIndex].Item;_title.Text=_editing.Title;_timed.Checked=_editing.Due!=0;_date.Value=_editing.Due==0?DateTime.Now.AddHours(1):AgendaRules.Date(_editing.Due);_end.Value=_editing.Due==0?_date.Value.AddHours(1):AgendaRules.Date(_editing.End==0?_editing.Due+3600:_editing.End);_repeat.SelectedIndex=_editing.Repeat;_lead.SelectedIndex=Math.Max(0,Array.IndexOf(_leads,_editing.Lead));RefreshActions();UnsavedChanges.MarkSaved(this);};
        _date.ValueChanged+=(_,_)=>{if(_end.Value<=_date.Value)_end.Value=_date.Value.AddHours(1);};
        _timed.CheckedChanged+=(_,_)=>{_date.Enabled=_end.Enabled=_repeat.Enabled=_lead.Enabled=_timed.Checked;};_filter.SelectedIndexChanged+=(_,_)=>{RefreshList();BeginNew();};
        _snooze.Click+=(_,_)=>Perform();
        _remove.Click+=(_,_)=>{if(_editing is not {} item)return;if(MessageBox.Show(this,$"删除“{item.Title}”？","删除事项",MessageBoxButtons.OKCancel,MessageBoxIcon.Question)!=DialogResult.OK)return;Try(()=>{_store.Remove(item.Id,item.Edit);RefreshList();BeginNew();_status.Text="已删除，连接后同步至 TAB5。";});};
        RefreshList();BeginNew();AutoScaleDimensions=new(96,96);AutoScaleMode=AutoScaleMode.Dpi;ResumeLayout(true);SettingsWindow.FitScreen(this);
        bool sizingRows=false;
        void SizeTimeRows(){if(sizingRows)return;sizingRows=true;try{
            times.RowStyles[0].Height=Math.Max(_date.Height,_date.MinimumSize.Height)+_date.Margin.Vertical;
            times.RowStyles[1].Height=Math.Max(_end.Height,_end.MinimumSize.Height)+_end.Margin.Vertical;
            timing.RowStyles[1].Height=new[]{_repeat.Height,_repeat.MinimumSize.Height,_lead.Height,_lead.MinimumSize.Height}.Max();
        }finally{sizingRows=false;}}
        foreach(var input in new Control[]{_date,_end,_repeat,_lead}){input.SizeChanged+=(_,_)=>SizeTimeRows();input.FontChanged+=(_,_)=>SizeTimeRows();}
        AgendaNativeFonts.Attach(this,[_filter,_title,_date,_end,_repeat,_lead]);Shown+=(_,_)=>SizeTimeRows();SizeTimeRows();
        _clock.Tick+=(_,_)=>{RefreshList(true);RefreshActions();};Shown+=(_,_)=>_clock.Start();FormClosed+=(_,_)=>_clock.Dispose();
    }
    private Button Action(string title,Action callback,bool primary=false){var b=new Button{Text=title,AutoSize=true};SettingsWindow.StyleButton(b,primary);b.Click+=(_,_)=>callback();return b;}
    private void Try(Action action){try{action();}catch(Exception ex)when(ex is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException){_status.Text=ex.Message;}}
    private void RefreshList(bool onlyIfChanged=false){Try(()=>{long now=AgendaRules.Now;var next=_store.Snapshot().Items.Select(i=>AgendaRules.View(i,now)).Where(v=>_filter.SelectedIndex!=0||!v.Complete&&(v.Item.Due==0||v.Occurrence/86400<=now/86400)).OrderBy(v=>v.Complete).ThenBy(v=>v.Item.Due==0?long.MaxValue:v.Complete?-v.Occurrence:v.Occurrence).ToArray();if(onlyIfChanged&&next.SequenceEqual(_views))return;_views=next;_list.Items.Clear();foreach(var v in _views)_list.Items.Add(v.Item.Title);_status.Text=_views.Length==0?"此列表没有事项。点击新建事项开始。":"满 16 条新增时，清理最早结束的过期事项。";});}
    private void BeginNew(){_editing=null;_list.ClearSelected();_title.Clear();_timed.Checked=true;_date.Value=DateTime.Now.AddHours(1);_end.Value=_date.Value.AddHours(1);_repeat.SelectedIndex=_lead.SelectedIndex=0;RefreshActions();UnsavedChanges.MarkSaved(this);}
    private void RefreshActions(){bool selected=_editing is not null;_remove.Enabled=selected;long now=AgendaRules.Now;var v=selected?AgendaRules.View(_editing!,now):null;_snooze.Enabled=v is not null&&_editing!.Due!=0&&!v.Complete&&v.AlertAt<=now&&now+600<AgendaRules.EndsAt(_editing,v.Occurrence);}
    private void Save(){Try(()=>{var old=_editing;var item=new AgendaItem(old?.Id??Guid.NewGuid().ToString("N"),old?.Edit??Guid.NewGuid().ToString("N"),_title.Text.Trim(),_timed.Checked?AgendaRules.Civil(_date.Value.AddSeconds(-_date.Value.Second)):0,_timed.Checked?_repeat.SelectedIndex:0,_timed.Checked?_leads[_lead.SelectedIndex]:0);
        if(_timed.Checked){item=item with{End=AgendaRules.Civil(_end.Value.AddSeconds(-_end.Value.Second))};if(item.End<=item.Due)throw new InvalidOperationException("结束时间须晚于开始时间。");}
        _store.Save(item,old?.Edit);RefreshList();BeginNew();_status.Text="已保存。TAB5 连接后自动同步。";});}
    private void Perform(){if(_editing is not {} item)return;Try(()=>{var v=AgendaRules.View(item,AgendaRules.Now);_store.Apply(new(item.Id,item.Edit,v.Occurrence,"snooze",AgendaRules.Now+600));RefreshList();BeginNew();_status.Text="已设为 10 分钟后再提醒。";});}
}
