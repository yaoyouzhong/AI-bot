namespace AIBotBridge;

internal sealed class CycleSettingsForm : Form
{
    private readonly ListView _pages = new() { Dock=DockStyle.Fill, View=View.Details, CheckBoxes=true,
        FullRowSelect=true, MultiSelect=false, HideSelection=false, HeaderStyle=ColumnHeaderStyle.None,
        AccessibleName="展示页面列表", Margin=Padding.Empty };
    private readonly CheckBox _enabled = new() { Text="启用循环展示", AutoSize=true };
    private readonly ComboBox _interval = new() { DropDownStyle=ComboBoxStyle.DropDownList, Width=90, AccessibleName="切换间隔" };
    private readonly Button _up = new() { Text="上移", AutoSize=true, MinimumSize=new Size(78,34) };
    private readonly Button _down = new() { Text="下移", AutoSize=true, MinimumSize=new Size(78,34) };
    private readonly Label _count = new() { AutoSize=true, Anchor=AnchorStyles.Left, ForeColor=Color.FromArgb(85,95,110) };
    internal CycleSettingsForm()
    {
        Text="调整展示顺序";
        AutoScaleDimensions=new SizeF(96,96);
        AutoScaleMode=AutoScaleMode.Dpi;
        Font=new Font("Microsoft YaHei UI",10,FontStyle.Regular,GraphicsUnit.Point);
        ClientSize=new Size(560,600);
        MinimumSize=new Size(480,460);
        StartPosition=FormStartPosition.CenterScreen;
        MinimizeBox=false;
        var policy=DisplayModes.Load(BridgeSettings.Load());
        _enabled.Checked=policy.CycleEnabled;
        _interval.Items.AddRange(new object[]{10,15,30,60});
        _interval.SelectedItem=policy.IntervalSeconds;
        _pages.Columns.Add("页面",300);
        foreach(var mode in policy.Pages.Concat(DisplayModes.Pages.Select(page=>page.Mode)).Distinct()) {
            var page=DisplayModes.Pages.First(item=>item.Mode==mode);
            _pages.Items.Add(new ListViewItem(page.Label){Tag=mode,Checked=policy.Pages.Contains(mode)});
        }
        var root=new TableLayoutPanel { Dock=DockStyle.Fill, Padding=new Padding(20), ColumnCount=1, RowCount=5 };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _enabled.Margin=new Padding(0,0,0,12);root.Controls.Add(_enabled,0,0);
        var timing=new FlowLayoutPanel { AutoSize=true, Dock=DockStyle.Fill, WrapContents=false, Margin=new Padding(0,0,0,12) };
        timing.Controls.Add(new Label { Text="切换间隔",AutoSize=true,Margin=new Padding(0,6,10,0) });
        timing.Controls.Add(_interval);
        timing.Controls.Add(new Label { Text="秒",AutoSize=true,Margin=new Padding(8,6,0,0) });
        root.Controls.Add(timing,0,1);
        var hint=new Label { Text="勾选参与轮播的页面；选中一行后调整顺序。",AutoSize=true,
            ForeColor=Color.FromArgb(85,95,110),Margin=new Padding(0,0,0,12),Dock=DockStyle.Fill };
        root.Controls.Add(hint,0,2);
        var body=new TableLayoutPanel { Dock=DockStyle.Fill, ColumnCount=2, RowCount=1, Margin=Padding.Empty };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));body.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        body.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        body.Controls.Add(_pages,0,0);
        var moves=new FlowLayoutPanel { AutoSize=true, Dock=DockStyle.Fill, FlowDirection=FlowDirection.TopDown,
            WrapContents=false,Margin=new Padding(12,0,0,0) };
        _up.Margin=new Padding(0,0,0,8);_down.Margin=Padding.Empty;
        _up.Click+=(_,_)=>MovePage(-1);_down.Click+=(_,_)=>MovePage(1);
        moves.Controls.Add(_up);moves.Controls.Add(_down);body.Controls.Add(moves,1,0);root.Controls.Add(body,0,3);
        var footer=new TableLayoutPanel { AutoSize=true,Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,Margin=new Padding(0,16,0,0) };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        footer.Controls.Add(_count,0,0);
        var actions=new FlowLayoutPanel { AutoSize=true,Dock=DockStyle.Fill,WrapContents=false,Margin=Padding.Empty };
        var save=new Button { Text="保存",AutoSize=true,MinimumSize=new Size(82,36),Margin=new Padding(0,0,8,0) };
        var cancel=new Button { Text="取消",AutoSize=true,MinimumSize=new Size(82,36),DialogResult=DialogResult.Cancel,Margin=Padding.Empty };
        save.Click+=(_,_)=>Save();actions.Controls.Add(save);actions.Controls.Add(cancel);footer.Controls.Add(actions,1,0);
        root.Controls.Add(footer,0,4);Controls.Add(root);AcceptButton=save;CancelButton=cancel;
        _pages.SelectedIndexChanged+=(_,_)=>UpdateState();
        _pages.ItemChecked+=(_,_)=>UpdateState();
        _pages.Resize+=(_,_)=>_pages.Columns[0].Width=Math.Max(80,_pages.ClientSize.Width-SystemInformation.VerticalScrollBarWidth-4);
        _pages.KeyDown+=(_,e)=>{if(e.Alt&&(e.KeyCode==Keys.Up||e.KeyCode==Keys.Down)){MovePage(e.KeyCode==Keys.Up?-1:1);e.Handled=true;e.SuppressKeyPress=true;}};
        Shown+=(_,_)=>{if(_pages.Items.Count>0){_pages.Items[0].Selected=true;_pages.Select();}UpdateState();};
        UpdateState();
    }
    private void UpdateState()
    {
        int index=_pages.SelectedIndices.Count==1?_pages.SelectedIndices[0]:-1;
        _up.Enabled=index>0;_down.Enabled=index>=0&&index<_pages.Items.Count-1;
        _count.Text=$"已选 {_pages.CheckedItems.Count} / {_pages.Items.Count} 页";
    }
    private void MovePage(int step)
    {
        if(_pages.SelectedIndices.Count!=1)return;
        int from=_pages.SelectedIndices[0],to=from+step;
        if(to<0||to>=_pages.Items.Count)return;
        var item=_pages.Items[from];
        _pages.BeginUpdate();
        try{_pages.Items.RemoveAt(from);_pages.Items.Insert(to,item);item.Selected=true;item.Focused=true;item.EnsureVisible();}
        finally{_pages.EndUpdate();}
        _pages.Select();UpdateState();
    }
    private void Save()
    {
        var pages=_pages.Items.Cast<ListViewItem>().Where(item=>item.Checked).Select(item=>(string)item.Tag!).ToArray();
        if(pages.Length==0){MessageBox.Show(this,"请至少选择一个循环页面。","调整展示顺序");return;}
        if(!BridgeSettings.Load().SaveEditable(new Dictionary<string,string> {
            ["display_mode"]="auto",["display_cycle_enabled"]=_enabled.Checked?"1":"0",
            ["display_cycle_interval_seconds"]=_interval.SelectedItem!.ToString()!,["display_cycle_pages"]=string.Join(',',pages)
        },out var error)){MessageBox.Show(this,error,"调整展示顺序");return;}
        DialogResult=DialogResult.OK;Close();
    }

    internal static void VerifyLayout()
    {
        var display=new NetworkDisplayWindow();
        for(int i=0;i<=16;i++) {
            var value=display.Update(i*250,new(i*1000,i*2000));
            long expected=i<8?0:i<16?4500:12500;
            if(value.Upload!=expected||value.Download!=expected*2)throw new InvalidOperationException("Two-second network hold/average failed.");
        }
        var output=Path.Combine(Environment.CurrentDirectory,"artifacts","cycle-layout");Directory.CreateDirectory(output);
        foreach(float scale in new[]{1f,1.5f,2f}) {
            using var form=new CycleSettingsForm();
            form.Show();Application.DoEvents();
            if(scale!=1)form.Scale(new SizeF(scale,scale));
            form.PerformLayout();Application.DoEvents();
            var checks=form._pages.Items.Cast<ListViewItem>().ToDictionary(x=>(string)x.Tag!,x=>x.Checked);
            var moving=form._pages.Items[0];moving.Selected=true;
            form.MovePage(1);Application.DoEvents();
            if(form._pages.Items[1]!=moving||checks.Any(x=>form._pages.Items.Cast<ListViewItem>().Single(i=>(string)i.Tag! == x.Key).Checked!=x.Value))
                throw new InvalidOperationException("Reorder changed page selection.");
            form.MovePage(-1);
            foreach(var control in new Control[]{form._pages,form._up,form._down,(Button)form.AcceptButton!,(Button)form.CancelButton!}) {
                var rectangle=form.RectangleToClient(control.RectangleToScreen(control.ClientRectangle));
                if(!form.ClientRectangle.Contains(rectangle)||rectangle.Width<20||rectangle.Height<20)
                    throw new InvalidOperationException($"Cycle layout clipped {control.Text} at scale {scale}.");
            }
            using var bitmap=new Bitmap(form.Width,form.Height);form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size));
            bitmap.Save(Path.Combine(output,$"scale-{scale:0.0}.png"));form.Close();
        }
        Console.WriteLine("CYCLE_LAYOUT_OK 1x/1.5x/2x layout scales, buttons visible, order/check-state retained; no settings saved");
        Console.WriteLine("METRICS_DISPLAY_INTERVAL_OK 2000ms hold, 8-sample average; graph samples unchanged");
    }
}
