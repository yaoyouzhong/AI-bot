namespace AIBotBridge;
internal sealed class Tab5DisplaySettingsForm : Form
{
    private readonly CancellationTokenSource _stop=new();
    private readonly Func<Tab5DisplaySettings?,CancellationToken,Task<Tab5DisplaySettings>> _exchange;
    private Tab5DisplaySettings? _loaded;
    private readonly NumericUpDown _brightness=Number(10,100),_volume=Number(0,100);
    private readonly ComboBox _interval=Options([10,15,30,60]),_saver=Options([0,1,5,10,30,60]);
    private readonly CheckBox _muted=new(){Text="静音",AutoSize=true},_cycle=new(){Text="轮播",AutoSize=true},_alerts=new(){Text="任务提醒",AutoSize=true};
    private readonly ComboBox _mode=new(){DropDownStyle=ComboBoxStyle.DropDownList,Width=190};
    private readonly ListView _pages=new(){View=View.Details,CheckBoxes=true,FullRowSelect=true,MultiSelect=false,HideSelection=false,HeaderStyle=ColumnHeaderStyle.None,Height=200,Width=400};
    private readonly Label _status=new(){AutoSize=true,MaximumSize=new(460,0)};
    private readonly Button _read=new(){Text="重新读取",AutoSize=true},_save=new(){Text="保存到设备",AutoSize=true};
    private readonly TableLayoutPanel _body=new(){Dock=DockStyle.Fill,ColumnCount=2,RowCount=6,Padding=new(14),AutoScroll=true};
    private bool _busy,_disposed;
    private static ComboBox Options(int[] values){var box=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Width=105,FormattingEnabled=true};foreach(int value in values)box.Items.Add(value);return box;}
    private static NumericUpDown Number(int min,int max)=>new(){Minimum=min,Maximum=max,Width=82};
    internal Tab5DisplaySettingsForm(Func<Tab5DisplaySettings?,CancellationToken,Task<Tab5DisplaySettings>> exchange) {
        _exchange=exchange;Text="TAB5 · 显示设置";Font=new("Microsoft YaHei UI",9);AutoScaleMode=AutoScaleMode.Dpi;AutoScaleDimensions=new(96,96);ClientSize=new(540,510);MinimumSize=new(510,530);
        var root=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=2};root.ColumnStyles.Add(new(SizeType.Percent,100));root.RowStyles.Add(new(SizeType.Percent,100));root.RowStyles.Add(new(SizeType.AutoSize));Controls.Add(root);root.Controls.Add(_body,0,0);
        _body.ColumnStyles.Add(new(SizeType.AutoSize));_body.ColumnStyles.Add(new(SizeType.Percent,100));
        for(int i=0;i<6;i++)_body.RowStyles.Add(new(i==5?SizeType.Percent:SizeType.AutoSize,i==5?100:0));
        void Row(int index,string title,params Control[] controls){var row=new FlowLayoutPanel{AutoSize=true,Dock=DockStyle.Fill,WrapContents=true,Margin=new(0,2,0,2)};row.Controls.AddRange(controls);_body.Controls.Add(new Label{Text=title,AutoSize=true,Anchor=AnchorStyles.Left,Margin=new(0,4,12,4)},0,index);_body.Controls.Add(row,1,index);}
        Row(0,"亮度 %",_brightness);Row(1,"音量 %",_volume,_muted);
        _mode.Items.Add("自动");_mode.Items.AddRange(Tab5DisplaySettings.Pages);_mode.Width=150;Row(2,"页面",_mode,_cycle);
        _interval.Format+=(_,e)=>e.Value=e.ListItem+" 秒";_saver.Format+=(_,e)=>e.Value=e.ListItem is 0?"关闭":e.ListItem+" 分钟";
        Row(3,"间隔",_interval,new Label{Text="屏保",AutoSize=true,Margin=new(12,8,3,0)},_saver);Row(4,"提醒",_alerts);
        _pages.Columns.Add("轮播页面",300);_pages.Dock=DockStyle.Fill;_pages.SizeChanged+=(_,_)=>_pages.Columns[0].Width=Math.Max(80,_pages.ClientSize.Width-24);
        var up=new Button{Text="上移",AutoSize=true};var down=new Button{Text="下移",AutoSize=true};
        void Move(int step){if(_pages.SelectedIndices.Count!=1)return;int from=_pages.SelectedIndices[0],to=from+step;if(to<0||to>=8)return;var item=_pages.Items[from];_pages.Items.RemoveAt(from);_pages.Items.Insert(to,item);item.Selected=true;}
        up.Click+=(_,_)=>Move(-1);down.Click+=(_,_)=>Move(1);
        var order=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoSize=true,FlowDirection=FlowDirection.TopDown,WrapContents=false,Margin=Padding.Empty};order.Controls.Add(new Label{Text="轮播页",AutoSize=true});order.Controls.AddRange([up,down]);_body.Controls.Add(order,0,5);_body.Controls.Add(_pages,1,5);
        var footer=new FlowLayoutPanel{AutoSize=true,Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,Padding=new(18,0,18,10)};
        var buttons=new FlowLayoutPanel{AutoSize=true};buttons.Controls.AddRange([_read,_save]);footer.Controls.Add(buttons);footer.Controls.Add(_status);root.Controls.Add(footer,0,1);
        footer.SizeChanged+=(_,_)=>_status.MaximumSize=new(Math.Max(100,footer.ClientSize.Width-footer.Padding.Horizontal-8),0);
        _read.Click+=async(_,_)=>await ExchangeAsync(false);_save.Click+=async(_,_)=>await ExchangeAsync(true);
        _cycle.CheckedChanged+=(_,_)=>{if(_cycle.Checked)_mode.SelectedIndex=0;};_mode.SelectedIndexChanged+=(_,_)=>{if(_mode.SelectedIndex>0)_cycle.Checked=false;};
        Shown+=async(_,_)=>await ExchangeAsync(false);FormClosing+=(_,_)=>_stop.Cancel();SettingsWindow.FitScreen(this);_save.Enabled=false;
    }
    private async Task ExchangeAsync(bool save) {
        if(_busy)return;
        try {
            Tab5DisplaySettings? desired=null;
            if(save){if(_loaded is null)return;var enabled=new int[8];foreach(ListViewItem item in _pages.Items)enabled[(int)item.Tag!]=item.Checked?1:0;
                desired=_loaded with {Brightness=(int)_brightness.Value,Volume=(int)_volume.Value,Muted=_muted.Checked,Selected=_mode.SelectedIndex-1,Cycle=_cycle.Checked,Interval=(int)_interval.SelectedItem!,SaverMinutes=(int)_saver.SelectedItem!,Alerts=_alerts.Checked,Enabled=enabled,Order=_pages.Items.Cast<ListViewItem>().Select(i=>(int)i.Tag!).ToArray()};desired.Validate();}
            _busy=true;_body.Enabled=false;_read.Enabled=_save.Enabled=false;_status.Text=save?"等待设备确认…":"正在读取…";
            var result=await _exchange(desired,_stop.Token);if(IsDisposed)return;LoadSettings(result);
            _status.Text=save?"设备已保存":"已读取";
        }catch(OperationCanceledException){}catch(Exception ex){_loaded=null;if(!IsDisposed)_status.Text=ex.Message;}
        finally{_busy=false;if(!IsDisposed){_body.Enabled=true;_read.Enabled=true;_save.Enabled=_loaded is not null;}}
    }
    private void LoadSettings(Tab5DisplaySettings value) {
        value.Validate();_loaded=value;_brightness.Value=value.Brightness;_volume.Value=value.Volume;_muted.Checked=value.Muted;_mode.SelectedIndex=value.Selected+1;_cycle.Checked=value.Cycle;_interval.SelectedItem=value.Interval;_saver.SelectedItem=value.SaverMinutes;_alerts.Checked=value.Alerts;
        _pages.Items.Clear();foreach(int index in value.Order)_pages.Items.Add(new ListViewItem(Tab5DisplaySettings.Pages[index]){Tag=index,Checked=value.Enabled[index]==1});
    }
    protected override void Dispose(bool disposing){if(disposing&&!_disposed){_disposed=true;_stop.Cancel();_stop.Dispose();}base.Dispose(disposing);}
}
