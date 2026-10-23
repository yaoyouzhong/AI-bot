using System.Globalization;
using System.Text.RegularExpressions;

namespace AIBotBridge;

internal sealed record Tab5UpgradeStatusText(string Title,string State,string Hint,string Transfer,string Progress)
{
    internal static Tab5UpgradeStatusText Create(string? version,bool online,bool running,bool verified,bool transferring,
        string summary,string remote,string hint,string diagnostic) {
        if(version is null)return new("未选择固件","","选择固件后可查看版本和更新内容。","","");
        string state=verified&&online?"已核验":running?"设备已运行":transferring?"正在传输":online?"待升级":"等待连接";
        string message=verified&&online?"":running?"设备已回报新版本，请核验启动。":
            transferring?"正在传输，请保持连接和供电。":hint.Length>0?hint:remote.Length>0?remote:summary.Contains("等待设备确认版本")?"已提供固件，等待设备确认版本。":"";
        string transfer="",progress="";
        var count=Regex.Match(diagnostic,@"(?:实际传输|已提供)：(\d+)/(\d+)");
        if(count.Success&&long.TryParse(count.Groups[1].Value,out long sent)&&long.TryParse(count.Groups[2].Value,out long total)&&total>0) {
            string channel=diagnostic.Split('；')[0];
            bool compressed=channel.EndsWith(" 分块压缩",StringComparison.Ordinal);
            if(compressed)channel=channel[..^5];
            var image=Regex.Match(diagnostic,@"镜像：(\d+)");
            if(image.Success&&long.TryParse(image.Groups[1].Value,out long imageSize)&&imageSize!=total)transfer="固件 "+Size(imageSize);
            if(compressed&&transfer.Length>0)transfer+="（分块压缩）";
            progress=sent>=total?$"已通过 {channel} 传输 · {Size(total)}":$"通过 {channel} 传输 · {Size(sent)} / {Size(total)}（{Math.Clamp(sent*100/total,0,100)}%）";
            var elapsed=Regex.Match(diagnostic,@"耗时毫秒：(\d+)");
            if(elapsed.Success&&long.TryParse(elapsed.Groups[1].Value,out long milliseconds))
                progress+="　耗时 "+(milliseconds/1000d).ToString("0.0",CultureInfo.InvariantCulture)+" 秒";
        }else if(diagnostic.StartsWith("传输中断",StringComparison.Ordinal)&&!running) {
            state="传输中断";message="请检查连接后重新开始升级。";
        }
        return new(version,state,message,transfer,progress);
    }
    private static string Size(long bytes)=>bytes>=1048576?(bytes/1048576d).ToString("0.00",CultureInfo.InvariantCulture)+" MiB":
        bytes>=1024?(bytes/1024d).ToString("0.0",CultureInfo.InvariantCulture)+" KiB":bytes+" B";
}

internal sealed partial class Tab5Service
{
    internal Tab5UpgradeStatusText UpgradeStatusText {
        get {
            lock(_lifecycle) {
            if(_otaDelivery is {StopRequested:true} stopped)return new(stopped.Image.Version,stopped.Confirmed?"已停止":"正在停止",OtaStopHint,"","");
            var image=Volatile.Read(ref _ota);var view=DeviceView;
            var value=Tab5UpgradeStatusText.Create(image?.Version,view.Online,view.Online&&view.Firmware==image?.Version,
                UpgradeBootSummary=="启动核验通过",Volatile.Read(ref _otaTransfers)>0,OtaSummary,RemoteOtaStatus,OtaStartHint,OtaTransferSummary);
            if(_otaDelivery is {Started:true,FinalReleased:true}&&view.Firmware!=image?.Version)
                value=value with {State="等待校验",Hint="固件已传完，正在等待设备校验和重启，不能停止；请保持供电。"};
            else if(CanStopOta)value=value with {State=RemoteOtaPending?"准备升级":"正在传输",Hint="可点“停止升级”取消本次传输。"};
            return value;
            }
        }
    }
}

// Version/result, guidance, then human-readable transfer details. Raw timings
// remain available through the window's existing diagnostic details link.
internal sealed class Tab5UpgradeStatusView : Panel
{
    private readonly Label _version=new(){Margin=Padding.Empty};
    private readonly Label _state=new(){Margin=Padding.Empty,TextAlign=ContentAlignment.MiddleRight};
    private readonly Label _hint=Line(),_transfer=Line(),_progress=Line();
    private readonly Font _titleFont=SettingsWindow.SectionFont();
    private Tab5UpgradeStatusText? _last;
    private bool _arranging;
    internal Tab5UpgradeStatusView() {
        Name="upgrade-status";AutoSize=true;AutoSizeMode=AutoSizeMode.GrowAndShrink;
        Margin=new Padding(0,0,0,10);
        _version.Font=_titleFont;_version.Name="upgrade-version";_state.Name="upgrade-result";
        _hint.Name="upgrade-hint";_transfer.Name="upgrade-transfer";_progress.Name="upgrade-progress";
        Controls.AddRange([_version,_state,_hint,_transfer,_progress]);
    }
    internal void SetStatus(Tab5UpgradeStatusText value) {
        if(_last==value)return;_last=value;SuspendLayout();
        _version.Text=value.Title;_state.Text=value.State;
        _state.ForeColor=value.State=="已核验"?Color.FromArgb(24,121,78):SettingsWindow.Muted;
        Set(_hint,value.Hint);Set(_transfer,value.Transfer);Set(_progress,value.Progress);ResumeLayout(true);
        static void Set(Label label,string text){label.Text=text;label.Visible=text.Length>0;}
    }
    protected override void OnLayout(LayoutEventArgs e) {
        if(_arranging)return;_arranging=true;
        try {int height=Arrange(Math.Max(1,ClientSize.Width),true);if(Height!=height)Height=height;base.OnLayout(e);}
        finally{_arranging=false;}
    }
    public override Size GetPreferredSize(Size proposedSize) {
        int width=proposedSize.Width>0&&proposedSize.Width<int.MaxValue?proposedSize.Width:Math.Max(1,Width);
        return new(width,Arrange(width,false));
    }
    private int Arrange(int width,bool apply) {
        int gap=LogicalToDeviceUnits(6),stateWidth=Math.Min(width/2,TextRenderer.MeasureText(_state.Text,_state.Font).Width);
        int titleWidth=Math.Max(1,width-stateWidth-(stateWidth>0?gap*2:0));
        int height=Math.Max(Measure(_version,titleWidth),Measure(_state,Math.Max(1,stateWidth)));
        if(apply){_version.SetBounds(0,0,titleWidth,height);_state.SetBounds(width-stateWidth,0,stateWidth,height);}
        foreach(var label in new[]{_hint,_transfer,_progress}) {
            if(label.Text.Length==0)continue;
            int row=Measure(label,width);height+=gap;if(apply)label.SetBounds(0,height,width,row);height+=row;
        }
        return height;
    }
    private static int Measure(Label label,int width)=>TextRenderer.MeasureText(label.Text.Length>0?label.Text:" ",label.Font,
        new Size(width,int.MaxValue),TextFormatFlags.WordBreak|TextFormatFlags.TextBoxControl).Height;
    private static Label Line()=>new(){ForeColor=SettingsWindow.Muted,Margin=Padding.Empty};
    protected override void Dispose(bool disposing){base.Dispose(disposing);if(disposing)_titleFont.Dispose();}
}
