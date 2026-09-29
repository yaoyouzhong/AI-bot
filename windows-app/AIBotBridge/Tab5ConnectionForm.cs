namespace AIBotBridge;

internal sealed class Tab5ConnectionForm : Form
{
    private readonly CancellationTokenSource _stop=new();
    private readonly System.Windows.Forms.Timer _timer=new(){Interval=1000};
    private bool _resourcesDisposed;
    internal Tab5ConnectionForm(Tab5Service service,bool loadNetworks=true)
    {
        SuspendLayout();AutoScaleDimensions=new SizeF(96,96);AutoScaleMode=AutoScaleMode.Dpi;
        Font=new Font("Microsoft YaHei UI",9F);Text="TAB5 连接";
        ClientSize=new Size(640,530);MinimumSize=new Size(560,440);StartPosition=FormStartPosition.CenterScreen;
        BackColor=Color.FromArgb(246,248,250);
        var layout=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(16),ColumnCount=1,RowCount=3};
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));Controls.Add(layout);
        var state=new Label{AutoSize=true,Dock=DockStyle.Fill,Text=service.ConnectionSummary,Padding=new Padding(12),BackColor=Color.White,Margin=new Padding(0,0,0,12)};
        layout.Controls.Add(state,0,0);
        var tabs=new TabControl{Dock=DockStyle.Fill,Padding=new Point(12,6),Margin=Padding.Empty};layout.Controls.Add(tabs,0,1);
        FlowLayoutPanel Page(string title) {
            var tab=new TabPage(title){BackColor=Color.White,Padding=new Padding(4)};tabs.TabPages.Add(tab);
            var panel=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,AutoScroll=true,Padding=new Padding(10)};
            tab.Controls.Add(panel);SettingsWindow.FitFlow(panel);return panel;
        }
        var network=Page("Wi-Fi");var usb=Page("USB 配对");var upgrade=Page("固件升级");var audio=Page("语音");
        var result=new Label{AutoSize=true,Dock=DockStyle.Fill,Padding=new Padding(0,8,0,0),MinimumSize=new Size(0,30),Text="",ForeColor=Color.FromArgb(64,83,101)};
        layout.Controls.Add(result,0,2);
        network.Controls.Add(Heading("已保存的 Wi-Fi"));
        var saved=new ListView{View=View.Details,FullRowSelect=true,MultiSelect=false,HideSelection=false,Height=118,Margin=new Padding(0,6,0,4)};
        saved.Columns.Add("Wi-Fi 名称");saved.Columns.Add("备注");saved.Columns.Add("状态");
        saved.SizeChanged+=(_,_)=>{int width=Math.Max(100,saved.ClientSize.Width-24);saved.Columns[0].Width=width*45/100;saved.Columns[1].Width=width*35/100;saved.Columns[2].Width=width-width*45/100-width*35/100;};network.Controls.Add(saved);
        var refreshWifi=new Button{Text="刷新"};var addWifi=new Button{Text="新增"};var forget=new Button{Text="删除所选",Enabled=false};network.Controls.Add(Buttons(refreshWifi,addWifi,forget));
        var fields=new TableLayoutPanel{AutoSize=true,ColumnCount=2,RowCount=3,Margin=new Padding(0,4,0,4)};
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));network.Controls.Add(fields);
        TextBox Field(string caption,string placeholder,int row,bool secret=false) {
            fields.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            fields.Controls.Add(new Label{Text=caption,AutoSize=true,Anchor=AnchorStyles.Left,Margin=new Padding(0,6,12,6)},0,row);
            var input=new TextBox{PlaceholderText=placeholder,UseSystemPasswordChar=secret,Dock=DockStyle.Fill,Margin=new Padding(0,4,0,4)};fields.Controls.Add(input,1,row);return input;
        }
        var ssid=Field("Wi-Fi 名称","2.4 GHz 网络",0);var wifiLabel=Field("备注","可选",1);var password=Field("新密码","修改密码时填写",2,true);
        var wifi=new Button{Text="保存并连接"};var saveLabel=new Button{Text="保存备注"};network.Controls.Add(Buttons(wifi,saveLabel));StyleCompactButton(wifi,true);
        usb.Controls.Add(Heading("USB 配对"));
        var ports=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,DisplayMember="Name",Margin=new Padding(0,8,0,8)};usb.Controls.Add(ports);
        var refresh=new Button{Text="刷新设备"};var pair=new Button{Text="配对"};usb.Controls.Add(Buttons(refresh,pair));
        usb.Controls.Add(Note("首次使用时配对。"));
        var firstInstall=new Button{Text="新设备首次安装…"};usb.Controls.Add(Buttons(firstInstall));
        firstInstall.Click+=(_,_)=> {
            using var installer=new Tab5InstallForm(service);installer.Text=Text+" · 首次安装与恢复";
            if(installer.ShowDialog(this)==DialogResult.OK){tabs.SelectedIndex=1;RefreshPorts();result.Text="启动已确认，请选择刚安装的 TAB5 并配对，再配置 Wi-Fi。";}
        };
        void RefreshPorts() {
            var selected=(ports.SelectedItem as FlashUsbDevice)?.Port;
            ports.DataSource=FlashDeviceDiscovery.Read().Where(d=>d.Identity.Contains("VID_303A",StringComparison.OrdinalIgnoreCase)).ToArray();
            for(int i=0;i<ports.Items.Count;i++)if(ports.Items[i] is FlashUsbDevice device&&device.Port==selected)ports.SelectedIndex=i;
        }
        refresh.Click+=(_,_)=>RefreshPorts();
        upgrade.Controls.Add(Heading("固件升级"));
        var otaStatus=new Label{AutoSize=true,MaximumSize=new Size(570,0),Text=service.OtaSummary,Margin=new Padding(0,12,0,16)};upgrade.Controls.Add(otaStatus);
        var offerOta=new Button{Text="选择固件…"};var cancelOta=new Button{Text="停止提供"};upgrade.Controls.Add(Buttons(offerOta,cancelOta));StyleCompactButton(offerOta,true);
        var releaseNotes=new Tab5ReleaseNotes();upgrade.Controls.Add(releaseNotes);
        void UpdateOffer() {
            if(otaStatus.Text!=service.OtaSummary)otaStatus.Text=service.OtaSummary;
            releaseNotes.Visible=cancelOta.Enabled=service.HasOta;releaseNotes.SetNotes(service.OtaNotes);
        }
        UpdateOffer();
        upgrade.Controls.Add(Note("固件升级使用 USB 或 Wi-Fi，并保持供电。蓝牙用于日常数据和会话，不用于大体积固件下载。0.2.39 / 0.2.40 请先在 TAB5 将连接方式切到 USB 或 Wi-Fi。"));
        offerOta.Click+=(_,_)=> {
            using var pick=new OpenFileDialog{Title="选择 TAB5 固件",Filter="TAB5 固件 (*.bin)|*.bin",CheckFileExists=true};
            if(pick.ShowDialog(this)!=DialogResult.OK)return;
            try{service.OfferOta(pick.FileName);UpdateOffer();result.Text="固件已提供。";}
            catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or ArgumentException){result.Text=ex.Message;}
        };
        cancelOta.Click+=(_,_)=>{service.CancelOta();UpdateOffer();result.Text="";};
        audio.Controls.Add(Heading("语音输入"));audio.Controls.Add(Note("识别后在 TAB5 检查，再手动发送。"));
        var voice=new Button{Text="语音设置…"};audio.Controls.Add(Buttons(voice));voice.Click+=(_,_)=>service.ShowVoiceSettings(this);
        bool busy=false,loading=false;
        Tab5Service.SavedWifiNetwork? Selected()=>saved.SelectedItems.Count==1?saved.SelectedItems[0].Tag as Tab5Service.SavedWifiNetwork:null;
        void FillSelection() {
            var selected=Selected();forget.Enabled=!busy&&selected is not null;
            if(loading||selected is null)return;
            ssid.Text=selected.Ssid;ssid.ReadOnly=true;wifiLabel.Text=selected.Label;password.Clear();result.Text="";
        }
        saved.SelectedIndexChanged+=(_,_)=>FillSelection();
        async Task RefreshNetworks() {
            var rows=await service.ReadWifiNetworksAsync(_stop.Token);if(IsDisposed||_resourcesDisposed)return;
            var selected=Selected()?.Ssid;loading=true;saved.BeginUpdate();
            try {
                saved.Items.Clear();
                foreach(var row in rows) {
                    var item=new ListViewItem(new[]{row.Ssid,row.Label,row.Connected?"已连接":row.Selected?"连接中":""}){Tag=row};saved.Items.Add(item);
                    if(row.Ssid==selected)item.Selected=true;
                }
            }finally{saved.EndUpdate();loading=false;}
            forget.Enabled=Selected() is not null;
            if(rows.Length==0)result.Text="暂无保存的网络。";
        }
        async Task Run(Func<Task> action,string done,bool quiet=false) {
            if(busy)return;busy=true;
            pair.Enabled=wifi.Enabled=refresh.Enabled=saveLabel.Enabled=refreshWifi.Enabled=forget.Enabled=addWifi.Enabled=saved.Enabled=fields.Enabled=false;if(!quiet)result.Text="正在处理…";
            try{await action();if(!IsDisposed&&!_resourcesDisposed&&!quiet)result.Text=done;}
            catch(OperationCanceledException){}
            catch(Exception ex) when(ex is IOException or InvalidOperationException or UnauthorizedAccessException or TimeoutException or ArgumentException or System.Security.Cryptography.CryptographicException or System.ComponentModel.Win32Exception or System.Text.Json.JsonException or KeyNotFoundException){if(!IsDisposed&&!_resourcesDisposed)result.Text=ex.Message;}
            finally{busy=false;if(!IsDisposed&&!_resourcesDisposed){pair.Enabled=wifi.Enabled=refresh.Enabled=saveLabel.Enabled=refreshWifi.Enabled=addWifi.Enabled=saved.Enabled=fields.Enabled=true;forget.Enabled=Selected() is not null;}}
        }
        refreshWifi.Click+=async(_,_)=>await Run(RefreshNetworks,"");
        addWifi.Click+=(_,_)=>{saved.SelectedItems.Clear();ssid.ReadOnly=false;ssid.Clear();wifiLabel.Clear();password.Clear();ssid.Focus();result.Text="";};
        forget.Click+=async(_,_)=> {
            var selected=Selected();if(selected is null)return;
            string warning=selected.Connected?"\n此网络正在连接，删除后会切换或断开 Wi-Fi。":"";
            if(MessageBox.Show(this,$"删除“{selected.Ssid}”？{warning}","删除 Wi-Fi",MessageBoxButtons.OKCancel,MessageBoxIcon.Warning)!=DialogResult.OK)return;
            await Run(async()=>{await service.ForgetWifiAsync(selected.Ssid,_stop.Token);await RefreshNetworks();ssid.ReadOnly=false;ssid.Clear();wifiLabel.Clear();password.Clear();},"网络已删除。");
        };
        pair.Click+=async(_,_)=>{if(ports.SelectedItem is FlashUsbDevice device)await Run(async()=>{await service.PairUsbAsync(device,_stop.Token);await RefreshNetworks();},"配对已完成。");else result.Text="请连接 USB 后刷新。";};
        wifi.Click+=async(_,_)=> {
            if(password.Text.Length==0&&MessageBox.Show(this,"将保存为空密码网络，是否继续？\n仅改备注请点“保存备注”。","Wi-Fi 密码为空",MessageBoxButtons.OKCancel,MessageBoxIcon.Question)!=DialogResult.OK)return;
            await Run(async()=>{await service.ConfigureWifiAsync(ssid.Text,password.Text,_stop.Token,wifiLabel.Text.Trim());password.Clear();await RefreshNetworks();},"已保存，正在连接。");
        };
        saveLabel.Click+=async(_,_)=>await Run(async()=>{await service.ConfigureWifiLabelAsync(ssid.Text,wifiLabel.Text.Trim(),_stop.Token);await RefreshNetworks();},"备注已保存。");
        long nextRefresh=Environment.TickCount64+10000;
        _timer.Tick+=async(_,_)=> {
            var value=service.ConnectionSummary;if(state.Text!=value)state.Text=value;
            UpdateOffer();
            if(loadNetworks&&!busy&&!fields.ContainsFocus&&tabs.SelectedIndex==0&&Environment.TickCount64>=nextRefresh){nextRefresh=Environment.TickCount64+10000;await Run(RefreshNetworks,"",true);}
        };
        if(loadNetworks)Shown+=async(_,_)=>await Run(RefreshNetworks,"");
        _timer.Start();RefreshPorts();SettingsWindow.FitScreen(this);ResumeLayout(true);
    }
    private static Label Note(string text)=>new(){Text=text,AutoSize=true,ForeColor=Color.FromArgb(86,102,117),Margin=new Padding(0,4,0,8)};
    private static Label Heading(string text)=>new(){Text=text,AutoSize=true,Font=new Font("Microsoft YaHei UI",10F,FontStyle.Bold),Margin=new Padding(0,8,0,4)};
    private static FlowLayoutPanel Buttons(params Button[] buttons) {
        var row=new FlowLayoutPanel{AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,WrapContents=true,Margin=new Padding(0,2,0,2)};
        foreach(var button in buttons){StyleCompactButton(button);button.Margin=new Padding(0,0,6,2);row.Controls.Add(button);}return row;
    }
    private static void StyleCompactButton(Button button,bool primary=false) {
        SettingsWindow.StyleButton(button,primary);
    }
    protected override void Dispose(bool disposing){if(disposing&&!_resourcesDisposed){_resourcesDisposed=true;_timer.Stop();_stop.Cancel();_timer.Dispose();_stop.Dispose();}base.Dispose(disposing);}
}
