using System.Drawing.Drawing2D;
namespace AIBotBridge;

internal class GalleryImageView : Control
{
    private Bitmap? _image;
    private float _zoom=1;
    private PointF _pan;
    private Point _drag;
    private bool _dragging;
    internal float Zoom=>_zoom;
    internal bool HasImage=>_image is not null;
    internal event EventHandler? ViewChanged;
    internal GalleryImageView() {
        SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.Selectable,true);
        TabStop=true;BackColor=SettingsWindow.Surface;Cursor=Cursors.Hand;
        AccessibleName="作品图片：滚轮缩放，拖动查看，双击全屏";
    }
    internal void SetImage(Bitmap? image){var previous=_image;_image=image;previous?.Dispose();ResetView();}
    internal Bitmap? CopyImage()=>_image is null?null:new Bitmap(_image);
    internal RectangleF ImageBounds {
        get {
            if(_image is null||ClientSize.Width==0||ClientSize.Height==0)return RectangleF.Empty;
            float scale=Math.Min((float)ClientSize.Width/_image.Width,(float)ClientSize.Height/_image.Height)*_zoom;
            float width=_image.Width*scale,height=_image.Height*scale;
            return new((ClientSize.Width-width)/2+_pan.X,(ClientSize.Height-height)/2+_pan.Y,width,height);
        }
    }
    internal void ResetView(){_zoom=1;_pan=PointF.Empty;Invalidate();ViewChanged?.Invoke(this,EventArgs.Empty);}
    internal void ZoomAt(float zoom,Point anchor) {
        var before=ImageBounds;if(before.Width<=0)return;zoom=Math.Clamp(zoom,1,16);
        float fractionX=(anchor.X-before.X)/before.Width,fractionY=(anchor.Y-before.Y)/before.Height;
        _zoom=zoom;_pan=PointF.Empty;var after=ImageBounds;
        _pan=new(anchor.X-after.X-fractionX*after.Width,anchor.Y-after.Y-fractionY*after.Height);
        ClampPan();Invalidate();ViewChanged?.Invoke(this,EventArgs.Empty);
    }
    private void ClampPan(){var rectangle=ImageBounds;float maxX=Math.Max(0,(rectangle.Width-ClientSize.Width)/2),maxY=Math.Max(0,(rectangle.Height-ClientSize.Height)/2);_pan=new(Math.Clamp(_pan.X,-maxX,maxX),Math.Clamp(_pan.Y,-maxY,maxY));}
    protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);if(_image is null)return;e.Graphics.InterpolationMode=InterpolationMode.HighQualityBicubic;e.Graphics.PixelOffsetMode=PixelOffsetMode.HighQuality;e.Graphics.DrawImage(_image,ImageBounds);}
    protected override void OnMouseWheel(MouseEventArgs e){base.OnMouseWheel(e);if(_image is null)return;Focus();ZoomAt(_zoom*(float)Math.Pow(1.2,e.Delta/120d),e.Location);}
    protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);if(e.Button!=MouseButtons.Left||_image is null)return;Focus();_drag=e.Location;_dragging=true;Capture=true;Cursor=Cursors.SizeAll;}
    protected override void OnMouseMove(MouseEventArgs e){base.OnMouseMove(e);if(!_dragging)return;_pan=new(_pan.X+e.X-_drag.X,_pan.Y+e.Y-_drag.Y);_drag=e.Location;ClampPan();Invalidate();}
    protected override void OnMouseUp(MouseEventArgs e){base.OnMouseUp(e);_dragging=false;Capture=false;Cursor=Cursors.Hand;}
    protected override void OnMouseCaptureChanged(EventArgs e){base.OnMouseCaptureChanged(e);if(!Capture){_dragging=false;Cursor=Cursors.Hand;}}
    protected override void OnResize(EventArgs e){base.OnResize(e);ClampPan();Invalidate();}
    protected override void Dispose(bool disposing){if(disposing){_image?.Dispose();_image=null;}base.Dispose(disposing);}
}

internal sealed class GalleryImageFullscreenForm : Form
{
    internal GalleryImageView View {get;}=new(){Name="fullscreen-image",Dock=DockStyle.Fill,BackColor=Color.FromArgb(15,20,22)};
    internal GalleryImageFullscreenForm(Bitmap image,string title) {
        Text=title;Font=SettingsWindow.BodyFont();FormBorderStyle=FormBorderStyle.None;WindowState=FormWindowState.Maximized;KeyPreview=true;BackColor=View.BackColor;
        Controls.Add(View);View.SetImage(image);
        var tools=new FlowLayoutPanel{Name="fullscreen-tools",Dock=DockStyle.Top,AutoSize=true,Padding=new(12,8,12,8),BackColor=Color.FromArgb(31,40,46)};Controls.Add(tools);
        var fit=new Button{Name="fullscreen-fit",Text="适合屏幕",Margin=new(0,0,10,0)};SettingsWindow.StyleButton(fit);fit.Click+=(_,_)=>View.ResetView();tools.Controls.Add(fit);
        var close=new Button{Name="fullscreen-close",Text="退出全屏 (Esc)",Margin=new(0,0,10,0)};SettingsWindow.StyleButton(close);close.Click+=(_,_)=>Close();tools.Controls.Add(close);
        var hint=new Label{Text="滚轮缩放 · 拖动查看",AutoSize=true,ForeColor=Color.FromArgb(193,207,215),Margin=new(10,7,0,0)};tools.Controls.Add(hint);
        View.MouseDoubleClick+=(_,_)=>Close();KeyDown+=(_,e)=>{if(e.KeyCode==Keys.Escape){e.Handled=true;Close();}};Shown+=(_,_)=>View.Focus();
    }
}

internal sealed class GalleryWorkList : ListBox
{
    internal event EventHandler? ViewportChanged;
    protected override void WndProc(ref Message m){base.WndProc(ref m);if(m.Msg is 0x115 or 0x20A)ViewportChanged?.Invoke(this,EventArgs.Empty);}
}
