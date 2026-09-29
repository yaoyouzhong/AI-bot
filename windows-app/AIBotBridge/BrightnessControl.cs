namespace AIBotBridge;

internal sealed class BrightnessControl : UserControl
{
    private readonly Func<Task<UsbDeviceInfo>> _readDevice;
    private readonly Func<int,bool> _sendBrightness;
    private readonly TrackBar _brightness = new() { AutoSize=false,Height=34,Minimum = 0, Maximum = 100, TickFrequency = 10, Value = 100, Width = 260 };
    private readonly Label _brightnessValue = new() { AutoSize = true, Text = "屏幕亮度 · 读取中" };
    private readonly Label _status = new() { AutoSize = true, ForeColor = Color.DimGray };
    private readonly System.Windows.Forms.Timer _brightnessTimer=new(){Interval=180};
    private int? _pendingBrightness;
    private bool _readingBrightness=true,_sendingBrightness;

    internal BrightnessControl(Func<Task<UsbDeviceInfo>> readDevice,Func<int,bool> sendBrightness)
    {
        SuspendLayout();
        _readDevice=readDevice;_sendBrightness=sendBrightness;
        AutoSize=true;AutoSizeMode=AutoSizeMode.GrowAndShrink;Margin=Padding.Empty;
        _brightness.Name="screen-brightness";
        _brightness.Enabled=false;
        _brightness.ValueChanged += (_, _) => {
            _brightnessValue.Text = "屏幕亮度 · "+_brightness.Value + "%";
            if(_readingBrightness)return;
            _pendingBrightness=_brightness.Value;_brightnessTimer.Stop();_brightnessTimer.Start();
        };
        _brightnessTimer.Tick+=async(_,_)=>await SendPendingBrightnessAsync();
        _brightness.MouseUp+=async(_,_)=>await SendPendingBrightnessAsync();

        var layout=new TableLayoutPanel{AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,Dock=DockStyle.Top,ColumnCount=1,RowCount=3,Margin=Padding.Empty};
        layout.ColumnStyles.Add(new(SizeType.Percent,100));for(int i=0;i<3;i++)layout.RowStyles.Add(new(SizeType.AutoSize));
        _brightnessValue.Dock=_brightness.Dock=_status.Dock=DockStyle.Fill;
        layout.Controls.Add(_brightnessValue,0,0);layout.Controls.Add(_brightness,0,1);
        _status.Text="调整后自动生效";
        layout.Controls.Add(_status,0,2);
        Controls.Add(layout);
        ResumeLayout(true);
    }
    internal async Task ReadAsync()
    {
            try {
                var info=await _readDevice();
                if(!IsDisposed){_brightness.Value=Math.Clamp(info.Brightness,0,100);_brightnessValue.Text="屏幕亮度 · "+info.Brightness+"%";}
            }catch(Exception ex) when(ex is IOException or TimeoutException or InvalidOperationException){if(!IsDisposed){_brightnessValue.Text="屏幕亮度 · 未读取";SetStatus(false,"","未读到当前亮度，请检查 USB 连接。");}}
            finally{_readingBrightness=false;if(!IsDisposed)_brightness.Enabled=true;}
    }

    internal async Task SendPendingBrightnessAsync()
    {
        _brightnessTimer.Stop();
        if(_sendingBrightness)return;
        _sendingBrightness=true;
        try {
            while(_pendingBrightness is int level) {
                _pendingBrightness=null;bool sent;
                try{sent=await Task.Run(()=>_sendBrightness(level));}
                catch(Exception ex) when(ex is IOException or TimeoutException or InvalidOperationException){sent=false;}
                if(!IsDisposed&&!Disposing)SetStatus(sent,"调整后自动生效","亮度未发送，请检查 USB 连接。");
                // Keep only the latest change while the transport is occupied.
            }
        }finally{_sendingBrightness=false;}
    }

    private void SetStatus(bool success, string successText, string errorText)
    {
        _status.ForeColor = success ? Color.DimGray : Color.Firebrick;
        _status.Text = success ? successText : errorText;
    }

    protected override void Dispose(bool disposing)
    {
        if(disposing)_brightnessTimer.Dispose();
        base.Dispose(disposing);
    }

}
