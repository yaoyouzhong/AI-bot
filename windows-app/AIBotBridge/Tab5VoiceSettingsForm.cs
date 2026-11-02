using System.Diagnostics;
using NAudio.CoreAudioApi;

namespace AIBotBridge;

internal sealed record Tab5VoiceEnvironment(Tab5AudioDevice[] Inputs,Tab5AudioDevice[] Outputs,bool? DoubaoInstalled,bool? DoubaoRunning,string? WindowsRecognizer=null)
{
    internal static Task<Tab5VoiceEnvironment> CaptureAsync()=>Task.Run(()=> {
        var inputs=Tab5VoiceAudio.Devices(DataFlow.Capture);var outputs=Tab5VoiceAudio.Devices(DataFlow.Render);
        var service=Tab5DoubaoAvailability.Observe();return new Tab5VoiceEnvironment(inputs,outputs,service.Installed,service.Running,WindowsSpeechBackend.Availability());
    });
    internal string ServiceStatus=>DoubaoRunning==true?"豆包输入法正在运行；实际收音在开始录音时确认。":
        DoubaoInstalled==false?"未安装官方豆包输入法，请先安装后使用。":
        DoubaoInstalled==true?"豆包已安装，输入服务尚未运行；开始录音时会再次检查。":"无法确认豆包输入法状态，请在电脑检查安装与运行。";
    internal string ServiceStatusFor(string provider)=>provider=="windows"?
        WindowsRecognizer is null?"未检测到微软简体中文识别器，请检查 Windows 语言设置中的语音组件。":"已检测到微软简体中文识别器；本机识别，无需豆包或联网。":ServiceStatus;
    internal string ExpectedInput(string selected,string provider){
        var route=Tab5VoiceAudio.Inspect(Inputs,Outputs,selected);
        return route.SelectedMissing||provider!="windows"&&!route.Ready?route.Message:Tab5VoiceAudio.SelectInput(Inputs,selected)?.Name??"TAB5 内置麦克风";
    }
    internal string? Issue(string selected,string provider="doubao") {
        if(provider is not ("doubao" or "windows"))return "识别方式无效，请重新选择。";
        var route=Tab5VoiceAudio.Inspect(Inputs,Outputs,selected);
        if(provider=="windows")return WindowsRecognizer is null?ServiceStatusFor(provider):route.SelectedMissing?route.Message:null;
        return !route.Ready?route.Message:DoubaoInstalled==false?ServiceStatus:null;
    }
}

// One transactional voice entry, matching the Mac settings' information order.
// Enumeration does not open microphones, change the system default or select an IME.
internal sealed class Tab5VoiceSettingsForm : Form
{
    private Tab5VoiceSettings _baseline;
    private readonly Func<bool> _busy;
    private readonly Func<Tab5VoiceSettings> _read;
    private readonly Action<Tab5VoiceSettings> _commit;
    private readonly Func<Task<Tab5VoiceEnvironment>> _inspect;
    private readonly bool _preview;
    private readonly Form? _embeddedOwner;
    private readonly ComboBox _inputs=new(){Name="voice-inputs",DropDownStyle=ComboBoxStyle.DropDownList,Dock=DockStyle.Fill};
    private readonly ComboBox _provider=new(){Name="voice-provider",DropDownStyle=ComboBoxStyle.DropDownList,Dock=DockStyle.Fill,Margin=new(0,0,0,12)};
    private readonly Label _availability=new(){Name="voice-availability",AutoSize=true,ForeColor=SettingsWindow.Muted,Dock=DockStyle.Fill,Margin=new(0,8,0,0)};
    private readonly Label _guidance=new(){Name="voice-guidance",AutoSize=true,ForeColor=SettingsWindow.Muted,Dock=DockStyle.Fill,Margin=new(0,8,0,0)};
    private readonly Label _status=new(){Name="voice-status",AutoSize=true,ForeColor=SettingsWindow.Muted,Dock=DockStyle.Fill,Margin=new(24,8,24,8)};
    private readonly Button _save=new(){Name="voice-save",Text="保存"};
    private readonly Button _cancel=new(){Name="voice-cancel",Text="取消",CausesValidation=false};
    private readonly Button _refresh=new(){Name="voice-refresh",Text="刷新设备"};
    private readonly LinkLabel _install=new(){Name="voice-install-doubao",Text="安装豆包输入法…",AutoSize=true,LinkColor=SettingsWindow.Accent,Margin=new(0,12,0,0)};
    private readonly ToolTip _tooltip=new();
    private Tab5VoiceEnvironment? _environment;
    private string _selected;
    private string _speechProvider;
    private int _generation;
    private bool _loading,_saving,_binding,_closed;
    internal string EditFingerprint=>_speechProvider+"|"+_selected;
    private bool Unchanged=>_selected==_baseline.DjiId&&_speechProvider==_baseline.Provider;
    internal bool SaveAvailable=>_save.Enabled;
    internal string StatusText=>_status.Text;
    internal string SelectedInput=>_selected;

    internal Tab5VoiceSettingsForm(Tab5VoiceSettings initial,Func<bool> busy,Func<Tab5VoiceSettings> read,
        Action<Tab5VoiceSettings> commit,bool preview=false,Form? embeddedOwner=null,Func<Task<Tab5VoiceEnvironment>>? inspect=null)
    {
        _baseline=initial with {Enabled=true};_selected=_baseline.DjiId;_speechProvider=_baseline.Provider;_busy=busy;_read=read;_commit=commit;
        _preview=preview;_embeddedOwner=embeddedOwner;_inspect=inspect??Tab5VoiceEnvironment.CaptureAsync;
        SuspendLayout();AutoScaleDimensions=new SizeF(96,96);AutoScaleMode=AutoScaleMode.Dpi;
        Font=SettingsWindow.BodyFont();Text="TAB5 · 语音设置";ClientSize=new Size(560,480);MinimumSize=new Size(520,420);
        BackColor=Color.White;ForeColor=SettingsWindow.Ink;StartPosition=FormStartPosition.CenterParent;
        if(embeddedOwner is not null){TopLevel=false;FormBorderStyle=FormBorderStyle.None;MinimumSize=Size.Empty;Dock=DockStyle.Fill;}
        var shell=new TableLayoutPanel{Dock=DockStyle.Fill,RowCount=3,ColumnCount=1,Margin=Padding.Empty};
        shell.ColumnStyles.Add(new(SizeType.Percent,100));shell.RowStyles.Add(new(SizeType.Percent,100));
        shell.RowStyles.Add(new(SizeType.AutoSize));shell.RowStyles.Add(new(SizeType.AutoSize));Controls.Add(shell);
        var scroll=new Panel{Dock=DockStyle.Fill,AutoScroll=true,Name="voice-scroll",Margin=Padding.Empty,Padding=new(24)};
        var body=new TableLayoutPanel{Dock=DockStyle.Top,AutoSize=true,ColumnCount=1,Margin=Padding.Empty,Padding=Padding.Empty};
        body.ColumnStyles.Add(new(SizeType.Percent,100));scroll.Controls.Add(body);shell.Controls.Add(scroll,0,0);
        void Add(Control control){int row=body.RowCount++;body.RowStyles.Add(new(SizeType.AutoSize));body.Controls.Add(control,0,row);}
        var title=SettingsWindow.Title("语音输入");title.Margin=new(0,0,0,6);Add(title);
        Add(new Label{Text="保存后用于下一段录音。",AutoSize=true,ForeColor=SettingsWindow.Muted,Margin=new(0,0,0,20)});
        var fields=new TableLayoutPanel{AutoSize=true,Dock=DockStyle.Top,ColumnCount=2,RowCount=2,Margin=Padding.Empty};
        fields.ColumnStyles.Add(new(SizeType.Absolute,90));fields.ColumnStyles.Add(new(SizeType.Percent,100));
        _provider.Items.AddRange(["豆包输入法","Windows 本地识别"]);
        if(_speechProvider is not ("doubao" or "windows"))_provider.Items.Add("原识别方式（不可用）");
        _provider.SelectedIndex=_speechProvider=="doubao"?0:_speechProvider=="windows"?1:2;
        ConfigurePicker(_provider);ConfigurePicker(_inputs);
        fields.Controls.Add(new Label{Text="识别方式",AutoSize=true,Margin=new(0,5,12,12)},0,0);fields.Controls.Add(_provider,1,0);
        fields.Controls.Add(new Label{Text="录音麦克风",AutoSize=true,Margin=new(0,5,12,0)},0,1);_inputs.Margin=Padding.Empty;fields.Controls.Add(_inputs,1,1);Add(fields);
        Add(_guidance);Add(new Label{Text="收音状态",AutoSize=true,Font=SettingsWindow.SectionFont(),Margin=new(0,22,0,0)});Add(_availability);Add(_install);
        shell.Controls.Add(_status,0,1);
        var actions=new TableLayoutPanel{Dock=DockStyle.Fill,AutoSize=true,ColumnCount=3,Padding=new(24,8,24,20),Margin=Padding.Empty,Name="voice-actions"};
        actions.ColumnStyles.Add(new(SizeType.Percent,100));actions.ColumnStyles.Add(new(SizeType.AutoSize));actions.ColumnStyles.Add(new(SizeType.AutoSize));
        _refresh.Anchor=AnchorStyles.Left;_save.Margin=new(8,0,0,0);_cancel.Margin=Padding.Empty;
        actions.Controls.Add(_refresh,0,0);actions.Controls.Add(_cancel,1,0);actions.Controls.Add(_save,2,0);shell.Controls.Add(actions,0,2);
        SettingsWindow.StyleButton(_save,true);SettingsWindow.StyleButton(_cancel);SettingsWindow.StyleButton(_refresh);
        _inputs.SelectedIndexChanged+=(_,_)=>{if(_binding)return;if(_inputs.SelectedItem is Tab5AudioDevice d)_selected=d.Id;UpdateAvailability();_status.Text=Unchanged?"":"尚未保存";};
        _provider.SelectedIndexChanged+=(_,_)=>{if(_binding)return;_speechProvider=_provider.SelectedIndex==0?"doubao":_provider.SelectedIndex==1?"windows":_baseline.Provider;UpdateAvailability();_status.Text=Unchanged?"":"尚未保存";};
        _refresh.Click+=async(_,_)=>await ReloadAsync();_save.Click+=async(_,_)=>await SaveAsync();
        _cancel.Click+=(_,_)=>{if(_embeddedOwner is null)Close();else{_selected=_baseline.DjiId;SelectProvider(_baseline.Provider);BindChoices();_status.Text="";}};
        _install.LinkClicked+=(_,_)=>Process.Start(new ProcessStartInfo("https://srf.doubao.com/pc"){UseShellExecute=true});
        _inputs.Items.Add(new Tab5AudioDevice(_selected,"正在读取麦克风…"));_inputs.SelectedIndex=0;
        _provider.Enabled=_inputs.Enabled=_save.Enabled=false;_install.Visible=false;_status.Text="正在读取麦克风…";
        AcceptButton=_save;CancelButton=_cancel;if(!preview&&embeddedOwner is null)SettingsWindow.FitScreen(this);ResumeLayout(true);
        if(embeddedOwner is not null)embeddedOwner.FormClosing+=(_,e)=>{if(!e.Cancel&&!Unchanged&&!UnsavedChanges.Confirm(embeddedOwner))e.Cancel=true;};
    }
    protected override bool ShowWithoutActivation=>_preview;
    protected override void OnLoad(EventArgs e){
        base.OnLoad(e);
        if(TopLevel){float scale=DeviceDpi/96f;ClientSize=new Size((int)(560*scale),(int)(480*scale));MinimumSize=new Size((int)(520*scale),(int)(420*scale));}
    }
    internal static void DrawPickerText(Graphics graphics,ComboBox combo,Rectangle bounds,string? text,Color color,Color? background=null)=>
        TextRenderer.DrawText(graphics,text,combo.Font,bounds,color,background??combo.BackColor,
            TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix);
    private static void ConfigurePicker(ComboBox combo){
        combo.DrawMode=DrawMode.OwnerDrawFixed;combo.ItemHeight=combo.Font.Height+6;
        combo.FontChanged+=(_,_)=>combo.ItemHeight=combo.Font.Height+6;
        combo.DrawItem+=(_,e)=>{
            e.DrawBackground();if(e.Index<0||e.Index>=combo.Items.Count)return;
            var bounds=Rectangle.Inflate(e.Bounds,-4,0);
            DrawPickerText(e.Graphics,combo,bounds,combo.Items[e.Index]?.ToString(),combo.Enabled?e.ForeColor:SettingsWindow.Muted,e.BackColor);
            e.DrawFocusRectangle();
        };
    }
    protected override void OnShown(EventArgs e){base.OnShown(e);_ = ReloadAsync();}
    protected override void OnFormClosing(FormClosingEventArgs e){base.OnFormClosing(e);if(!e.Cancel){_closed=true;++_generation;}}
    protected override void Dispose(bool disposing){if(disposing){_closed=true;++_generation;_tooltip.Dispose();}base.Dispose(disposing);}
    private bool Current(int generation)=>!_closed&&!IsDisposed&&generation==_generation;
    private void BindChoices(){
        _binding=true;_inputs.BeginUpdate();try{
            _inputs.Items.Clear();_inputs.Items.Add(new Tab5AudioDevice("","自动（优先 DJI，否则 TAB5）"));
            _inputs.Items.Add(new Tab5AudioDevice(Tab5VoiceAudio.Tab5Selection,"TAB5 内置麦克风"));
            _inputs.Items.Add(new Tab5AudioDevice(Tab5VoiceAudio.SystemSelection,"Windows 系统默认麦克风"));
            foreach(var d in _environment?.Inputs??[])if(!Tab5VoiceAudio.IsCableCapture(d.Name))_inputs.Items.Add(d);
            var index=_inputs.Items.Cast<Tab5AudioDevice>().ToList().FindIndex(d=>d.Id==_selected);
            if(index<0){_inputs.Items.Add(new Tab5AudioDevice(_selected,"原选择（未连接或不可用）"));index=_inputs.Items.Count-1;}
            _inputs.SelectedIndex=index;
        }finally{_inputs.EndUpdate();_binding=false;}UpdateAvailability();
    }
    private void UpdateAvailability(){
        _tooltip.SetToolTip(_inputs,_inputs.SelectedItem?.ToString());
        _guidance.Text=_selected.Length>0&&_selected!=Tab5VoiceAudio.Tab5Selection&&_selected!=Tab5VoiceAudio.SystemSelection?"设备断开时不自动切换。":"";
        if(_environment is not {} env)return;
        string expected=env.ExpectedInput(_selected,_speechProvider);
        _availability.Text=(Unchanged?"下次使用：":"保存后使用：")+expected+Environment.NewLine+env.ServiceStatusFor(_speechProvider);
        _install.Visible=_speechProvider=="doubao"&&env.DoubaoInstalled==false;
        _provider.Enabled=_inputs.Enabled=!_loading&&!_saving;_save.Enabled=!_loading&&!_saving;
    }
    internal async Task ReloadAsync(){
        if(_closed||_saving)return;int owner=++_generation;_loading=true;_environment=null;
        _provider.Enabled=_inputs.Enabled=_save.Enabled=false;_availability.Text="正在读取麦克风…";_status.Text="正在读取麦克风…";
        try{var env=await _inspect();if(!Current(owner))return;_environment=env;_loading=false;BindChoices();_status.Text=Unchanged?"":"尚未保存";}
        catch(Exception ex) when(Tab5VoiceHost.IsAudioError(ex)||ex is IOException){if(Current(owner)){_loading=false;_status.Text="无法读取音频设备，原设置保留，请刷新重试。";}}
    }
    internal async Task SaveAsync(){
        if(_closed||_saving||_loading||_environment is null)return;
        if(_busy()){_status.Text="请等待当前录音结束后保存。";return;}
        int owner=++_generation;var value=_baseline with {Enabled=true,DjiId=_selected,Provider=_speechProvider};
        _saving=true;_provider.Enabled=_inputs.Enabled=_save.Enabled=_refresh.Enabled=false;_status.Text="正在检查设备…";
        try{
            var env=await _inspect();if(!Current(owner))return;_environment=env;
            if(_busy()||_selected!=value.DjiId||_speechProvider!=value.Provider||(_read() with {Enabled=true})!=_baseline){_status.Text="录音或设置已发生变化，本次未保存，请检查后重试。";return;}
            if(env.Issue(value.DjiId,value.Provider) is {} issue){_status.Text=issue;return;}
            _commit(value);_baseline=value;UnsavedChanges.MarkSaved(this);
            if(_embeddedOwner is null){DialogResult=DialogResult.OK;Close();}else _status.Text="已保存";
        }catch(Exception ex) when(Tab5VoiceHost.IsAudioError(ex)||ex is IOException or UnauthorizedAccessException){if(Current(owner))_status.Text="设置未保存，原设置保留，请检查音频设备后重试。";}
        finally{_saving=false;if(Current(owner)){_refresh.Enabled=true;UpdateAvailability();}}
    }
    internal void SelectInput(string id){_selected=id;BindChoices();}
    internal void SelectProvider(string id){_speechProvider=id;_binding=true;try{_provider.SelectedIndex=id=="doubao"?0:id=="windows"?1:-1;}finally{_binding=false;}UpdateAvailability();}
}
