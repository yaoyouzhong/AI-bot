namespace AIBotBridge;

internal sealed class Tab5ReleaseNotes : Panel
{
    private string? _notes;
    private readonly Font _heading=new("Microsoft YaHei UI",10F,FontStyle.Bold);
    private readonly RichTextBox _text=new(){Dock=DockStyle.Fill,ReadOnly=true,BorderStyle=BorderStyle.None,WordWrap=true,ScrollBars=RichTextBoxScrollBars.Vertical,DetectUrls=false};
    internal Tab5ReleaseNotes() {
        Margin=new Padding(0,8,0,10);Padding=new Padding(12,8,8,8);
        BackColor=Color.FromArgb(246,248,250);_text.BackColor=BackColor;_text.ForeColor=Color.FromArgb(46,62,78);Controls.Add(_text);
    }
    internal static (bool Heading,string Text)[] Parse(string notes)=>notes.Replace("\r\n","\n").Split('\n')
        .Select(s=>s.Trim()).Where(s=>s.Length>0).Select(s=>
            s.StartsWith("#")?(true,s.TrimStart('#').Trim()):
            (false,s.StartsWith("- ")||s.StartsWith("* ")?"• "+s[2..]:s)).ToArray();
    internal void SetNotes(string notes) {
        if(notes==_notes)return;_notes=notes;_text.Clear();
        void Append(string text,bool heading) {
            _text.SelectionFont=heading?_heading:Font;
            _text.AppendText(text+Environment.NewLine);
        }
        Append("更新内容",true);
        foreach(var line in Parse(string.IsNullOrWhiteSpace(notes)?"暂无更新说明":notes))Append(line.Text,line.Heading);
        _text.Select(0,0);_text.ScrollToCaret();
    }
    protected override void Dispose(bool disposing){base.Dispose(disposing);if(disposing)_heading.Dispose();}
}