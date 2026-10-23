namespace AIBotBridge;

// Shared desktop rendering of the TAB5 reader chapters, with selectable complete text.
internal sealed class GalleryReaderTabs : TabControl
{
    internal Dictionary<string,TextBox> Bodies {get;}=[];
    internal GalleryReaderTabs(string prefix) {
        ImeMode=ImeMode.Disable;Name=prefix+"-reading";Dock=DockStyle.Fill;Margin=System.Windows.Forms.Padding.Empty;
        SizeMode=TabSizeMode.Fixed;ItemSize=new(128,48);DrawMode=TabDrawMode.OwnerDrawFixed;
        foreach(string name in new[]{"简介","欣赏提示","原文"}){
            var tab=new TabPage(name){Padding=new(24),BackColor=Color.FromArgb(250,249,246)};TabPages.Add(tab);
            var text=new TextBox{Name=prefix+"-reading-"+name,Dock=DockStyle.Fill,Multiline=true,ReadOnly=true,ImeMode=ImeMode.Disable,ScrollBars=ScrollBars.Vertical,BorderStyle=BorderStyle.None,
                BackColor=tab.BackColor,ForeColor=SettingsWindow.Ink,Font=new Font("Microsoft YaHei UI",11.5f)};
            tab.Controls.Add(text);Bodies[name]=text;
        }
    }
    // Exact key matches leave text selection and modified editing shortcuts alone.
    internal bool HandleReadingKey(Keys key) {
        bool alt=key==(Keys.Alt|Keys.Left)||key==(Keys.Alt|Keys.Right);
        var body=Bodies[TabPages[SelectedIndex].Text];
        if(alt||key is Keys.Left or Keys.Right){
            if(!alt&&body.Focused&&body.SelectionLength>0)return false;
            int direction=(key&Keys.KeyCode)==Keys.Left?-1:1;
            SelectedIndex=(SelectedIndex+direction+TabCount)%TabCount;
            Focus();return true;
        }
        int scroll=key switch{Keys.Up=>0,Keys.Down=>1,Keys.PageUp=>2,Keys.PageDown=>3,_=>-1};
        if(scroll<0)return false;
        SendMessage(body.Handle,0x115,(IntPtr)scroll,IntPtr.Zero);return true;
    }
    [System.Runtime.InteropServices.DllImport("user32.dll",EntryPoint="SendMessageW")]
    private static extern IntPtr SendMessage(IntPtr window,int message,IntPtr wParam,IntPtr lParam);
    protected override void OnDrawItem(DrawItemEventArgs e){
        bool selected=e.Index==SelectedIndex;var bounds=e.Bounds;
        using var background=new SolidBrush(selected?Color.FromArgb(224,233,234):Color.FromArgb(242,241,237));e.Graphics.FillRectangle(background,bounds);
        TextRenderer.DrawText(e.Graphics,TabPages[e.Index].Text,Font,bounds,selected?SettingsWindow.Ink:SettingsWindow.Muted,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.NoPrefix);
        if(selected){using var accent=new SolidBrush(Color.FromArgb(58,93,102));e.Graphics.FillRectangle(accent,bounds.Left+16,bounds.Bottom-3,bounds.Width-32,3);}
        if(Focused&&selected)ControlPaint.DrawFocusRectangle(e.Graphics,Rectangle.Inflate(bounds,-5,-5));
    }
    internal static void StyleArrow(Button button){
        button.AutoSize=false;button.MinimumSize=new(48,56);button.Size=new(48,56);button.Padding=System.Windows.Forms.Padding.Empty;button.Margin=System.Windows.Forms.Padding.Empty;
        button.Font=new Font("Microsoft YaHei UI",20);button.BackColor=Color.FromArgb(242,241,237);button.FlatAppearance.BorderSize=0;
    }
    internal static Control ImageStage(Control image,Control state,Button previous,Button next){
        var stage=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=3,RowCount=1,Margin=System.Windows.Forms.Padding.Empty,BackColor=image.BackColor};
        stage.ColumnStyles.Add(new(SizeType.Absolute,72));stage.ColumnStyles.Add(new(SizeType.Percent,100));stage.ColumnStyles.Add(new(SizeType.Absolute,72));stage.RowStyles.Add(new(SizeType.Percent,100));
        var center=new Panel{Dock=DockStyle.Fill,Margin=System.Windows.Forms.Padding.Empty};center.Controls.Add(image);center.Controls.Add(state);stage.Controls.Add(center,1,0);
        previous.Anchor=next.Anchor=AnchorStyles.None;stage.Controls.Add(previous,0,0);stage.Controls.Add(next,2,0);return stage;
    }
}

// Keep the original image control alive while opening the reading pane.
internal sealed class GalleryComparisonStage : TableLayoutPanel
{
    private readonly Control _picture,_reader;
    private bool _open;
    internal GalleryComparisonStage(Control picture,Control reader){
        _picture=picture;_reader=reader;Dock=DockStyle.Fill;Margin=Padding.Empty;
        Controls.Add(picture);Controls.Add(reader);Resize+=(_,_)=>Arrange();Arrange();
    }
    internal void ShowReading(bool open){_open=open;Arrange();}
    private void Arrange(){
        SuspendLayout();
        bool wide=ClientSize.Width>=900*DeviceDpi/96f;
        ColumnStyles.Clear();RowStyles.Clear();
        ColumnCount=_open&&wide?2:1;RowCount=_open&&!wide?2:1;
        ColumnStyles.Add(new(SizeType.Percent,100));
        if(_open&&wide)ColumnStyles.Add(new(SizeType.Absolute,Math.Min(ClientSize.Width*.38f,520*DeviceDpi/96f)));
        RowStyles.Add(new(SizeType.Percent,_open&&!wide?52:100));
        if(_open&&!wide)RowStyles.Add(new(SizeType.Percent,48));
        SetCellPosition(_picture,new(0,0));SetCellPosition(_reader,new(_open&&wide?1:0,_open&&!wide?1:0));
        _picture.Visible=true;_reader.Visible=_open;
        _reader.Margin=_open?new Padding(wide?16:0,wide?0:12,0,0):Padding.Empty;
        ResumeLayout(true);
    }
}
