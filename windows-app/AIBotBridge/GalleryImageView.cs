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
    internal int ImageRevision {get;private set;}
    internal event EventHandler? ViewChanged;
    internal Action<int>? NavigatePage {get;set;}
    internal bool PreviousPage {get;set;}
    internal bool NextPage {get;set;}
    private Rectangle ArrowBounds(bool next){int width=(int)(28*DeviceDpi/96f),height=(int)(44*DeviceDpi/96f),inset=(int)(6*DeviceDpi/96f);return new(next?ClientSize.Width-width-inset:inset,(ClientSize.Height-height)/2,width,height);}
    private int ArrowAt(Point point)=>NavigatePage is null||!PreviousPage&&!NextPage?0:ArrowBounds(false).Contains(point)?-1:ArrowBounds(true).Contains(point)?1:0;
    internal GalleryImageView() {
        SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.Selectable,true);
        // Viewing surfaces must deliver letter shortcuts before IME composition.
        ImeMode=ImeMode.Disable;TabStop=true;BackColor=SettingsWindow.Surface;Cursor=Cursors.Hand;
        AccessibleName="作品图片：滚轮缩放，拖动查看，双击全屏";
    }
    internal void SetImage(Bitmap? image){var previous=_image;_image=image;ImageRevision++;previous?.Dispose();ResetView();}
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
    protected override void OnPaint(PaintEventArgs e){
        base.OnPaint(e);if(_image is null)return;e.Graphics.InterpolationMode=InterpolationMode.HighQualityBicubic;e.Graphics.PixelOffsetMode=PixelOffsetMode.HighQuality;e.Graphics.DrawImage(_image,ImageBounds);
        if(NavigatePage is null||!PreviousPage&&!NextPage)return;
        foreach(bool next in new[]{false,true}){var bounds=ArrowBounds(next);using var background=new SolidBrush(Color.FromArgb(160,24,30,32));e.Graphics.FillRectangle(background,bounds);
            using var pen=new Pen(next?NextPage?Color.White:Color.Gray:PreviousPage?Color.White:Color.Gray,2*DeviceDpi/96f);
            float x=bounds.Left+bounds.Width/2f,y=bounds.Top+bounds.Height/2f,d=5*DeviceDpi/96f,sign=next?1:-1;
            e.Graphics.DrawLines(pen,[new PointF(x-sign*d/2,y-d),new PointF(x+sign*d/2,y),new PointF(x-sign*d/2,y+d)]);
        }
    }
    protected override void OnMouseWheel(MouseEventArgs e){base.OnMouseWheel(e);if(_image is null)return;Focus();ZoomAt(_zoom*(float)Math.Pow(1.2,e.Delta/120d),e.Location);}
    protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);if(e.Button!=MouseButtons.Left||_image is null)return;Focus();int direction=ArrowAt(e.Location);if(direction!=0){if(direction<0?PreviousPage:NextPage)NavigatePage?.Invoke(direction);return;}_drag=e.Location;_dragging=true;Capture=true;Cursor=Cursors.SizeAll;}
    protected override void OnMouseDoubleClick(MouseEventArgs e){if(ArrowAt(e.Location)==0)base.OnMouseDoubleClick(e);}
    protected override void OnMouseMove(MouseEventArgs e){base.OnMouseMove(e);if(!_dragging)return;_pan=new(_pan.X+e.X-_drag.X,_pan.Y+e.Y-_drag.Y);_drag=e.Location;ClampPan();Invalidate();}
    protected override void OnMouseUp(MouseEventArgs e){base.OnMouseUp(e);_dragging=false;Capture=false;Cursor=Cursors.Hand;}
    protected override void OnMouseCaptureChanged(EventArgs e){base.OnMouseCaptureChanged(e);if(!Capture){_dragging=false;Cursor=Cursors.Hand;}}
    protected override void OnResize(EventArgs e){base.OnResize(e);ClampPan();Invalidate();}
    protected override void Dispose(bool disposing){if(disposing){_image?.Dispose();_image=null;}base.Dispose(disposing);}
}

internal sealed class GalleryImageFullscreenForm : Form
{
    private readonly Action<int>? _changePage;
    private readonly Label _page=new(){Name="fullscreen-page",AutoSize=true,ForeColor=Color.White,Margin=new(8,7,16,0)};
    private readonly Label _state=new(){Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleCenter,ForeColor=Color.White,Visible=false};
    private readonly GalleryReaderTabs _reading=new("fullscreen"){Visible=false};
    private readonly Button _details=new(){Name="fullscreen-details",Text="作品详情 (I)"};
    private readonly Label _title=new(){AutoSize=true,Dock=DockStyle.Fill,ForeColor=SettingsWindow.Ink},_metadata=new(){AutoSize=true,Dock=DockStyle.Fill,ForeColor=SettingsWindow.Muted};
    private readonly Label _hint=new(){AutoSize=true,ForeColor=SettingsWindow.Muted,Margin=new(10,7,0,0)};
    private bool _readingOpen,_hasPrevious,_hasNext;
    private readonly Control _picture;
    private readonly GalleryComparisonStage _comparison;
    private readonly TableLayoutPanel _layout,_heading;
    internal GalleryImageView View {get;}=new(){Name="fullscreen-image",Dock=DockStyle.Fill,BackColor=Color.FromArgb(15,20,22)};
    internal GalleryImageFullscreenForm(Bitmap image,string title,Action<int>? changePage=null) {
        _changePage=changePage;
        Text=title;Font=SettingsWindow.BodyFont();FormBorderStyle=FormBorderStyle.None;WindowState=FormWindowState.Maximized;KeyPreview=true;BackColor=View.BackColor;
        var root=_layout=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=4,Margin=Padding.Empty,BackColor=Color.FromArgb(250,249,246)};Controls.Add(root);
        root.ColumnStyles.Add(new(SizeType.Percent,100));root.RowStyles.Add(new(SizeType.AutoSize));root.RowStyles.Add(new(SizeType.Percent,100));root.RowStyles.Add(new(SizeType.AutoSize));root.RowStyles.Add(new(SizeType.AutoSize));
        var heading=_heading=new TableLayoutPanel{Dock=DockStyle.Fill,AutoSize=false,ColumnCount=1,RowCount=2,Padding=new(16,4,16,4)};heading.ColumnStyles.Add(new(SizeType.Percent,100));heading.RowStyles.Add(new(SizeType.AutoSize));heading.RowStyles.Add(new(SizeType.AutoSize));heading.Controls.Add(_title,0,0);heading.Controls.Add(_metadata,0,1);root.Controls.Add(heading,0,0);_title.Text=title;
        var stage=new Panel{Dock=DockStyle.Fill,Margin=Padding.Empty};root.Controls.Add(stage,0,1);
        _picture=new Panel{Dock=DockStyle.Fill,Margin=Padding.Empty};_picture.Controls.Add(View);_picture.Controls.Add(_state);_comparison=new GalleryComparisonStage(_picture,_reading);stage.Controls.Add(_comparison);View.SetImage(image);View.NavigatePage=changePage;heading.Visible=true;
        var footer=new TableLayoutPanel{Dock=DockStyle.Fill,AutoSize=true,ColumnCount=2,RowCount=1,Padding=new(12,4,12,4),Margin=Padding.Empty};footer.ColumnStyles.Add(new(SizeType.Percent,100));footer.ColumnStyles.Add(new(SizeType.AutoSize));footer.RowStyles.Add(new(SizeType.AutoSize));root.Controls.Add(footer,0,3);
        var tools=new FlowLayoutPanel{Name="fullscreen-tools",Dock=DockStyle.Fill,AutoSize=true,WrapContents=false,Margin=Padding.Empty};footer.Controls.Add(tools,0,0);
        Resize+=(_,_)=>MeasureHeading();MeasureHeading();
        tools.Controls.Add(_page);_page.ForeColor=SettingsWindow.Muted;_page.Visible=changePage is not null;
        SettingsWindow.StyleButton(_details);_details.Click+=(_,_)=>ToggleReading();_details.Visible=changePage is not null;footer.Controls.Add(_details,1,0);
        var fit=new Button{Name="fullscreen-fit",Text="适合屏幕",Margin=new(0,0,10,0)};SettingsWindow.StyleButton(fit);fit.Click+=(_,_)=>View.ResetView();tools.Controls.Add(fit);
        var close=new Button{Name="fullscreen-close",Text="退出全屏 (Esc)",Margin=new(0,0,10,0)};SettingsWindow.StyleButton(close);close.Click+=(_,_)=>Close();tools.Controls.Add(close);
        _hint.Text=changePage is null?"滚轮缩放":"← → 翻页";tools.Controls.Add(_hint);
        View.MouseDoubleClick+=(_,_)=>Close();KeyDown+=(_,e)=>{if(e.KeyCode==Keys.Escape){e.Handled=true;if(_readingOpen)ToggleReading();else Close();}};Shown+=(_,_)=>View.Focus();
    }
    private void MeasureHeading(){
        int width=Math.Max(100,ClientSize.Width-_heading.Padding.Horizontal-_title.Margin.Horizontal);
        _title.MaximumSize=_metadata.MaximumSize=new(width,0);
        float height=_heading.Padding.Vertical+_title.Margin.Vertical+_metadata.Margin.Vertical+_title.GetPreferredSize(new(width,0)).Height+_metadata.GetPreferredSize(new(width,0)).Height;
        _layout.RowStyles[0].SizeType=SizeType.Absolute;_layout.RowStyles[0].Height=height;
    }
    private void ToggleReading(){_readingOpen=!_readingOpen;_layout.SetRow(_heading,0);_heading.Visible=true;_comparison.ShowReading(_readingOpen);if(_readingOpen){_reading.BringToFront();_reading.Focus();}else View.Focus();_hint.Text=_readingOpen?"← → 切换栏目":"← → 翻页";_details.Text=_readingOpen?"返回作品 (I)":"作品详情 (I)";UpdateArrows();}
    private void UpdateArrows(){if(View.PreviousPage==_hasPrevious&&View.NextPage==_hasNext)return;View.PreviousPage=_hasPrevious;View.NextPage=_hasNext;View.Invalidate();}
    internal void SetPage(Bitmap? image,bool replaceImage,string message,string page,bool previous,bool next,string title,string metadata,Dictionary<string,string> reading){
        if(replaceImage)View.SetImage(image);_state.Text=message;_state.Visible=!View.HasImage;_page.Text=page;_hasPrevious=previous;_hasNext=next;UpdateArrows();
        _title.Text=title;_metadata.Text=metadata;MeasureHeading();_details.Enabled=_readingOpen||View.HasImage;
        foreach(var pair in reading)if(_reading.Bodies[pair.Key].Text!=pair.Value)_reading.Bodies[pair.Key].Text=pair.Value;
    }
    protected override bool ProcessCmdKey(ref Message msg,Keys keyData){
        if(keyData==Keys.I&&_changePage is not null&&_details.Enabled){ToggleReading();return true;}
        if(_readingOpen&&_reading.HandleReadingKey(keyData))return true;
        if(!_readingOpen&&keyData is Keys.Left or Keys.Right&&_changePage is not null){_changePage(keyData==Keys.Left?-1:1);return true;}
        if(keyData==Keys.Escape){if(_readingOpen)ToggleReading();else Close();return true;}
        return base.ProcessCmdKey(ref msg,keyData);
    }
}

internal sealed class GalleryWorkList : ListBox
{
    internal GalleryWorkList(){ImeMode=ImeMode.Disable;}
    internal event EventHandler? ViewportChanged;
    protected override void WndProc(ref Message m){base.WndProc(ref m);if(m.Msg is 0x115 or 0x20A)ViewportChanged?.Invoke(this,EventArgs.Empty);}
}
