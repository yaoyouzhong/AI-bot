namespace AIBotBridge;

internal sealed class CycleSettingsForm : Form
{
    private sealed record ModeChoice(string Label,string Mode,bool Cycle=false){public override string ToString()=>Label;}
    private readonly ComboBox _mode=new(){Name="display-mode",DropDownStyle=ComboBoxStyle.DropDownList,Dock=DockStyle.Fill};
    private readonly ListView _pages=new(){Name="cycle-pages",Dock=DockStyle.Fill,View=View.Details,CheckBoxes=true,FullRowSelect=true,MultiSelect=false,HideSelection=false,HeaderStyle=ColumnHeaderStyle.None,AccessibleName="轮播页面列表",Margin=Padding.Empty};
    private readonly ComboBox _interval=new(){Name="cycle-interval",DropDownStyle=ComboBoxStyle.DropDownList,Width=90};
    private readonly Button _up=new(){Text="上移",AutoSize=true},_down=new(){Text="下移",AutoSize=true};
    private readonly Label _hint=new(){AutoSize=true,Dock=DockStyle.Fill,ForeColor=Color.DimGray};
    private readonly Label _count=new(){AutoSize=true,Anchor=AnchorStyles.Left,ForeColor=Color.DimGray};
    private readonly BrightnessControl _brightness;
    private readonly Action<DisplayPolicy>? _apply;
    private DisplayPolicy _saved;
    private bool _loading,_ready;

    internal CycleSettingsForm(SerialPublisher? serial=null,Action<DisplayPolicy>? apply=null)
        :this(()=>serial is null?Task.FromException<UsbDeviceInfo>(new IOException("USB 未连接")):Task.Run(serial.ReadDeviceInfo),level=>serial?.SendBrightness(level)==true,apply) { }

    internal CycleSettingsForm(Func<Task<UsbDeviceInfo>> read,Func<int,bool> send,Action<DisplayPolicy>? apply=null)
    {
        SuspendLayout();Text="ESP8266 · 显示设置";AutoScaleDimensions=new(96,96);AutoScaleMode=AutoScaleMode.Dpi;
        Font=new Font("Microsoft YaHei UI",9F);ClientSize=new(500,520);MinimumSize=new(460,480);StartPosition=FormStartPosition.CenterScreen;MinimizeBox=false;
        _apply=apply;_saved=DisplayModes.Load(BridgeSettings.Load());
        _brightness=new BrightnessControl(read,send){Dock=DockStyle.Fill,Margin=new Padding(0,0,0,14)};
        _mode.Items.AddRange([new ModeChoice("自动轮播","auto",true),new ModeChoice("智能跟随","auto")]);
        foreach(var page in DisplayModes.Pages)_mode.Items.Add(new ModeChoice(page.Label,page.Mode));
        foreach(var (label,mode) in new[]{("综合动态","activity"),("双模型额度","quotas"),("国产模型","domestic"),("屏保","screensaver")})_mode.Items.Add(new ModeChoice(label,mode));
        _interval.Items.AddRange([10,15,30,60]);_pages.Columns.Add("页面",300);LoadPolicy(_saved);
        var root=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(18),ColumnCount=1,RowCount=6};root.ColumnStyles.Add(new(SizeType.Percent,100));
        for(int i=0;i<6;i++)root.RowStyles.Add(new(i==4?SizeType.Percent:SizeType.AutoSize,i==4?100:0));
        root.Controls.Add(_brightness,0,0);
        var modeRow=new TableLayoutPanel{Dock=DockStyle.Fill,AutoSize=true,ColumnCount=2,RowCount=1,Margin=new Padding(0,0,0,12)};
        modeRow.ColumnStyles.Add(new(SizeType.AutoSize));modeRow.ColumnStyles.Add(new(SizeType.Percent,100));modeRow.RowStyles.Add(new(SizeType.AutoSize));
        modeRow.Controls.Add(new Label{Text="显示方式",AutoSize=true,Anchor=AnchorStyles.Left,Margin=new Padding(0,0,12,0)},0,0);modeRow.Controls.Add(_mode,1,0);root.Controls.Add(modeRow,0,1);
        var timing=new FlowLayoutPanel{AutoSize=true,Dock=DockStyle.Fill,WrapContents=false,Margin=new Padding(0,0,0,8)};
        timing.Controls.Add(new Label{Text="轮播间隔",AutoSize=true,Margin=new Padding(0,5,12,0)});timing.Controls.Add(_interval);timing.Controls.Add(new Label{Text="秒",AutoSize=true,Margin=new Padding(8,5,0,0)});root.Controls.Add(timing,0,2);
        _hint.Margin=new Padding(0,0,0,10);root.Controls.Add(_hint,0,3);
        var body=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,Margin=Padding.Empty};body.ColumnStyles.Add(new(SizeType.Percent,100));body.ColumnStyles.Add(new(SizeType.AutoSize));body.RowStyles.Add(new(SizeType.Percent,100));
        body.Controls.Add(_pages,0,0);var moves=new FlowLayoutPanel{AutoSize=true,Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,Margin=new Padding(10,0,0,0)};
        _up.Margin=new Padding(0,0,0,8);_down.Margin=Padding.Empty;_up.Click+=(_,_)=>MovePage(-1);_down.Click+=(_,_)=>MovePage(1);moves.Controls.Add(_up);moves.Controls.Add(_down);body.Controls.Add(moves,1,0);root.Controls.Add(body,0,4);
        var footer=new TableLayoutPanel{AutoSize=true,Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,Margin=new Padding(0,12,0,0)};footer.ColumnStyles.Add(new(SizeType.Percent,100));footer.ColumnStyles.Add(new(SizeType.AutoSize));footer.RowStyles.Add(new(SizeType.AutoSize));footer.Controls.Add(_count,0,0);
        var close=new Button{Text="关闭",AutoSize=true,DialogResult=DialogResult.Cancel};close.Click+=(_,_)=>Close();footer.Controls.Add(close,1,0);root.Controls.Add(footer,0,5);Controls.Add(root);CancelButton=close;AcceptButton=close;
        foreach(var button in new[]{_up,_down,close})SettingsWindow.StyleButton(button);
        _mode.SelectionChangeCommitted+=(_,_)=>{UpdateState();Save(resumeCycle:(_mode.SelectedItem as ModeChoice)?.Cycle==true);};_interval.SelectedIndexChanged+=(_,_)=>Save();
        _pages.SelectedIndexChanged+=(_,_)=>UpdateState();
        _pages.ItemCheck+=(_,e)=>{if(_ready&&!_loading&&e.NewValue==CheckState.Unchecked&&_pages.CheckedItems.Count==1){e.NewValue=CheckState.Checked;_hint.Text="至少保留一个轮播页面。";}};
        _pages.ItemChecked+=(_,_)=>{if(!_loading){UpdateState();Save();}};
        _pages.Resize+=(_,_)=>_pages.Columns[0].Width=Math.Max(80,_pages.ClientSize.Width-SystemInformation.VerticalScrollBarWidth-4);
        _pages.KeyDown+=(_,e)=>{if(e.Alt&&(e.KeyCode==Keys.Up||e.KeyCode==Keys.Down)){MovePage(e.KeyCode==Keys.Up?-1:1);e.Handled=true;e.SuppressKeyPress=true;}};
        Shown+=async(_,_)=>{if(_pages.Items.Count>0)_pages.Items[0].Selected=true;UpdateState();_ready=true;await _brightness.ReadAsync();};
        FormClosing+=(_,_)=>{_ = _brightness.SendPendingBrightnessAsync();};SettingsWindow.FitScreen(this);ResumeLayout(true);UpdateState();
    }
    private void LoadPolicy(DisplayPolicy policy)
    {
        _loading=true;
        try {
            _mode.SelectedIndex=_mode.Items.Cast<ModeChoice>().ToList().FindIndex(m=>m.Mode==policy.SelectedMode&&(m.Mode!="auto"||m.Cycle==policy.CycleEnabled));
            if(_mode.SelectedIndex<0)_mode.SelectedIndex=0;
            _interval.SelectedItem=policy.IntervalSeconds;_pages.Items.Clear();
            foreach(string mode in policy.Pages.Concat(DisplayModes.Pages.Select(p=>p.Mode)).Distinct())_pages.Items.Add(new ListViewItem(DisplayModes.Pages.First(p=>p.Mode==mode).Label){Tag=mode,Checked=policy.Pages.Contains(mode)});
        }finally{_loading=false;}
    }
    private void UpdateState()
    {
        bool cycling=(_mode.SelectedItem as ModeChoice)?.Cycle==true;_pages.Enabled=_interval.Enabled=cycling;
        int index=_pages.SelectedIndices.Count==1?_pages.SelectedIndices[0]:-1;
        _up.Enabled=cycling&&index>0;_down.Enabled=cycling&&index>=0&&index<_pages.Items.Count-1;
        _hint.Text=cycling?"勾选轮播页面，用上移、下移调整顺序。更改自动生效。":"轮播设置已保留，选择“自动轮播”后启用。";
        _count.Text=$"轮播已选 {_pages.CheckedItems.Count} 页";
    }
    private void MovePage(int step)
    {
        if(!_pages.Enabled||_pages.SelectedIndices.Count!=1)return;
        int from=_pages.SelectedIndices[0],to=from+step;if(to<0||to>=_pages.Items.Count)return;
        var item=_pages.Items[from];_loading=true;_pages.BeginUpdate();
        try{_pages.Items.RemoveAt(from);_pages.Items.Insert(to,item);item.Selected=true;item.Focused=true;item.EnsureVisible();}
        finally{_pages.EndUpdate();_loading=false;}
        _pages.Select();UpdateState();Save();
    }
    private void Save(bool resumeCycle=false)
    {
        if(!_ready||_loading||_mode.SelectedItem is not ModeChoice mode)return;
        var policy=new DisplayPolicy(mode.Mode,mode.Cycle,(int)_interval.SelectedItem!,_pages.Items.Cast<ListViewItem>().Where(i=>i.Checked).Select(i=>(string)i.Tag!).ToArray());
        if(policy.Pages.Length==0)return;
        if(!resumeCycle&&policy.SelectedMode==_saved.SelectedMode&&policy.CycleEnabled==_saved.CycleEnabled&&policy.IntervalSeconds==_saved.IntervalSeconds&&policy.Pages.SequenceEqual(_saved.Pages))return;
        if(!BridgeSettings.Load().SaveEditable(new Dictionary<string,string>{["display_mode"]=policy.SelectedMode,["display_cycle_enabled"]=policy.CycleEnabled?"1":"0",["display_cycle_interval_seconds"]=policy.IntervalSeconds.ToString(),["display_cycle_pages"]=string.Join(',',policy.Pages)},out var error)) {
            LoadPolicy(_saved);UpdateState();_hint.Text=error;return;
        }
        if(resumeCycle)SessionActivityReader.Signals.Acknowledge();
        _saved=policy;_apply?.Invoke(policy);
    }
    internal static void VerifyLayout()
    {
        if(!AppPaths.IsPublicSelfTest)AppPaths.BeginPublicSelfTest();
        var output=Path.Combine(Environment.CurrentDirectory,"artifacts","cycle-layout");Directory.CreateDirectory(output);
        foreach(float scale in new[]{1f,1.5f,2f}) {
            using var form=new CycleSettingsForm();form.ShowInTaskbar=false;form.StartPosition=FormStartPosition.Manual;form.Location=new(-30000,-30000);form.Show();Application.DoEvents();if(scale!=1)form.Scale(new SizeF(scale,scale));form.PerformLayout();Application.DoEvents();
            var checks=form._pages.Items.Cast<ListViewItem>().ToDictionary(x=>(string)x.Tag!,x=>x.Checked);var moving=form._pages.Items[0];moving.Selected=true;form.MovePage(1);Application.DoEvents();
            if(form._pages.Items[1]!=moving||checks.Any(x=>form._pages.Items.Cast<ListViewItem>().Single(i=>(string)i.Tag! == x.Key).Checked!=x.Value))throw new InvalidOperationException("Reorder changed page selection.");form.MovePage(-1);
            foreach(var control in new Control[]{form._pages,form._up,form._down,(Button)form.CancelButton!}){var rectangle=form.RectangleToClient(control.RectangleToScreen(control.ClientRectangle));if(!form.ClientRectangle.Contains(rectangle)||rectangle.Width<20||rectangle.Height<20)throw new InvalidOperationException($"Display layout clipped {control.Text} at scale {scale}.");}
            using var bitmap=new Bitmap(form.Width,form.Height);form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size));bitmap.Save(Path.Combine(output,$"scale-{scale:0.0}.png"));form.Close();
        }
        Console.WriteLine("CYCLE_LAYOUT_OK 1x/1.5x/2x, order/check-state retained; isolated profile only");
    }
}
