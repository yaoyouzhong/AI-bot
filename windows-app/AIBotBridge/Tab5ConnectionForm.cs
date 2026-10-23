namespace AIBotBridge;

internal sealed class Tab5ConnectionForm : Form
{
    private readonly CancellationTokenSource _stop=new();
    private readonly System.Windows.Forms.Timer _timer=new(){Interval=1000};
    private bool _resourcesDisposed;
    internal void ShowUpgrade(){if(Controls.Find("connection-tabs",true).FirstOrDefault() is TabControl tabs)tabs.SelectedIndex=2;}
    internal Tab5ConnectionForm(Tab5Service service,bool loadNetworks=true,bool showUpgrade=false)
    {
        SuspendLayout();AutoScaleDimensions=new SizeF(96,96);AutoScaleMode=AutoScaleMode.Dpi;
        Font=SettingsWindow.BodyFont();Text="TAB5 连接与升级";
        ClientSize=new Size(640,530);MinimumSize=new Size(560,520);StartPosition=FormStartPosition.CenterScreen;
        BackColor=Color.White;
        var layout=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(24),ColumnCount=1,RowCount=3};
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));Controls.Add(layout);
        var state=new Label{AutoSize=true,Dock=DockStyle.Fill,Text=service.ConnectionSummary,Padding=new Padding(12),BackColor=Color.White,Margin=new Padding(0,0,0,12)};
        layout.Controls.Add(state,0,0);
        var tabs=new TabControl{Name="connection-tabs",Dock=DockStyle.Fill,Padding=new Point(12,6),Margin=Padding.Empty};layout.Controls.Add(tabs,0,1);
        Panel Page(string title) {
            var tab=new TabPage(title){BackColor=Color.White,Padding=new Padding(4)};tabs.TabPages.Add(tab);
            Panel panel=title is "固件升级" or "传输测速"?new ContentPage{Dock=DockStyle.Fill,Padding=new Padding(10)}:
                new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,AutoScroll=true,Padding=new Padding(10)};
            tab.Controls.Add(panel);if(panel is FlowLayoutPanel flow)SettingsWindow.FitFlow(flow);return panel;
        }
        var network=Page("Wi-Fi");var usb=Page("USB 配对");var upgrade=Page("固件升级");var audio=Page("语音");
        if(showUpgrade)tabs.SelectedIndex=2;
        var benchmark=Page("传输测速");
        var benchmarkMode=new ComboBox{Name="benchmark-mode",DropDownStyle=ComboBoxStyle.DropDownList,Width=150};
        benchmarkMode.Items.AddRange(["图库写卡","通信传输"]);benchmarkMode.SelectedIndex=0;
        var benchmarkStart=new Button{Text="开始测速"};var benchmarkStop=new Button{Text="停止",Enabled=false};
        var benchmarkActions=Buttons(benchmarkStart,benchmarkStop);benchmarkActions.Controls.Add(benchmarkMode);benchmarkActions.Controls.SetChildIndex(benchmarkMode,0);
        StyleCompactButton(benchmarkStart,true);benchmark.Controls.Add(benchmarkActions);
        var benchmarkHint=Note("");benchmark.Controls.Add(benchmarkHint);
        void UpdateBenchmarkHint()=>benchmarkHint.Text=benchmarkMode.SelectedIndex==0?
            "8 个独立样本，包含传输、校验和写卡；现有图库保留。":"比较各可用通道的上传和下载速度，不写入 TF 卡。";
        benchmarkMode.SelectedIndexChanged+=(_,_)=>UpdateBenchmarkHint();UpdateBenchmarkHint();
        string benchmarkValue=service.BenchmarkDiagnostic;bool benchmarkExpanded=false;
        var benchmarkDetailsLink=new LinkLabel{Text="查看详细数据",AutoSize=true,Margin=new Padding(0,8,0,6)};benchmark.Controls.Add(benchmarkDetailsLink);
        var benchmarkResult=new TextBox{Name="benchmark-result",Multiline=true,ReadOnly=true,BorderStyle=BorderStyle.None,BackColor=Color.White,ScrollBars=ScrollBars.Vertical,Dock=DockStyle.Fill,Text=Tab5Service.FormatBenchmarkOverview(benchmarkValue)};benchmark.Controls.Add(benchmarkResult);
        void RenderBenchmark()=>benchmarkResult.Text=benchmarkExpanded?Tab5Service.FormatBenchmark(benchmarkValue):Tab5Service.FormatBenchmarkOverview(benchmarkValue);
        benchmarkDetailsLink.LinkClicked+=(_,_)=>{benchmarkExpanded=!benchmarkExpanded;benchmarkDetailsLink.Text=benchmarkExpanded?"收起详细数据":"查看详细数据";RenderBenchmark();};
        CancellationTokenSource? benchmarkCancel=null;
        benchmarkStop.Click+=(_,_)=>benchmarkCancel?.Cancel();
        async Task RunBenchmark() {
            benchmarkStart.Enabled=false;benchmarkStop.Enabled=true;benchmarkCancel=CancellationTokenSource.CreateLinkedTokenSource(_stop.Token);
            benchmarkMode.Enabled=false;benchmarkValue="running;starting";RenderBenchmark();
            try {
                void Progress(string value){if(!IsDisposed){benchmarkValue=value;RenderBenchmark();}}
                string value=benchmarkMode.SelectedIndex==0?await service.GalleryBenchmarkAsync(Progress,benchmarkCancel.Token):await service.BenchmarkAsync(Progress,benchmarkCancel.Token);
                Progress(value);
            }
            catch(OperationCanceledException){if(!IsDisposed){benchmarkValue="测速已停止。";RenderBenchmark();}}
            catch(Exception ex){if(!IsDisposed){benchmarkValue=ex.Message;RenderBenchmark();}}
            finally{benchmarkCancel.Dispose();benchmarkCancel=null;if(!IsDisposed){benchmarkStart.Enabled=true;benchmarkStop.Enabled=false;benchmarkMode.Enabled=true;}}
        }
        benchmarkStart.Click+=async(_,_)=>await RunBenchmark();
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
        var otaStatus=new Tab5UpgradeStatusView();upgrade.Controls.Add(otaStatus);
        var offerOta=new Button{Text="选择固件…"};var cancelOta=new Button{Text="停止提供"};var startOta=new Button{Text="开始升级"};upgrade.Controls.Add(Buttons(startOta,offerOta,cancelOta));StyleCompactButton(startOta,true);
        var bootRow=new TableLayoutPanel{AutoSize=true,ColumnCount=2,RowCount=1,Margin=new Padding(0,8,0,8)};
        bootRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));bootRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        var bootState=new Label{AutoSize=true,Anchor=AnchorStyles.Left,ForeColor=SettingsWindow.Muted,Margin=new Padding(0,4,8,4)};
        var verifyOta=new Button{Text="核验启动",Anchor=AnchorStyles.Right,Margin=Padding.Empty};StyleCompactButton(verifyOta);bootRow.Controls.Add(bootState,0,0);bootRow.Controls.Add(verifyOta,1,0);upgrade.Controls.Add(bootRow);
        bool verifying=false,otaResult=false;
        var releaseNotes=new Tab5ReleaseNotes{Dock=DockStyle.Fill};upgrade.Controls.Add(releaseNotes);
        verifyOta.Click+=async(_,_)=>{verifying=true;verifyOta.Enabled=false;bootState.Text="正在核验启动…";try{await service.VerifyUpgradeAsync(_stop.Token);if(!IsDisposed){result.Text="";otaResult=false;}}catch(OperationCanceledException){}catch(Exception ex){if(!IsDisposed){result.Text=ex.Message;otaResult=true;}}finally{verifying=false;if(!IsDisposed)UpdateOffer();}};
        void UpdateOffer() {
            var presentation=service.UpgradeStatusText;otaStatus.SetStatus(presentation);
            if(otaResult&&presentation.State is "设备已运行" or "已核验"){result.Text="";otaResult=false;}
            startOta.Enabled=service.CanStartOta;offerOta.Enabled=!service.Busy;
            if(!verifying)bootState.Text=service.UpgradeBootSummary;
            verifyOta.Enabled=!verifying&&service.CanVerifyUpgrade;
            releaseNotes.Visible=service.HasOta;cancelOta.Enabled=service.HasOta&&!service.Busy;releaseNotes.SetNotes(service.OtaNotes);
        }
        void UpdateConnection() {
            var view=service.DeviceView;
            var text=tabs.SelectedIndex is 2 or 4?(view.Online?$"{view.Transport} · 当前版本 {view.Firmware}":"TAB5 未连接"):service.ConnectionSummary;
            if(state.Text!=text)state.Text=text;
        }
        tabs.SelectedIndexChanged+=(_,_)=>UpdateConnection();UpdateConnection();
        UpdateOffer();
        startOta.Click+=async(_,_)=> {
            startOta.Enabled=false;otaResult=true;
            try {await service.StartOtaAsync(_stop.Token);if(!IsDisposed)result.Text=service.RemoteOtaStatus;}
            catch(OperationCanceledException){}
            catch(Exception ex) when(ex is IOException or InvalidOperationException){if(!IsDisposed)result.Text=ex.Message;}
            finally{if(!IsDisposed)UpdateOffer();}
        };
        offerOta.Click+=(_,_)=> {
            using var pick=new OpenFileDialog{Title="选择 TAB5 固件",Filter="TAB5 固件 (*.bin)|*.bin",CheckFileExists=true};
            if(pick.ShowDialog(this)!=DialogResult.OK)return;
            try{
                var candidate=Tab5OtaPackage.Load(pick.FileName);
                if(MessageBox.Show(this,$"TAB5 固件 {candidate.Version}\n大小：{candidate.Image.Length:N0} 字节\nSHA-256：{candidate.Sha256}\nESP32-P4 镜像结构和内置校验通过。\n\n{candidate.Notes}\n\n提供后可点击电脑“开始升级”；旧设备需先在 TAB5 完成一次升级。", "确认固件",MessageBoxButtons.OKCancel,MessageBoxIcon.Information)!=DialogResult.OK)return;
                service.OfferOta(candidate);UpdateOffer();result.Text="已提供固件，等待设备确认。";
            }
            catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException){result.Text=ex.Message;}
        };
        cancelOta.Click+=(_,_)=>{try{service.CancelOta();UpdateOffer();result.Text="";}catch(InvalidOperationException ex){result.Text=ex.Message;}};
        audio.Controls.Add(Heading("语音输入"));audio.Controls.Add(Note("识别后在 TAB5 检查，再手动发送。"));
        var voice=new Button{Text="语音设置…"};audio.Controls.Add(Buttons(voice));voice.Click+=(_,_)=>service.ShowVoiceSettings(this);
        bool busy=false,loading=false;
        FormClosing+=(_,e)=>{
            if(busy||verifying||service.Busy||benchmarkCancel is not null) {
                e.Cancel=true;result.Text="设备正在处理操作，请先完成升级／录音，或停止测速后再关闭。";
            }
        };
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
            UpdateConnection();
            UpdateOffer();
            benchmarkStart.Enabled=benchmarkCancel is null&&!service.Busy&&service.DeviceView.Usb=="已连接";
            if(loadNetworks&&!busy&&!fields.ContainsFocus&&tabs.SelectedIndex==0&&Environment.TickCount64>=nextRefresh){nextRefresh=Environment.TickCount64+10000;await Run(RefreshNetworks,"",true);}
        };
        if(loadNetworks)Shown+=async(_,_)=>await Run(RefreshNetworks,"");
        _timer.Start();RefreshPorts();SettingsWindow.FitScreen(this);ResumeLayout(true);
    }
    // Keep the actions visible while notes/results scroll inside their own row.
    private sealed class ContentPage : TableLayoutPanel {
        private bool _arranging;
        internal ContentPage(){ColumnCount=1;ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));}
        protected override void OnControlAdded(ControlEventArgs e) {
            int row=Controls.Count-1;RowCount=Controls.Count;SetCellPosition(e.Control!,new(0,row));
            RowStyles.Add(new RowStyle(e.Control!.Dock==DockStyle.Fill?SizeType.Percent:SizeType.AutoSize,e.Control.Dock==DockStyle.Fill?100:0));
            if(e.Control.Dock!=DockStyle.Fill)e.Control.Anchor=AnchorStyles.Left|AnchorStyles.Right|AnchorStyles.Top;
            base.OnControlAdded(e);
        }
        protected override void OnLayout(LayoutEventArgs e) {
            if(_arranging){base.OnLayout(e);return;}_arranging=true;
            try {
                int width=Math.Max(80,ClientSize.Width-Padding.Horizontal);
                foreach(Control child in Controls) {
                    int available=Math.Max(1,width-child.Margin.Horizontal);
                    if(child is Label label)label.MaximumSize=new(available,0);
                    if(child is FlowLayoutPanel)child.MaximumSize=new(available,0);
                }
                base.OnLayout(e);
            }finally{_arranging=false;}
        }
    }
    private static Label Note(string text)=>new(){Text=text,AutoSize=true,ForeColor=Color.FromArgb(86,102,117),Margin=new Padding(0,4,0,8)};
    private static Label Heading(string text)=>new(){Text=text,AutoSize=true,Font=SettingsWindow.SectionFont(),Margin=new Padding(0,8,0,4)};
    private static FlowLayoutPanel Buttons(params Button[] buttons) {
        var row=new FlowLayoutPanel{AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,WrapContents=true,Margin=new Padding(0,2,0,2)};
        foreach(var button in buttons){StyleCompactButton(button);button.Margin=new Padding(0,0,6,2);row.Controls.Add(button);}return row;
    }
    private static void StyleCompactButton(Button button,bool primary=false) {
        SettingsWindow.StyleButton(button,primary);
    }
    protected override void Dispose(bool disposing){if(disposing&&!_resourcesDisposed){_resourcesDisposed=true;_timer.Stop();_stop.Cancel();_timer.Dispose();_stop.Dispose();}base.Dispose(disposing);}
}
