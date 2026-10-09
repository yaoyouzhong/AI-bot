namespace AIBotBridge;

internal sealed class BirthdaySettingsForm:Form
{
    private readonly ListBox _list=new(){Name="birthday-list",Dock=DockStyle.Fill,IntegralHeight=false,BorderStyle=BorderStyle.FixedSingle,DrawMode=DrawMode.OwnerDrawFixed};
    private readonly TextBox _name=new(){Name="birthday-name",Dock=DockStyle.Fill,MaxLength=16};
    private readonly RadioButton _solar=new(){Text="公历",AutoSize=true,Checked=true};
    private readonly RadioButton _lunar=new(){Text="农历",AutoSize=true};
    private readonly Button _apply=new(){Text="添加到列表"};
    private readonly NumericUpDown _month=new(){Minimum=1,Maximum=12,Value=1,Width=65};
    private readonly NumericUpDown _day=new(){Minimum=1,Maximum=31,Value=1,Width=65};
    private readonly NumericUpDown _remind=new(){Minimum=0,Maximum=30,Value=3,Width=65};
    private readonly CheckBox _leap=new(){Text="闰月",AutoSize=true};
    private readonly Button _remove=new(){Name="birthday-remove",Text="移除所选",Enabled=false};
    private readonly Label _listTitle=new(){AutoSize=true,Margin=new(0,6,0,0)};
    private readonly List<Birthday> _entries=[];
    private readonly Label _status=new(){AutoSize=true,ForeColor=Color.DimGray,Dock=DockStyle.Fill};
    internal BirthdaySettingsForm(bool preview=false)
    {
        Text="日历与生日";Font=new Font("Microsoft YaHei UI",9F);AutoScaleDimensions=new SizeF(96,96);AutoScaleMode=AutoScaleMode.Dpi;
        ClientSize=new Size(740,600);MinimumSize=new Size(640,510);StartPosition=FormStartPosition.CenterScreen;BackColor=Color.White;ForeColor=Color.FromArgb(32,43,59);
        var root=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(24),ColumnCount=2,RowCount=4};
        root.ColumnStyles.Add(new(SizeType.Percent,40));root.ColumnStyles.Add(new(SizeType.Percent,60));
        root.RowStyles.Add(new(SizeType.AutoSize));root.RowStyles.Add(new(SizeType.Percent,100));root.RowStyles.Add(new(SizeType.AutoSize));root.RowStyles.Add(new(SizeType.AutoSize));
        var header=new FlowLayoutPanel{AutoSize=true,Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,Margin=new(0,0,0,20)};
        header.Controls.Add(new Label{Text="日历与生日",AutoSize=true,Font=new(Font.FontFamily,17,FontStyle.Bold),Margin=new(0,0,0,6)});
        header.Controls.Add(new Label{Text="管理生日提醒，保存后同步至 TAB5。",AutoSize=true,ForeColor=Color.DimGray,Margin=Padding.Empty});root.Controls.Add(header,0,0);root.SetColumnSpan(header,2);
        var listPanel=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=3,Margin=new(0,0,20,0)};
        listPanel.ColumnStyles.Add(new(SizeType.Percent,100));listPanel.RowStyles.Add(new(SizeType.AutoSize));listPanel.RowStyles.Add(new(SizeType.Percent,100));listPanel.RowStyles.Add(new(SizeType.AutoSize));
        var editScroll=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoScroll=true,FlowDirection=FlowDirection.TopDown,WrapContents=false,Padding=new(14),Margin=Padding.Empty,BackColor=Color.FromArgb(246,248,251)};
        void Field(string caption,Control control){var group=new TableLayoutPanel{AutoSize=true,ColumnCount=1,Margin=new(0,0,0,14)};group.ColumnStyles.Add(new(SizeType.Percent,100));group.Controls.Add(new Label{Text=caption,AutoSize=true,Margin=new(0,0,0,6)},0,0);control.Dock=DockStyle.Fill;control.Margin=Padding.Empty;group.Controls.Add(control,0,1);editScroll.Controls.Add(group);}
        Field("姓名",_name);
        var calendars=new FlowLayoutPanel{AutoSize=true,WrapContents=false};_solar.Margin=new(0,0,20,0);_lunar.Margin=Padding.Empty;calendars.Controls.AddRange([_solar,_lunar]);Field("生日历法",calendars);
        var date=new TableLayoutPanel{AutoSize=true,ColumnCount=1};date.ColumnStyles.Add(new(SizeType.Percent,100));
        var monthDay=new FlowLayoutPanel{AutoSize=true,Dock=DockStyle.Top,WrapContents=false,Margin=Padding.Empty};monthDay.Controls.AddRange([_month,new Label{Text="月",AutoSize=true,Padding=new(0,5,0,0)},_day,new Label{Text="日",AutoSize=true,Padding=new(0,5,0,0)}]);date.Controls.Add(monthDay,0,0);_leap.Margin=new(0,8,0,0);date.Controls.Add(_leap,0,1);Field("生日日期",date);
        var remind=new FlowLayoutPanel{AutoSize=true,WrapContents=false};remind.Controls.AddRange([_remind,new Label{Text="天前提醒",AutoSize=true,Padding=new(0,5,0,0)}]);Field("提前提醒",remind);
        Button Action(string caption,Action action,bool primary=false){var b=new Button{Text=caption};SettingsWindow.StyleButton(b,primary);b.Click+=(_,_)=>action();return b;}
        SettingsWindow.StyleButton(_apply,true);_apply.Click+=(_,_)=>Upsert();
        editScroll.Controls.Add(_apply);
        var note=new Label{Text="无对应闰月时按普通月；无 30 日或 2 月 29 日时按当月末日。",AutoSize=true,ForeColor=Color.DimGray,Margin=new(0,12,0,0)};editScroll.Controls.Add(note);SettingsWindow.FitFlow(editScroll);
        var listTools=new TableLayoutPanel{AutoSize=true,Dock=DockStyle.Fill,ColumnCount=2,Margin=new(0,0,0,12)};listTools.ColumnStyles.Add(new(SizeType.Percent,100));listTools.ColumnStyles.Add(new(SizeType.AutoSize));_listTitle.Font=new(Font,FontStyle.Bold);listTools.Controls.Add(_listTitle,0,0);listTools.Controls.Add(Action("新建",BeginNew),1,0);listPanel.Controls.Add(listTools,0,0);_list.Margin=Padding.Empty;listPanel.Controls.Add(_list,0,1);
        SettingsWindow.StyleButton(_remove);_remove.Margin=new(0,12,0,0);_remove.Click+=(_,_)=>{if(_list.SelectedIndex>=0){_entries.RemoveAt(_list.SelectedIndex);RefreshList();BeginNew();_status.Text="已从列表移除，点击保存后生效。";}};listPanel.Controls.Add(_remove,0,2);
        root.Controls.Add(listPanel,0,1);root.Controls.Add(editScroll,1,1);_status.Margin=new(0,14,0,0);root.Controls.Add(_status,0,2);root.SetColumnSpan(_status,2);root.SizeChanged+=(_,_)=>_status.MaximumSize=new(Math.Max(100,root.Width-root.Padding.Horizontal),0);
        var footer=new FlowLayoutPanel{AutoSize=true,Dock=DockStyle.Fill,FlowDirection=FlowDirection.RightToLeft,Padding=new Padding(0,12,0,0)};
        var cancel=Action("取消",Close);cancel.DialogResult=DialogResult.Cancel;cancel.CausesValidation=false;
        var save=Action("保存并同步",()=>{try {if(_name.Text.Trim().Length>0&&!Upsert())return;BirthdayStore.Save(_entries.ToArray());DialogResult=DialogResult.OK;Close();}catch(Exception ex)when(ex is IOException or UnauthorizedAccessException or InvalidOperationException){_status.Text=ex.Message;}},true);
        footer.Controls.AddRange([cancel,save]);root.Controls.Add(footer,0,3);root.SetColumnSpan(footer,2);Controls.Add(root);CancelButton=cancel;
        void FitListRows()=>_list.ItemHeight=TextRenderer.MeasureText("生日",_list.Font).Height*2+12;
        _list.FontChanged+=(_,_)=>FitListRows();FitListRows();
        _list.DrawItem+=(_,e)=>{e.DrawBackground();if(e.Index<0||e.Index>=_entries.Count)return;var entry=_entries[e.Index];bool selected=(e.State&DrawItemState.Selected)!=0;int pad=Math.Max(6,e.Bounds.Height/7);using var bold=new Font(e.Font??Font,FontStyle.Bold);var title=new Rectangle(e.Bounds.Left+pad,e.Bounds.Top+pad,e.Bounds.Width-pad*2,e.Bounds.Height/2);TextRenderer.DrawText(e.Graphics,entry.Name,bold,title,selected?SystemColors.HighlightText:ForeColor,TextFormatFlags.SingleLine|TextFormatFlags.EndEllipsis);var detail=title with {Y=e.Bounds.Top+e.Bounds.Height/2};TextRenderer.DrawText(e.Graphics,$"{(entry.Lunar?"农历":"公历")}{(entry.Leap?"闰":"")}{entry.Month}月{entry.Day}日 · 提前{entry.RemindDays}天",e.Font??Font,detail,selected?SystemColors.HighlightText:Color.DimGray,TextFormatFlags.SingleLine|TextFormatFlags.EndEllipsis);e.DrawFocusRectangle();};
        _list.SelectedIndexChanged+=(_,_)=>{_remove.Enabled=_list.SelectedIndex>=0;_apply.Text=_list.SelectedIndex<0?"添加到列表":"更新所选";if(_list.SelectedIndex<0)return;var b=_entries[_list.SelectedIndex];_lunar.Checked=b.Lunar;_solar.Checked=!b.Lunar;_name.Text=b.Name;_month.Value=b.Month;_day.Value=b.Day;_leap.Checked=b.Leap;_remind.Value=b.RemindDays;};
        _lunar.CheckedChanged+=(_,_)=>{_leap.Enabled=_lunar.Checked;if(!_leap.Enabled)_leap.Checked=false;};_leap.Enabled=false;
        _status.Text="填写后添加到列表，或选中左侧条目修改；点击保存同步至 TAB5。";
        try {_entries.AddRange(preview?[new("家人",8,15,true,false,7),new("朋友",10,1,false,false,3)]:BirthdayStore.Load());}
        catch(Exception ex)when(ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or InvalidOperationException){_status.Text="生日文件无法读取，请保留原文件并检查。";save.Enabled=false;}
        RefreshList();SettingsWindow.FitScreen(this);
    }
    private void BeginNew()
    {
        _list.ClearSelected();_name.Clear();_solar.Checked=true;_month.Value=1;_day.Value=1;_leap.Checked=false;_remind.Value=3;
        _status.Text="正在新建生日：填写后点击添加到列表，也可直接保存。";_name.Focus();
    }
    private bool Upsert()
    {
        var entry=new Birthday(_name.Text.Trim(),(int)_month.Value,(int)_day.Value,_lunar.Checked,_leap.Checked,(int)_remind.Value);
        try {var candidate=_entries.ToList();int selected=_list.SelectedIndex;if(selected>=0)candidate[selected]=entry;else {selected=candidate.Count;candidate.Add(entry);}BirthdayStore.Validate(candidate.ToArray());_entries.Clear();_entries.AddRange(candidate);RefreshList();_list.SelectedIndex=selected;_status.Text="列表已更新，点击保存同步至 TAB5；添加另一人请点新建生日。";return true;}
        catch(InvalidOperationException ex){_status.Text=ex.Message;return false;}
    }
    private void RefreshList(){_list.Items.Clear();foreach(var b in _entries)_list.Items.Add($"{b.Name}  ·  {(b.Lunar?"农历":"公历")}{(b.Leap?"闰":"")}{b.Month}/{b.Day}");_listTitle.Text=$"生日列表 · {_entries.Count} 人";}
    internal static void SelfTest()
    {
        if(!AppPaths.IsPublicSelfTest)throw new InvalidOperationException("Birthday UI test requires an isolated profile.");
        BirthdayStore.Save([new("保存样例",1,1,false,false,0)]);
        using(var form=new BirthdaySettingsForm()) {
            form._name.Text="新增样例";form._month.Value=2;form._day.Value=29;
            if(!form.Upsert()||form._entries.Count!=2)throw new InvalidOperationException("Birthday add failed");
            if(!form.Upsert()||form._entries.Count!=2)throw new InvalidOperationException("Repeated apply duplicated birthday");
            form._list.SelectedIndex=1;form._name.Text="修改样例";
            if(!form.Upsert()||form._entries[1].Name!="修改样例")throw new InvalidOperationException("Birthday edit failed");
            form._name.Text="无效日期";form._month.Value=2;form._day.Value=31;
            if(form.Upsert()||form._entries.Count!=2)throw new InvalidOperationException("Invalid birthday accepted");
            form._lunar.Checked=true;form._leap.Checked=true;
            if(form._solar.Checked||!form._leap.Enabled)throw new InvalidOperationException("Lunar selection failed");
            form._solar.Checked=true;
            if(form._lunar.Checked||form._leap.Enabled||form._leap.Checked)throw new InvalidOperationException("Solar selection kept leap month");
            form.BeginNew();form._name.Text="另一人";
            if(form._list.SelectedIndex!=-1||!form.Upsert()||form._entries.Count!=3||form._entries[1].Name!="修改样例")throw new InvalidOperationException("New birthday overwrote selection");
            form.Close();
        }
        if(BirthdayStore.Load().Length!=1)throw new InvalidOperationException("Cancel saved birthdays");
        BirthdayStore.Save([]);
        Console.WriteLine("BIRTHDAY_UI_OK direct calendar selection, new/add/edit, repeated apply, date validation and cancel preservation");
    }
}
