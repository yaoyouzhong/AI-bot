namespace AIBotBridge;

// Keep the action separate from its current state, and acknowledge the verified result.
internal sealed class StartupSettingControl : TableLayoutPanel
{
    private readonly Func<bool> _read;
    private readonly Func<bool,Task> _change;
    private readonly Label _state=new(){Name="startup-state",AutoSize=true,Anchor=AnchorStyles.Right,Margin=new Padding(12,0,0,0)};
    private readonly Label _feedback=new(){Name="startup-feedback",AutoSize=true,Dock=DockStyle.Fill,Margin=new Padding(0,8,0,0)};
    private readonly CheckBox _toggle=new(){Name="startup-toggle",Text="登录 Windows 后自动运行 AI-bot",AutoSize=true,AutoCheck=false,Margin=new Padding(0,10,0,0)};
    private bool _enabled,_busy;

    internal StartupSettingControl(Func<bool> read,Func<bool,Task> change)
    {
        _read=read;_change=change;Name="startup-setting";AutoSize=true;ColumnCount=2;RowCount=3;Font=new Font("Microsoft YaHei UI",9F);
        Padding=new Padding(16);Margin=new Padding(0,8,0,0);BackColor=Color.FromArgb(247,249,252);
        ColumnStyles.Add(new(SizeType.Percent,100));ColumnStyles.Add(new(SizeType.AutoSize));
        for(int i=0;i<3;i++)RowStyles.Add(new(SizeType.AutoSize));
        var title=new Label{Text="开机启动",AutoSize=true,Margin=Padding.Empty};
        title.Font=new Font(title.Font,FontStyle.Bold);
        Controls.Add(title,0,0);Controls.Add(_state,1,0);
        Controls.Add(_toggle,0,1);SetColumnSpan(_toggle,2);
        Controls.Add(_feedback,0,2);SetColumnSpan(_feedback,2);
        _toggle.Click+=async(_,_)=>await ChangeAsync();RefreshState();
    }

    internal void RefreshState()
    {
        if(_busy||IsDisposed)return;
        try{bool current=_read();if(current!=_enabled)_feedback.Text="";_enabled=current;ShowState();}
        catch(Exception ex){_state.Text="状态未知";_state.ForeColor=Color.Firebrick;_toggle.Enabled=false;Feedback("读取失败："+ex.Message,true);}
    }
    private void ShowState()
    {
        _state.Text=_enabled?"已开启":"已关闭";_state.ForeColor=_enabled?Color.FromArgb(24,123,72):Color.DimGray;
        _toggle.Checked=_enabled;_toggle.AccessibleName="开机启动";_toggle.Enabled=true;
    }
    private void Feedback(string text,bool error=false){_feedback.Text=text;_feedback.ForeColor=error?Color.Firebrick:Color.FromArgb(24,123,72);}
    internal async Task ChangeAsync()
    {
        if(_busy||!_toggle.Enabled||IsDisposed)return;
        bool target=!_enabled;_busy=true;_toggle.Checked=target;_toggle.Enabled=false;
        _state.Text=target?"正在开启…":"正在关闭…";_state.ForeColor=Color.DimGray;_feedback.Text="";
        try{
            await _change(target);
            if(IsDisposed)return;
            _enabled=_read();
            if(_enabled!=target)throw new IOException("设置未生效，请重试。");
            Feedback(target?"已开启，下次登录 Windows 时自动运行。":"已关闭，下次登录 Windows 时不再自动运行。");
        }catch(Exception ex){
            if(IsDisposed)return;
            try{_enabled=_read();}catch{ /* Preserve the action's error and last known state. */ }
            Feedback("未能"+(target?"开启":"关闭")+"："+ex.Message,true);
        }finally{_busy=false;if(!IsDisposed)ShowState();}
    }
}
