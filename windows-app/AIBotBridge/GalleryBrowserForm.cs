namespace AIBotBridge;

// Local, read-only collection browsing. No device commands or date overrides.
internal sealed class GalleryBrowserForm : Form
{
    private readonly Tab5Gallery _gallery;
    private readonly ComboBox _category=new(){Name="browse-category",DropDownStyle=ComboBoxStyle.DropDownList,Width=136};
    private readonly DateTimePicker _date=new(){Name="browse-date",Format=DateTimePickerFormat.Custom,CustomFormat="yyyy-MM-dd",Width=132};
    private readonly TextBox _search=new(){Name="browse-search",PlaceholderText="作品名或作者",Width=188};
    private readonly GalleryWorkList _list=new(){Name="browse-works",Dock=DockStyle.Fill,BorderStyle=BorderStyle.None,DrawMode=DrawMode.OwnerDrawFixed,IntegralHeight=false};
    private readonly Label _summary=new(){Name="browse-summary",AutoSize=true,Dock=DockStyle.Fill,ForeColor=SettingsWindow.Muted};
    private readonly Label _title=new(){Name="browse-title",AutoSize=true,Dock=DockStyle.Fill,Font=SettingsWindow.SectionFont()};
    private readonly Label _metadata=new(){Name="browse-metadata",AutoSize=true,Dock=DockStyle.Fill,ForeColor=SettingsWindow.Muted};
    private readonly Label _imageState=new(){Name="browse-image-state",Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleCenter,ForeColor=SettingsWindow.Muted};
    private readonly Label _pageCount=new(){Name="browse-page-count",AutoSize=true,Margin=new(8,7,8,0)};
    private readonly GalleryImageView _image=new(){Name="browse-image",Dock=DockStyle.Fill};
    private readonly Button _previousWork,_nextWork,_previousPage,_nextPage,_findDate,_fit,_fullscreen,_readerToggle;
    private readonly Dictionary<string,TextBox> _reading=[];
    private readonly Dictionary<int,Bitmap> _thumbnails=[];
    private readonly HashSet<int> _thumbnailFailures=[];
    private CancellationTokenSource? _catalogStop,_thumbnailStop,_detailStop;
    private Tab5Gallery.BrowserCatalog? _catalog;
    private int[] _filtered=[];
    private int _selected=-1,_frame,_pending;
    private bool _changing,_closed,_thumbnailsQueued,_readingOpen;
    internal int PendingLoads=>_pending;
    internal string? SelectedWorkId=>_catalog is not null&&_selected>=0?_catalog.Works[_selected].Id:null;

    internal GalleryBrowserForm(Tab5Gallery? gallery=null,DateOnly? today=null) {
        _gallery=gallery??new();Text="AI-bot · 浏览作品";Font=SettingsWindow.BodyFont();
        AutoScaleDimensions=new(96,96);AutoScaleMode=AutoScaleMode.Dpi;ClientSize=new(1160,780);MinimumSize=new(960,680);
        _date.Value=(today??DateOnly.FromDateTime(DateTime.Today)).ToDateTime(TimeOnly.MinValue);
        _category.Items.AddRange(["每日名画","每日书法"]);_category.SelectedIndex=0;
        var root=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new(20),ColumnCount=1,RowCount=3};Controls.Add(root);
        root.ColumnStyles.Add(new(SizeType.Percent,100));
        root.RowStyles.Add(new(SizeType.AutoSize));root.RowStyles.Add(new(SizeType.AutoSize));root.RowStyles.Add(new(SizeType.Percent,100));
        _summary.Margin=new(0,0,0,12);root.Controls.Add(_summary,0,0);
        var filters=Flow();filters.Margin=new(0,0,0,16);root.Controls.Add(filters,0,1);
        filters.Controls.AddRange([_category,_date]);
        _findDate=Action("按日期查找","browse-find-date",()=>FindDate());filters.Controls.Add(_findDate);
        filters.Controls.Add(Action("今日","browse-today",()=>{_date.Value=DateTime.Today;FindDate();}));
        filters.Controls.Add(Caption("搜索"));_search.AccessibleName="作品名或作者";
        filters.Controls.Add(_search);filters.Controls.Add(Action("刷新","browse-refresh",async()=>await ReloadAsync()));
        var content=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,Margin=Padding.Empty};
        content.ColumnStyles.Add(new(SizeType.Percent,30));content.ColumnStyles.Add(new(SizeType.Percent,70));content.RowStyles.Add(new(SizeType.Percent,100));root.Controls.Add(content,0,2);
        var directory=new Panel{Dock=DockStyle.Fill,Margin=new(0,0,20,0),Padding=new(1),BackColor=SettingsWindow.Border};content.Controls.Add(directory,0,0);
        _list.BackColor=Color.White;_list.Margin=Padding.Empty;directory.Controls.Add(_list);
        var details=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=6,Margin=Padding.Empty};content.Controls.Add(details,1,0);
        details.ColumnStyles.Add(new(SizeType.Percent,100));
        details.RowStyles.Add(new(SizeType.AutoSize));details.RowStyles.Add(new(SizeType.AutoSize));details.RowStyles.Add(new(SizeType.Percent,100));details.RowStyles.Add(new(SizeType.AutoSize));details.RowStyles.Add(new(SizeType.Absolute,0));details.RowStyles.Add(new(SizeType.AutoSize));
        _title.Margin=new(0,0,0,6);_metadata.Margin=new(0,0,0,12);details.Controls.Add(_title,0,0);details.Controls.Add(_metadata,0,1);
        var imagePanel=new Panel{Dock=DockStyle.Fill,BackColor=SettingsWindow.Surface,Margin=Padding.Empty};imagePanel.Controls.Add(_image);imagePanel.Controls.Add(_imageState);details.Controls.Add(imagePanel,0,2);
        var frame=Flow();frame.Margin=new(0,8,0,0);details.Controls.Add(frame,0,3);
        _previousPage=Action("上一页","browse-previous-page",()=>ChangeFrame(-1));_nextPage=Action("下一页","browse-next-page",()=>ChangeFrame(1));frame.Controls.AddRange([_previousPage,_pageCount,_nextPage]);
        var reading=new TabControl{Name="browse-reading",Dock=DockStyle.Fill,Margin=new(0,12,0,0),Visible=false};details.Controls.Add(reading,0,4);
        _readerToggle=Action("阅读资料","browse-toggle-reading",()=>{_readingOpen=!_readingOpen;reading.Visible=_readingOpen;details.RowStyles[2]=new(SizeType.Percent,_readingOpen?65:100);details.RowStyles[4]=_readingOpen?new(SizeType.Percent,35):new(SizeType.Absolute,0);});
        _readerToggle.Click+=(_,_)=>_readerToggle.Text=_readingOpen?"收起资料":"阅读资料";
        _fit=Action("适合窗口","browse-fit",()=>_image.ResetView());_fullscreen=Action("全屏观看","browse-fullscreen",()=>ShowFullscreen());
        frame.Controls.AddRange([_readerToggle,_fit,_fullscreen]);_image.MouseDoubleClick+=(_,_)=>ShowFullscreen();
        foreach(string name in new[]{"简介","欣赏提示","原文"}) {
            var tab=new TabPage(name){Padding=new(12),BackColor=Color.White};reading.TabPages.Add(tab);
            var text=new TextBox{Name="browse-reading-"+name,Dock=DockStyle.Fill,Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Vertical,BorderStyle=BorderStyle.None,BackColor=Color.White,ForeColor=SettingsWindow.Ink};tab.Controls.Add(text);_reading[name]=text;
        }
        var navigate=Flow();navigate.Margin=new(0,12,0,0);details.Controls.Add(navigate,0,5);
        _previousWork=Action("上一件","browse-previous-work",()=>ChangeWork(-1));_nextWork=Action("下一件","browse-next-work",()=>ChangeWork(1));navigate.Controls.AddRange([_previousWork,_nextWork]);
        _list.DrawItem+=DrawWork;_list.ViewportChanged+=(_,_)=>QueueThumbnails();_list.SelectedIndexChanged+=async(_,_)=>{if(!_changing&&_list.SelectedItem is int selected){_selected=selected;_frame=0;UpdateNavigation();QueueThumbnails();await LoadDetailAsync();}};
        _category.SelectedIndexChanged+=async(_,_)=>await ReloadAsync();_search.TextChanged+=(_,_)=>{if(!_changing)ApplyFilter();};
        void Fit(){int width=Math.Max(80,details.ClientSize.Width-details.Padding.Horizontal);_title.MaximumSize=_metadata.MaximumSize=new(width,0);_summary.MaximumSize=new(Math.Max(100,root.ClientSize.Width-root.Padding.Horizontal),0);_summary.MinimumSize=new(0,(int)Math.Ceiling(_summary.Font.GetHeight()+4));_list.ItemHeight=(int)Math.Ceiling(Font.GetHeight()*4.8);}
        FontChanged+=(_,_)=>Fit();
        details.SizeChanged+=(_,_)=>Fit();root.SizeChanged+=(_,_)=>Fit();Shown+=async(_,_)=>{Fit();await ReloadAsync();};
        ClearDetail("正在读取图库…");UpdateNavigation();SettingsWindow.FitScreen(this);
    }
    private static FlowLayoutPanel Flow()=>new(){AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,Dock=DockStyle.Fill,WrapContents=true,Margin=Padding.Empty};
    private static Label Caption(string text)=>new(){Text=text,AutoSize=true,Margin=new(0,6,6,0)};
    private static Button Action(string text,string name,System.Action click){var b=new Button{Name=name,Text=text,Margin=new(0,0,8,0)};SettingsWindow.StyleButton(b);b.Click+=(_,_)=>click();return b;}
    private string Category=>_category.SelectedIndex==1?"calligraphy":"painting";
    private CancellationToken Replace(ref CancellationTokenSource? source){source?.Cancel();source?.Dispose();source=new();return source.Token;}
    private async Task ReloadAsync() {
        var token=Replace(ref _catalogStop);_thumbnailStop?.Cancel();_detailStop?.Cancel();string category=Category;
        string? previous=_catalog?.Category==category?SelectedWorkId:null;_catalog=null;_filtered=[];ClearThumbnails();_list.Items.Clear();_selected=-1;
        ClearDetail("正在读取图库…");_summary.Text="正在读取已安装的图库…";UpdateNavigation();Interlocked.Increment(ref _pending);
        try {
            var result=await Task.Run(()=>_gallery.Browse(category),token);
            if(_closed||token.IsCancellationRequested)return;_catalog=result;
            _summary.Text=$"{GalleryPack.CategoryName(category)} · {result.Works.Length} 件"+(result.Version is null?"":$" · {result.Version}");
            int selected=previous is null?-1:Array.FindIndex(result.Works,w=>w.Id==previous);
            if(selected<0&&result.Works.Length>0)selected=Tab5Gallery.DayIndex(DateOnly.FromDateTime(_date.Value),result.Works.Length);
            ApplyFilter(selected);
        }catch(OperationCanceledException){}
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or ArgumentException) {
            if(!_closed&&!token.IsCancellationRequested){_summary.Text="图库读取失败："+ex.Message;ClearDetail("请返回屏保图库重新导入，再点击刷新。");}
        }finally{Interlocked.Decrement(ref _pending);}
    }
    private void ApplyFilter(int? preferred=null) {
        if(_catalog is null)return;
        _detailStop?.Cancel();
        string search=_search.Text.Trim();_filtered=Enumerable.Range(0,_catalog.Works.Length).Where(i=>search.Length==0||(_catalog.Works[i].Title??"").Contains(search,StringComparison.OrdinalIgnoreCase)||(_catalog.Works[i].Author??"").Contains(search,StringComparison.OrdinalIgnoreCase)).ToArray();
        _selected=preferred is int p&&_filtered.Contains(p)?p:_filtered.FirstOrDefault(-1);
        RenderList();
        if(_selected<0)ClearDetail(_catalog.Works.Length==0?"尚未安装这类图库，请返回屏保图库导入。":"没有匹配的作品，请更换搜索词。");
        UpdateNavigation();if(_selected>=0){_frame=0;_ = LoadDetailAsync();}
    }
    private void FindDate() {
        if(_catalog is null||_catalog.Works.Length==0)return;
        _changing=true;_search.Text="";_changing=false;
        ApplyFilter(Tab5Gallery.DayIndex(DateOnly.FromDateTime(_date.Value),_catalog.Works.Length));
    }
    private void ChangeWork(int direction) {
        int position=Array.IndexOf(_filtered,_selected)+direction;if(position<0||position>=_filtered.Length)return;
        _selected=_filtered[position];_changing=true;_list.SelectedItem=_selected;_changing=false;_frame=0;QueueThumbnails();UpdateNavigation();_ = LoadDetailAsync();
    }
    private void ChangeFrame(int direction){if(_catalog is null||_selected<0)return;int frame=_frame+direction;if(frame<0||frame>=_catalog.Works[_selected].Frames.Length)return;_frame=frame;UpdateNavigation();_ = LoadDetailAsync();}
    private void RenderList() {
        _thumbnailStop?.Cancel();ClearThumbnails();_changing=true;_list.BeginUpdate();
        try{_list.Items.Clear();foreach(int index in _filtered)_list.Items.Add(index);_list.SelectedItem=_selected;}
        finally{_list.EndUpdate();_changing=false;}
        QueueThumbnails();
    }
    private void QueueThumbnails() {
        if(_closed||_thumbnailsQueued||!IsHandleCreated||_catalog is null)return;_thumbnailsQueued=true;
        BeginInvoke((Action)(()=>{_thumbnailsQueued=false;if(!_closed)_ = LoadThumbnailsAsync();}));
    }
    private async Task LoadThumbnailsAsync() {
        if(_catalog is null)return;var catalog=_catalog;int[] indices=_filtered.Skip(Math.Max(0,_list.TopIndex-4)).Take(32).ToArray();
        var known=_thumbnails.Keys.Concat(_thumbnailFailures).ToHashSet();
        if(indices.All(known.Contains))return;
        var token=Replace(ref _thumbnailStop);
        var thumbnails=new Dictionary<int,Bitmap>();var failures=new HashSet<int>();Interlocked.Increment(ref _pending);
        try {
            await Task.Run(()=>{
                foreach(int index in indices) {
                    if(known.Contains(index))continue;
                    token.ThrowIfCancellationRequested();
                    try{var page=_gallery.BrowsePage(catalog.Category,catalog.Works[index].Id,0);thumbnails[index]=Decode(page.Jpeg,new(112,63));}
                    catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or ArgumentException){failures.Add(index);}
                }
            },token);
            if(_closed||token.IsCancellationRequested)return;
            foreach(int index in _thumbnails.Keys.Where(i=>!indices.Contains(i)).ToArray()){_thumbnails[index].Dispose();_thumbnails.Remove(index);}
            _thumbnailFailures.RemoveWhere(i=>!indices.Contains(i));_thumbnailFailures.UnionWith(failures);
            foreach(var pair in thumbnails)_thumbnails[pair.Key]=pair.Value;thumbnails.Clear();_list.Invalidate();
        }catch(OperationCanceledException){}
        finally{foreach(var bitmap in thumbnails.Values)bitmap.Dispose();Interlocked.Decrement(ref _pending);}
    }
    private async Task LoadDetailAsync() {
        var token=Replace(ref _detailStop);if(_catalog is null||_selected<0)return;
        var work=_catalog.Works[_selected];string category=_catalog.Category;int frame=_frame;
        ClearDetail("正在读取作品…");_title.Text=work.Title;_metadata.Text=work.Author;UpdateNavigation();Interlocked.Increment(ref _pending);
        Bitmap? bitmap=null;
        try {
            var page=await Task.Run(()=>_gallery.BrowsePage(category,work.Id,frame),token);token.ThrowIfCancellationRequested();
            bitmap=await Task.Run(()=>Decode(page.Jpeg),token);
            if(_closed||token.IsCancellationRequested)return;
            var guide=page.Guide;_title.Text=string.IsNullOrWhiteSpace(guide?.Title)?page.Work.Title:guide.Title;
            _metadata.Text=string.Join(" · ",new[]{guide?.Author??page.Work.Author,guide?.Period,guide?.Museum}.Where(s=>!string.IsNullOrWhiteSpace(s)));
            _image.SetImage(bitmap);bitmap=null;_image.Visible=true;_imageState.Visible=false;UpdateNavigation();
            _reading["简介"].Text=guide is null?"此图库尚未提供作品简介。":string.Join(Environment.NewLine+Environment.NewLine,new[]{guide.Brief,guide.Overview}.Where(s=>!string.IsNullOrWhiteSpace(s)));
            _reading["欣赏提示"].Text=string.IsNullOrWhiteSpace(guide?.Looking)?"此作品暂无已核实的欣赏提示。":guide.Looking;
            _reading["原文"].Text=string.IsNullOrWhiteSpace(guide?.Transcription)?"此作品暂无已核实原文。":guide.Transcription+(string.IsNullOrWhiteSpace(guide.Explanation)?"":Environment.NewLine+Environment.NewLine+guide.Explanation);
        }catch(OperationCanceledException){}
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or ArgumentException) {
            if(!_closed&&!token.IsCancellationRequested)ClearDetail("作品读取失败："+ex.Message+Environment.NewLine+"请刷新目录；缺少文件时重新导入图库。");
        }finally{bitmap?.Dispose();Interlocked.Decrement(ref _pending);}
    }
    private static Bitmap Decode(byte[] jpeg,Size? size=null) {
        using var stream=new MemoryStream(jpeg,false);using var image=System.Drawing.Image.FromStream(stream);
        if(image.Width!=1280||image.Height!=720)throw new InvalidDataException("图库图片尺寸不兼容。");
        return size is Size target?new Bitmap(image,target):new Bitmap(image);
    }
    private void ClearDetail(string message) {
        _image.SetImage(null);_image.Visible=false;_imageState.Text=message;_imageState.Visible=true;
        _title.Text="";_metadata.Text="";foreach(var text in _reading.Values)text.Text="";
    }
    private void UpdateNavigation() {
        int position=Array.IndexOf(_filtered,_selected);int frames=_catalog is not null&&_selected>=0?_catalog.Works[_selected].Frames.Length:0;
        _previousWork.Enabled=position>0;_nextWork.Enabled=position>=0&&position+1<_filtered.Length;
        _previousPage.Enabled=_frame>0&&frames>0;_nextPage.Enabled=frames>0&&_frame+1<frames;
        _previousPage.Visible=_nextPage.Visible=_pageCount.Visible=frames>1;
        _readerToggle.Enabled=_fit.Enabled=_fullscreen.Enabled=_image.HasImage;
        _findDate.Enabled=_catalog is not null&&_catalog.Works.Length>0;_pageCount.Text=frames==0?"":$"{_frame+1} / {frames} 页";
    }
    private void DrawWork(object? sender,DrawItemEventArgs e) {
        if(e.Index<0||_catalog is null)return;int index=(int)_list.Items[e.Index];var work=_catalog.Works[index];bool selected=(e.State&DrawItemState.Selected)!=0;
        if(!_thumbnails.ContainsKey(index)&&!_thumbnailFailures.Contains(index))QueueThumbnails();
        using var brush=new SolidBrush(selected?Color.FromArgb(233,242,255):Color.White);e.Graphics.FillRectangle(brush,e.Bounds);
        float scale=e.Bounds.Height/90f;int x=e.Bounds.X+(int)(8*scale),width=Math.Min((int)(112*scale),(int)(_list.ClientSize.Width*.34)),height=width*9/16;
        int y=e.Bounds.Y+(e.Bounds.Height-height)/2,textY=e.Bounds.Y+(int)(13*scale);
        if(_thumbnails.TryGetValue(index,out var thumbnail))e.Graphics.DrawImage(thumbnail,new Rectangle(x,y,width,height));
        else {using var placeholder=new SolidBrush(SettingsWindow.Surface);e.Graphics.FillRectangle(placeholder,x,y,width,height);}
        int textX=x+width+(int)(12*scale),available=Math.Max(12,e.Bounds.Right-textX-(int)(8*scale));
        TextRenderer.DrawText(e.Graphics,work.Title,_list.Font,new Rectangle(textX,textY,available,(int)(42*scale)),SettingsWindow.Ink,TextFormatFlags.WordBreak|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix);
        TextRenderer.DrawText(e.Graphics,work.Author,_list.Font,new Rectangle(textX,textY+(int)(44*scale),available,(int)(20*scale)),SettingsWindow.Muted,TextFormatFlags.SingleLine|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix);
        e.DrawFocusRectangle();
    }
    private void ClearThumbnails(){foreach(var thumbnail in _thumbnails.Values)thumbnail.Dispose();_thumbnails.Clear();_thumbnailFailures.Clear();}
    private void ShowFullscreen(){if(_image.CopyImage() is not Bitmap image)return;using var form=new GalleryImageFullscreenForm(image,_title.Text);form.ShowDialog(this);}
    protected override void Dispose(bool disposing) {
        if(disposing&&!_closed){_closed=true;foreach(var stop in new[]{_catalogStop,_thumbnailStop,_detailStop}){stop?.Cancel();stop?.Dispose();}ClearThumbnails();}
        base.Dispose(disposing);
    }
}
