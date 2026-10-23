using System.Drawing.Drawing2D;
using System.Security.Cryptography;
using System.Text;
using AIBot.LoginMethod;
namespace AIBotBridge;

internal sealed class Tab5PinForm : Form
{
    internal Tab5PinForm(Tab5PinStore? store=null,Func<Tab5Pairing?>? pairing=null,Func<bool>? canEdit=null,ComputerUnlockSetup? setup=null)
    {
        store??=new();pairing??=()=>new Tab5PairingStore().Current;canEdit??=()=>WindowsSessionLock.Read()==false;
        setup??=new();var readiness=new UnlockSetupStatus(false,false);bool busy=false;int revision=0;
        var closed=new CancellationTokenSource();
        Text="TAB5 · 一键解锁";UnlockSettingsStyle.Apply(this,572);
        var outer=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new(28,24,28,24),ColumnCount=1,RowCount=2};
        outer.ColumnStyles.Add(new(SizeType.Percent,100));outer.RowStyles.Add(new(SizeType.Percent,100));outer.RowStyles.Add(new(SizeType.Absolute,60));Controls.Add(outer);
        var scroll=new Panel{Dock=DockStyle.Fill,AutoScroll=true,Margin=Padding.Empty};outer.Controls.Add(scroll,0,0);
        var layout=new TableLayoutPanel{Dock=DockStyle.Top,Height=464,ColumnCount=1,RowCount=13,Margin=Padding.Empty};
        layout.ColumnStyles.Add(new(SizeType.Percent,100));scroll.Controls.Add(layout);
        float[] heights=[52,0,52,36,48,0,40,52,40,52,52,40,0];
        for(int i=0;i<heights.Length;i++)layout.RowStyles.Add(new(SizeType.Absolute,heights[i]));
        void Add(Control c,int row){c.Dock=DockStyle.Fill;c.Margin=Padding.Empty;layout.Controls.Add(c,0,row);}
        Add(new Label{Text="一键解锁",Font=SettingsWindow.TitleFont()},0);
        var stateRow=new TableLayoutPanel{ColumnCount=2,RowCount=1,Margin=Padding.Empty};stateRow.ColumnStyles.Add(new(SizeType.Percent,100));stateRow.ColumnStyles.Add(new(SizeType.Absolute,52));Add(stateRow,2);
        var status=new Label{Name="pin-status",Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft,Margin=Padding.Empty};stateRow.Controls.Add(status,0,0);
        var toggle=new Tab5PinSwitch{Name="enable-oneclick",AccessibleName="启用一键解锁",Size=new(52,32),Anchor=AnchorStyles.Right,Margin=Padding.Empty};stateRow.Controls.Add(toggle,1,0);
        Add(new Label{Text="登录方式",TextAlign=ContentAlignment.BottomLeft,Padding=new(0,0,0,7)},3);
        var method=new ComboBox{Name="login-method",AccessibleName="登录方式",DropDownStyle=ComboBoxStyle.DropDownList};
        method.Items.AddRange(["请选择 PIN 或密码","Windows Hello PIN","Windows 密码"]);Add(method,4);
        Add(new Label{Text="Windows PIN / 密码",TextAlign=ContentAlignment.BottomLeft,Padding=new(0,0,0,7)},6);
        var pin=new Tab5PinField("saved-pin","Windows PIN 或密码");Add(pin,7);
        Add(new Label{Text="再次输入",TextAlign=ContentAlignment.BottomLeft,Padding=new(0,0,0,7)},8);
        var repeat=new Tab5PinField("repeat-pin","再次输入 PIN 或密码");Add(repeat,9);
        Add(new Label{Text="1–64 位：英文字母、数字、空格和英文符号。\n已有凭据时，可留空仅修改登录方式。",ForeColor=SettingsWindow.Muted,Padding=new(0,6,0,0)},10);
        var feedback=new Label{Name="pin-feedback",ForeColor=Color.FromArgb(170,49,49),Padding=new(0,4,0,0)};Add(feedback,11);
        feedback.TextChanged+=(_,_)=>scroll.ScrollControlIntoView(feedback);
        var actions=UnlockSettingsStyle.Footer();outer.Controls.Add(actions,0,1);
        var remove=UnlockSettingsStyle.Action("delete-pin","删除凭据",100);remove.ForeColor=Color.FromArgb(170,49,49);actions.Controls.Add(remove,0,0);
        var save=UnlockSettingsStyle.Action("save-pin","保存",108,true);actions.Controls.Add(save,2,0);
        void Hide(){pin.HideValue();repeat.HideValue();}
        void Clear(){pin.ClearValue();repeat.ClearValue();}
        bool refreshing=false;
        void RefreshState(bool reloadMethod=true){var pair=pairing();bool has=store.Has(pair),enabled=store.Enabled(pair);refreshing=true;try{toggle.Checked=enabled;}finally{refreshing=false;}
            bool configured=has&&store.Method(pair)!=LoginMethodKind.Manual;
            status.Text=pair is null?"请先配对 TAB5":!enabled?"已关闭":!configured?"请完成下方设置":readiness.Ready?"已就绪":!readiness.MethodReady?"登录方式尚未就绪":"Wi-Fi 尚未就绪";status.ForeColor=enabled&&configured&&readiness.Ready?SettingsWindow.Accent:SettingsWindow.Muted;
            toggle.Enabled=save.Enabled=method.Enabled=pair is not null;save.Text=has?"保存修改":"保存";remove.Enabled=store.Exists;
            if(reloadMethod)method.SelectedIndex=(int)store.Method(pair);}
        toggle.CheckedChanged+=(_,_)=>{if(refreshing)return;Hide();try{if(!canEdit()){feedback.Text="请先解锁电脑。";return;}var pair=pairing();if(pair is null)return;store.SetEnabled(pair,toggle.Checked);feedback.Text="";}
            catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or CryptographicException){feedback.Text="设置未保存，请重试。";}
            finally{RefreshState(false);}};
        save.Click+=async(_,_)=>{
            byte[]? bytes=null;feedback.ForeColor=Color.FromArgb(170,49,49);
            try{
                if(!canEdit()){feedback.Text="请先在电脑解锁后设置。";return;}
                var pair=pairing();if(pair is null){feedback.Text="请先配对 TAB5。";return;}
                var selected=(LoginMethodKind)method.SelectedIndex;if(selected is not (LoginMethodKind.Pin or LoginMethodKind.Password)){feedback.Text="请选择 PIN 或密码。";return;}
                bool retain=pin.Input.Text.Length==0&&repeat.Input.Text.Length==0&&store.Has(pair);
                if(!retain&&(pin.Input.Text!=repeat.Input.Text||pin.Input.Text.Length==0)){feedback.Text="请输入两次相同的 PIN 或密码。";return;}
                if(!retain&&pin.Input.Text.Any(c=>c<0x20||c>0x7e)){feedback.Text="请输入 1–64 位字母、数字或英文符号。";return;}
                busy=true;revision++;save.Enabled=remove.Enabled=toggle.Enabled=method.Enabled=pin.Enabled=repeat.Enabled=false;
                readiness=await setup.EnsureAsync(message=>feedback.Text=message,closed.Token);
                if(IsDisposed)return;
                if(!readiness.Ready){feedback.Text=readiness.Message;return;}
                if(!canEdit()||pairing()!=pair){feedback.Text="会话或设备已变化，请重新保存。";return;}
                if(pin.Input.Text.Length==0&&repeat.Input.Text.Length==0&&store.Has(pair)){
                    store.SetMethod(pair,selected);RefreshState();feedback.ForeColor=SettingsWindow.Accent;feedback.Text="已保存。";return;
                }
                if(pin.Input.Text!=repeat.Input.Text){feedback.Text="两次输入不一致，请重新输入。";return;}
                bytes=Encoding.ASCII.GetBytes(pin.Input.Text);if(pin.Input.Text.Any(c=>c<0x20||c>0x7e)||!Tab5PinStore.Valid(bytes)){feedback.Text="请输入 1–64 位字母、数字或英文符号。";return;}
                store.Save(pair,bytes,selected);RefreshState();feedback.ForeColor=SettingsWindow.Accent;feedback.Text="已保存。";
            }catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or CryptographicException){feedback.Text="未能保存，请重试。";}
            finally{if(bytes is not null)CryptographicOperations.ZeroMemory(bytes);Clear();busy=false;if(!IsDisposed){pin.Enabled=repeat.Enabled=true;RefreshState(false);}}
        };
        remove.Click+=(_,_)=>{try{if(!canEdit()){feedback.Text="请先在电脑解锁后设置。";return;}store.Delete();Clear();RefreshState();feedback.ForeColor=SettingsWindow.Muted;feedback.Text="已删除。";}catch(Exception ex) when(ex is IOException or UnauthorizedAccessException){feedback.Text="未能删除，请重试。";}};
        Shown+=async(_,_)=>{int started=revision;try{var observed=await setup.InspectAsync(closed.Token);if(!IsDisposed&&!busy&&revision==started){readiness=observed;RefreshState(false);}}catch(OperationCanceledException){}};
        Deactivate+=(_,_)=>Hide();FormClosing+=(_,e)=>{if(busy){e.Cancel=true;feedback.Text="正在完成配置，请稍候。";}else{closed.Cancel();Clear();}};FormClosed+=(_,_)=>closed.Dispose();RefreshState();UnlockSettingsStyle.Complete(this);
    }
}

// Reveal is momentary. Mouse/touch release, drag-out, capture loss and window
// deactivation all mask again; Click and accessibility activation never latch it.
internal sealed class Tab5PinField : Panel
{
    internal TextBox Input {get;}
    internal Tab5PinEye Eye {get;}
    internal Tab5PinField(string name,string label)
    {
        BackColor=Color.White;DoubleBuffered=true;
        Input=new TextBox{Name=name,AccessibleName=label,UseSystemPasswordChar=true,MaxLength=64,BorderStyle=BorderStyle.None,Font=new("Microsoft YaHei UI",13),TabIndex=0};
        Eye=new Tab5PinEye(Input){Name=name+"-eye",AccessibleName="按住显示"+label,TabIndex=1};
        Controls.Add(Input);Controls.Add(Eye);Input.GotFocus+=(_,_)=>Invalidate();Input.LostFocus+=(_,_)=>Invalidate();Eye.GotFocus+=(_,_)=>Invalidate();Eye.LostFocus+=(_,_)=>Invalidate();
    }
    protected override void OnLayout(LayoutEventArgs e){base.OnLayout(e);if(Input is null)return;float scale=Input.Font.Size/13f;int pad=(int)(14*scale),eye=Math.Max(40,(int)(48*scale));Eye.SetBounds(Width-eye-3,3,eye,Height-6);Input.SetBounds(pad,(Height-Input.PreferredHeight)/2,Math.Max(20,Width-eye-pad*2),Input.PreferredHeight);}
    protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);using var pen=new Pen(ContainsFocus?SettingsWindow.Accent:SettingsWindow.Border,ContainsFocus?2:1);e.Graphics.DrawRectangle(pen,1,1,Width-3,Height-3);}
    internal void HideValue()=>Eye.HideValue();
    internal void ClearValue(){HideValue();Input.Clear();}
}

internal sealed class Tab5PinEye : Button
{
    private readonly TextBox _input;
    private bool _revealing;
    internal Tab5PinEye(TextBox input){_input=input;SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;BackColor=Color.White;Cursor=Cursors.Hand;AccessibleRole=AccessibleRole.PushButton;AccessibleDescription="按住显示 PIN，松开隐藏。键盘可按住空格。";}
    private void Reveal(){if(!Enabled||_input.IsDisposed)return;_revealing=true;int start=_input.SelectionStart,length=_input.SelectionLength;_input.UseSystemPasswordChar=false;_input.Select(start,length);Invalidate();}
    internal void HideValue(){if(!_input.IsDisposed){int start=_input.SelectionStart,length=_input.SelectionLength;_input.UseSystemPasswordChar=true;_input.Select(start,length);}_revealing=false;Invalidate();}
    protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);if(e.Button==MouseButtons.Left){Capture=true;Reveal();}}
    protected override void OnMouseUp(MouseEventArgs e){HideValue();base.OnMouseUp(e);}
    protected override void OnMouseLeave(EventArgs e){HideValue();base.OnMouseLeave(e);}
    protected override void OnMouseCaptureChanged(EventArgs e){if(!Capture)HideValue();base.OnMouseCaptureChanged(e);}
    protected override void OnLostFocus(EventArgs e){HideValue();base.OnLostFocus(e);}
    protected override void OnEnabledChanged(EventArgs e){if(!Enabled)HideValue();base.OnEnabledChanged(e);}
    protected override void OnKeyDown(KeyEventArgs e){if(e.KeyCode==Keys.Space&&!e.Alt&&!e.Control){Reveal();e.Handled=e.SuppressKeyPress=true;}else base.OnKeyDown(e);}
    protected override void OnKeyUp(KeyEventArgs e){HideValue();if(e.KeyCode==Keys.Space)e.Handled=e.SuppressKeyPress=true;else base.OnKeyUp(e);}
    protected override void OnPaint(PaintEventArgs e){
        e.Graphics.Clear(_revealing?SettingsWindow.Surface:BackColor);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
        float unit=_input.Font.Size/13f,cx=Width/2f,cy=Height/2f;using var pen=new Pen(_revealing?SettingsWindow.Accent:SettingsWindow.Muted,1.7f*unit){StartCap=LineCap.Round,EndCap=LineCap.Round};
        using var path=new GraphicsPath();path.AddBezier(cx-11*unit,cy,cx-4*unit,cy-10*unit,cx+4*unit,cy-10*unit,cx+11*unit,cy);path.AddBezier(cx+11*unit,cy,cx+4*unit,cy+10*unit,cx-4*unit,cy+10*unit,cx-11*unit,cy);e.Graphics.DrawPath(pen,path);e.Graphics.DrawEllipse(pen,cx-3*unit,cy-3*unit,6*unit,6*unit);
        if(!_revealing)e.Graphics.DrawLine(pen,cx-9*unit,cy-9*unit,cx+9*unit,cy+9*unit);
        if(Focused&&ShowFocusCues)ControlPaint.DrawFocusRectangle(e.Graphics,Rectangle.Inflate(ClientRectangle,-5,-5));
    }
}

// Native CheckBox semantics preserve keyboard and accessibility toggle support.
internal sealed class Tab5PinSwitch : CheckBox
{
    internal Tab5PinSwitch(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);AutoSize=false;Cursor=Cursors.Hand;AccessibleRole=AccessibleRole.CheckButton;}
    protected override void OnCheckedChanged(EventArgs e){base.OnCheckedChanged(e);Invalidate();}
    protected override void OnPaint(PaintEventArgs e){
        e.Graphics.Clear(BackColor);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
        float h=Height*0.7f,w=Width-4,y=(Height-h)/2;
        using var path=new GraphicsPath();path.AddArc(2,y,h,h,90,180);path.AddArc(2+w-h,y,h,h,270,180);path.CloseFigure();
        using var track=new SolidBrush(!Enabled?SettingsWindow.Border:Checked?SettingsWindow.Accent:Color.FromArgb(160,170,182));e.Graphics.FillPath(track,path);
        float d=h-6,x=Checked?2+w-h+3:5;using var knob=new SolidBrush(Color.White);e.Graphics.FillEllipse(knob,x,y+3,d,d);
        if(Focused&&ShowFocusCues)ControlPaint.DrawFocusRectangle(e.Graphics,Rectangle.Inflate(ClientRectangle,-1,-1));
    }
}
