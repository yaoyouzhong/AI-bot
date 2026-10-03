namespace AIBotBridge;

internal sealed class BirthdaySettingsForm:Form
{
    private readonly ListBox _list=new(){Dock=DockStyle.Fill,IntegralHeight=false};
    private readonly TextBox _name=new(){Dock=DockStyle.Fill,MaxLength=16};
    private readonly RadioButton _solar=new(){Text="公历",AutoSize=true,Checked=true};
    private readonly RadioButton _lunar=new(){Text="农历",AutoSize=true};
    private readonly Button _apply=new(){Text="添加到列表"};
    private readonly NumericUpDown _month=new(){Minimum=1,Maximum=12,Value=1,Width=65};
    private readonly NumericUpDown _day=new(){Minimum=1,Maximum=31,Value=1,Width=65};
    private readonly NumericUpDown _remind=new(){Minimum=0,Maximum=30,Value=3,Width=65};
    private readonly CheckBox _leap=new(){Text="闰月",AutoSize=true};
    private readonly List<Birthday> _entries=[];
    private readonly Label _status=new(){AutoSize=true,ForeColor=Color.DimGray,Dock=DockStyle.Fill};
    internal BirthdaySettingsForm(bool preview=false)
    {
        Text="日历与生日";Font=new Font("Microsoft YaHei UI",9F);AutoScaleDimensions=new SizeF(96,96);AutoScaleMode=AutoScaleMode.Dpi;
        ClientSize=new Size(700,450);MinimumSize=new Size(580,430);StartPosition=FormStartPosition.CenterScreen;
        var root=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(18),ColumnCount=2,RowCount=3};
        root.ColumnStyles.Add(new(SizeType.Percent,43));root.ColumnStyles.Add(new(SizeType.Percent,57));
        root.RowStyles.Add(new(SizeType.Percent,100));root.RowStyles.Add(new(SizeType.AutoSize));root.RowStyles.Add(new(SizeType.AutoSize));
        var editScroll=new Panel{Dock=DockStyle.Fill,AutoScroll=true};
        var edit=new TableLayoutPanel{Dock=DockStyle.Top,AutoSize=true,ColumnCount=2,RowCount=6,Padding=new Padding(14,0,0,0)};
        edit.ColumnStyles.Add(new(SizeType.AutoSize));edit.ColumnStyles.Add(new(SizeType.Percent,100));
        for(int i=0;i<6;i++)edit.RowStyles.Add(new(SizeType.AutoSize));
        void Row(int row,string caption,Control control){edit.Controls.Add(new Label{Text=caption,AutoSize=true,Margin=new Padding(0,6,8,10)},0,row);edit.Controls.Add(control,1,row);}
        var calendars=new FlowLayoutPanel{AutoSize=true,Dock=DockStyle.Fill,WrapContents=false};calendars.Controls.AddRange([_solar,_lunar]);
        Row(0,"姓名",_name);Row(1,"历法",calendars);
        var date=new FlowLayoutPanel{AutoSize=true,Dock=DockStyle.Fill};date.Controls.AddRange([_month,new Label{Text="月",AutoSize=true,Padding=new Padding(0,5,0,0)},_day,new Label{Text="日",AutoSize=true,Padding=new Padding(0,5,0,0)},_leap]);Row(2,"生日",date);
        var remind=new FlowLayoutPanel{AutoSize=true,Dock=DockStyle.Fill};remind.Controls.AddRange([_remind,new Label{Text="天前提醒",AutoSize=true,Padding=new Padding(0,5,0,0)}]);Row(3,"提前",remind);
        Button Action(string caption,Action action,bool primary=false){var b=new Button{Text=caption};SettingsWindow.StyleButton(b,primary);b.Click+=(_,_)=>action();return b;}
        var actions=new FlowLayoutPanel{AutoSize=true,Dock=DockStyle.Fill};
        SettingsWindow.StyleButton(_apply,true);_apply.Click+=(_,_)=>Upsert();
        actions.Controls.AddRange([Action("新建生日",()=>BeginNew()),_apply,Action("移除",()=>{if(_list.SelectedIndex>=0){_entries.RemoveAt(_list.SelectedIndex);RefreshList();BeginNew();_status.Text="已从列表移除，点击保存后生效。";}})]);
        edit.Controls.Add(actions,0,4);edit.SetColumnSpan(actions,2);
        var note=new Label{Text="无对应闰月时按普通月；无 30 日或 2 月 29 日时按当月末日。",AutoSize=true,MaximumSize=new Size(335,0),ForeColor=Color.DimGray,Dock=DockStyle.Fill};edit.Controls.Add(note,0,5);edit.SetColumnSpan(note,2);
        editScroll.Controls.Add(edit);root.Controls.Add(_list,0,0);root.Controls.Add(editScroll,1,0);root.Controls.Add(_status,0,1);root.SetColumnSpan(_status,2);
        var footer=new FlowLayoutPanel{AutoSize=true,Dock=DockStyle.Fill,FlowDirection=FlowDirection.RightToLeft,Padding=new Padding(0,12,0,0)};
        var cancel=Action("取消",Close);cancel.DialogResult=DialogResult.Cancel;cancel.CausesValidation=false;
        var save=Action("保存",()=>{try {if(_name.Text.Trim().Length>0&&!Upsert())return;BirthdayStore.Save(_entries.ToArray());DialogResult=DialogResult.OK;Close();}catch(Exception ex)when(ex is IOException or UnauthorizedAccessException or InvalidOperationException){_status.Text=ex.Message;}},true);
        footer.Controls.AddRange([cancel,save]);root.Controls.Add(footer,0,2);root.SetColumnSpan(footer,2);Controls.Add(root);CancelButton=cancel;
        _list.SelectedIndexChanged+=(_,_)=>{_apply.Text=_list.SelectedIndex<0?"添加到列表":"更新所选";if(_list.SelectedIndex<0)return;var b=_entries[_list.SelectedIndex];_lunar.Checked=b.Lunar;_solar.Checked=!b.Lunar;_name.Text=b.Name;_month.Value=b.Month;_day.Value=b.Day;_leap.Checked=b.Leap;_remind.Value=b.RemindDays;};
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
    private void RefreshList(){_list.Items.Clear();foreach(var b in _entries)_list.Items.Add($"{b.Name}  ·  {(b.Lunar?"农历":"公历")}{(b.Leap?"闰":"")}{b.Month}/{b.Day}");}
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
