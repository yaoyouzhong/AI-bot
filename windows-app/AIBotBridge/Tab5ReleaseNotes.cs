namespace AIBotBridge;

internal sealed class Tab5ReleaseNotes : FlowLayoutPanel
{
    private string? _notes;
    private bool _arranging;
    private readonly Font _heading=new("Microsoft YaHei UI",10F,FontStyle.Bold);
    internal Tab5ReleaseNotes() {
        AutoSize=false;AutoScroll=true;WrapContents=false;
        FlowDirection=FlowDirection.TopDown;Margin=new Padding(0,8,0,10);
        Padding=new Padding(12,8,8,8);BackColor=Color.FromArgb(246,248,250);
        SettingsWindow.FitFlow(this);
    }
    internal static (bool Heading,string Text)[] Parse(string notes)=>notes.Replace("\r\n","\n").Split('\n')
        .Select(s=>s.Trim()).Where(s=>s.Length>0).Select(s=>
            s.StartsWith("#")?(true,s.TrimStart('#').Trim()):
            (false,s.StartsWith("- ")||s.StartsWith("* ")?"• "+s[2..]:s)).ToArray();
    internal void SetNotes(string notes) {
        if(notes==_notes)return;_notes=notes;SuspendLayout();
        try {
            foreach(Control child in Controls.Cast<Control>().ToArray())child.Dispose();
            Controls.Clear();
            Controls.Add(new Label {AutoSize=true,Text="更新内容",Font=_heading,Margin=new Padding(0,0,0,6)});
            foreach(var line in Parse(string.IsNullOrWhiteSpace(notes)?"暂无更新说明":notes)) {
                if(line.Heading) {
                    Controls.Add(new Label {AutoSize=true,Text=line.Text,UseMnemonic=false,Font=_heading,
                        ForeColor=Color.FromArgb(46,62,78),Margin=new Padding(0,8,0,4)});
                    continue;
                }
                var row=new TableLayoutPanel {AutoSize=true,ColumnCount=2,RowCount=1,Margin=new Padding(0,2,0,5)};
                row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,18));
                row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
                row.Controls.Add(new Label {AutoSize=true,Text="•",ForeColor=Color.FromArgb(88,117,145),Margin=Padding.Empty},0,0);
                var body=new Label {AutoSize=true,Text=line.Text.StartsWith("• ")?line.Text[2..]:line.Text,
                    UseMnemonic=false,ForeColor=Color.FromArgb(46,62,78),Dock=DockStyle.Fill,Margin=Padding.Empty};
                row.Controls.Add(body,1,0);Controls.Add(row);
            }
        }finally{ResumeLayout(true);}
    }
    protected override void OnLayout(LayoutEventArgs e) {
        base.OnLayout(e);
        if(_arranging)return;
        _arranging=true;
        try {
            int height=Padding.Vertical+Controls.Cast<Control>().Sum(c=>c.Height+c.Margin.Vertical);
            Height=Math.Clamp(height,64*DeviceDpi/96,240*DeviceDpi/96);
        }finally{_arranging=false;}
    }
    protected override void Dispose(bool disposing){base.Dispose(disposing);if(disposing)_heading.Dispose();}
}
