namespace AIBotBridge;

internal sealed class Tab5ReleaseNotes : FlowLayoutPanel
{
    private string? _notes;
    private readonly Font _heading=new("Microsoft YaHei UI",10F,FontStyle.Bold);
    internal Tab5ReleaseNotes() {
        AutoSize=true;AutoSizeMode=AutoSizeMode.GrowAndShrink;WrapContents=false;
        FlowDirection=FlowDirection.TopDown;Margin=new Padding(0,0,0,10);
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
            foreach(var line in Parse(string.IsNullOrWhiteSpace(notes)?"暂无更新说明":notes))
                Controls.Add(new Label {AutoSize=true,Text=line.Text,UseMnemonic=false,
                    Font=line.Heading?_heading:Font,ForeColor=Color.FromArgb(46,62,78),
                    Margin=new Padding(0,line.Heading?8:3,0,3)});
        }finally{ResumeLayout(true);}
    }
    protected override void Dispose(bool disposing){base.Dispose(disposing);if(disposing)_heading.Dispose();}
}
