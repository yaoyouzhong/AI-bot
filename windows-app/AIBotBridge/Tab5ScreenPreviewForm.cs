using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace AIBotBridge;

internal sealed class Tab5ScreenPreviewForm: Form
{
    private readonly Tab5Service _service;
    private readonly Func<bool> _valid;
    private readonly PictureBox _image=new(){Dock=DockStyle.Fill,SizeMode=PictureBoxSizeMode.Zoom,BackColor=Color.Black};
    private readonly Label _status=new(){Dock=DockStyle.Bottom,Height=44,TextAlign=ContentAlignment.MiddleLeft};
    private readonly System.Windows.Forms.Timer _timer=new(){Interval=1000};
    private string _token="";
    private long _shown=long.MinValue;
    private byte[]? _shownPixels;
    private int _shownWidth,_shownHeight;
    internal Tab5ScreenPreviewForm(Tab5Service service,Func<bool> valid) {
        _service=service;_valid=valid;Text="设备预览";ClientSize=new(840,540);MinimumSize=new(640,420);
        Controls.Add(_image);Controls.Add(_status);_timer.Tick+=(_,_)=>RefreshFrame();
        Shown+=(_,_)=>{_timer.Start();RefreshFrame();};
        SettingsWindow.FitScreen(this);
    }
    internal void Restart(){if(!Visible||WindowState==FormWindowState.Minimized)StopPreview();}
    private void ClearImage(){var old=_image.Image;_image.Image=null;old?.Dispose();_shown=long.MinValue;_shownPixels=null;}
    private void StopPreview(){_service.ScreenPreview.Stop(_token);_token="";ClearImage();}
    private void RefreshFrame() {
        if(!Visible||WindowState==FormWindowState.Minimized){if(_token.Length>0)StopPreview();return;}
        if(!_valid()){StopPreview();_status.Text="设备已移除或停用。";return;}
        var context=_service.ScreenPreviewContext;
        _timer.Interval=context?.Responsive==true&&(context.Transports.Contains("USB")||context.Transports.Contains("Wi-Fi"))?100:1000;
        if(_token.Length==0)_token=_service.ScreenPreview.Start(_service.ScreenPreviewContext);
        _service.ScreenPreview.Renew(_token,_service.ScreenPreviewContext);
        var snapshot=_service.ScreenPreview.Snapshot(_token);if(_status.Text!=snapshot.Note)_status.Text=snapshot.Note;
        if(snapshot.Frame is not {} frame){ClearImage();return;}
        if(frame.Received==_shown)return;
        if(_shownWidth==frame.Width&&_shownHeight==frame.Height&&_shownPixels is not null&&_shownPixels.AsSpan().SequenceEqual(frame.Pixels)){_shown=frame.Received;return;}
        _shownPixels=frame.Pixels;_shownWidth=frame.Width;_shownHeight=frame.Height;
        var image=Render(frame);var old=_image.Image;_image.Image=image;old?.Dispose();_shown=frame.Received;
    }
    protected override void OnFormClosed(FormClosedEventArgs e){_timer.Stop();StopPreview();base.OnFormClosed(e);}
    protected override void OnVisibleChanged(EventArgs e){base.OnVisibleChanged(e);if(!Visible&&_token.Length>0)StopPreview();}
    protected override void Dispose(bool disposing){if(disposing){_timer.Dispose();StopPreview();}base.Dispose(disposing);}
    internal static Bitmap Render(Tab5ScreenPreview.Frame frame) {
        if(frame.Pixels.Length!=frame.Width*frame.Height*2)throw new ArgumentException("Invalid preview pixels");
        var image=new Bitmap(frame.Width,frame.Height,PixelFormat.Format32bppArgb);
        var locked=image.LockBits(new(0,0,image.Width,image.Height),ImageLockMode.WriteOnly,PixelFormat.Format32bppArgb);
        try {
            byte[] pixels=new byte[locked.Stride*frame.Height];
            for(int y=0;y<frame.Height;y++)for(int x=0;x<frame.Width;x++) {
                int at=(y*frame.Width+x)*2,value=frame.Pixels[at]|frame.Pixels[at+1]<<8,outAt=y*locked.Stride+x*4;
                int r=(value>>11)&31,g=(value>>5)&63,b=value&31;
                pixels[outAt]=(byte)((b<<3)|(b>>2));pixels[outAt+1]=(byte)((g<<2)|(g>>4));pixels[outAt+2]=(byte)((r<<3)|(r>>2));pixels[outAt+3]=255;
            }
            Marshal.Copy(pixels,0,locked.Scan0,pixels.Length);
        } finally{image.UnlockBits(locked);}
        return image;
    }
}
