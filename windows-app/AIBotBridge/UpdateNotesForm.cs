using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace AIBotBridge;

// Native, read-only release notes: source text never becomes HTML or script.
internal sealed class UpdateNotesForm : Form
{
    internal sealed record Source(string Caption,string Notes) {public override string ToString()=>Caption;}
    internal static (string Version,string Notes) Bundled(string component) {
        using var stream=typeof(UpdateNotesForm).Assembly.GetManifestResourceStream("AIBotBridge.Notes."+component);
        if(stream is null)return ("未知","此程序包未附带该组件的变更说明。");
        using var reader=new StreamReader(stream);string text=reader.ReadToEnd();
        var match=Regex.Match(text,@"(?m)^## (\d+\.\d+\.\d+(?:-ui)?) - \d{4}-\d{2}-\d{2}\s*$");
        if(!match.Success)return ("未知","未找到随包版本说明。");
        int start=match.Index+match.Length,end=text.IndexOf("\n## ",start,StringComparison.Ordinal);
        return (match.Groups[1].Value,text[start..(end<0?text.Length:end)].Trim());
    }
    internal UpdateNotesForm(IReadOnlyDictionary<string,ComponentUpdate> published,UpdateDevice[] devices,string selected) {
        Text="AI-bot · 全部更新说明";Font=new("Microsoft YaHei UI",10);BackColor=Color.White;
        AutoScaleDimensions=new(96,96);AutoScaleMode=AutoScaleMode.Dpi;ClientSize=new(860,620);MinimumSize=new(640,460);
        var root=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new(22),ColumnCount=1,RowCount=3};
        root.ColumnStyles.Add(new(SizeType.Percent,100));root.RowStyles.Add(new(SizeType.AutoSize));root.RowStyles.Add(new(SizeType.Percent,100));root.RowStyles.Add(new(SizeType.AutoSize));Controls.Add(root);
        root.Controls.Add(new Label{Text="更新说明",Font=new(Font.FontFamily,18,FontStyle.Bold),AutoSize=true,Margin=new(0,0,0,16)},0,0);
        var tabs=new TabControl{Name="notes-components",Dock=DockStyle.Fill};root.Controls.Add(tabs,0,1);
        foreach(var (component,title) in new[]{("bridge","电脑桥接"),("tab5","TAB5 固件"),("esp8266","小屏固件")}) {
            var local=Bundled(component);published.TryGetValue(component,out var release);
            string current=devices.FirstOrDefault(d=>d.Component==component)?.Version??"未连接";
            var tab=new TabPage(title){Name=component,BackColor=Color.White,Padding=new(12)};tabs.TabPages.Add(tab);
            var layout=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=3};layout.ColumnStyles.Add(new(SizeType.Percent,100));layout.RowStyles.Add(new(SizeType.AutoSize));layout.RowStyles.Add(new(SizeType.AutoSize));layout.RowStyles.Add(new(SizeType.Percent,100));tab.Controls.Add(layout);
            layout.Controls.Add(new Label{Text=$"当前运行：{current}     最新发布：{release?.Version??"未查询到"}",AutoSize=true,Dock=DockStyle.Fill,Margin=new(0,4,0,12),ForeColor=Color.FromArgb(88,101,119)},0,0);
            var source=new ComboBox{Name="notes-source",DropDownStyle=ComboBoxStyle.DropDownList,Dock=DockStyle.Top,Margin=new(0,0,0,12)};
            source.Items.Add(new Source(local.Version+(current==local.Version?" · 本地版本说明":" · 随包版本说明"),local.Notes));
            if(release is not null)source.Items.Add(new Source(release.Version+" · 已发布版本说明",release.Notes));
            layout.Controls.Add(source,0,1);
            var view=new MarkdownNotesView{Dock=DockStyle.Fill};layout.Controls.Add(view,0,2);
            source.SelectedIndexChanged+=(_,_)=>view.SetNotes(((Source)source.SelectedItem!).Notes);
            source.SelectedIndex=current==local.Version?0:release is null?0:1;
            if(component==selected)tabs.SelectedTab=tab;
        }
        var close=new Button{Text="关闭",AutoSize=true,Anchor=AnchorStyles.Right,Margin=new(0,14,0,0),DialogResult=DialogResult.Cancel};SettingsWindow.StyleButton(close);root.Controls.Add(close,0,2);CancelButton=close;
        SettingsWindow.FitScreen(this);
    }
}

internal sealed class MarkdownNotesView : FlowLayoutPanel
{
    private readonly Font _heading=new("Microsoft YaHei UI",12,FontStyle.Bold);
    private static readonly Regex Links=new(@"\[([^\]]+)\]\(([^\s)]+)\)",RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(100));
    internal MarkdownNotesView(){AutoScroll=true;WrapContents=false;FlowDirection=FlowDirection.TopDown;Padding=new(8);BackColor=Color.White;SettingsWindow.FitFlow(this);}
    internal static (string Text,(int Start,int Length,string Url)[] Links) Inline(string markdown){
        var result=new StringBuilder();var links=new List<(int,int,string)>();int offset=0;
        foreach(Match match in Links.Matches(markdown)) {
            result.Append(markdown[offset..match.Index].Replace("**","").Replace("`",""));int start=result.Length;
            string caption=match.Groups[1].Value.Replace("**","").Replace("`","");result.Append(caption);
            if(Uri.TryCreate(match.Groups[2].Value,UriKind.Absolute,out var uri)&&uri.Scheme=="https"&&uri.UserInfo=="")links.Add((start,caption.Length,uri.AbsoluteUri));
            offset=match.Index+match.Length;
        }
        result.Append(markdown[offset..].Replace("**","").Replace("`",""));return (result.ToString(),links.ToArray());
    }
    private LinkLabel Paragraph(string text,bool heading=false) {
        var parsed=Inline(text);var label=new LinkLabel{Text=parsed.Text,AutoSize=true,UseMnemonic=false,Font=heading?_heading:Font,
            ForeColor=Color.FromArgb(32,43,59),LinkColor=Color.FromArgb(34,109,215),Margin=new(0,heading?12:0,0,heading?8:10)};
        label.Links.Clear();foreach(var link in parsed.Links)label.Links.Add(link.Start,link.Length,link.Url);
        label.LinkClicked+=(_,e)=>{if(e.Link?.LinkData is string url)try{Process.Start(new ProcessStartInfo(url){UseShellExecute=true});}catch(Exception ex){MessageBox.Show(FindForm(),ex.Message,"无法打开链接");}};
        return label;
    }
    internal void SetNotes(string notes) {
        SuspendLayout();try {
            foreach(Control control in Controls.Cast<Control>().ToArray())control.Dispose();Controls.Clear();AutoScrollPosition=Point.Empty;
            // Put change descriptions first while retaining the full download section below.
            var sections=Regex.Split(notes.Replace("\r\n","\n"),@"(?m)(?=^## )");
            string ordered=string.Join("\n",sections.OrderBy(s=>s.StartsWith("## 完整下载",StringComparison.Ordinal)||s.StartsWith("## All downloads",StringComparison.OrdinalIgnoreCase)?1:0));
            var lines=ordered.Split('\n');
            for(int i=0;i<lines.Length;i++) {
                string line=lines[i].Trim();if(line.Length==0)continue;
                if(line.StartsWith('|')&&i+1<lines.Length&&Regex.IsMatch(lines[i+1],@"^\s*\|?[\s:|\-]+\|?\s*$")) {
                    string[] headers=line.Trim('|').Split('|').Select(s=>s.Trim()).ToArray();i+=2;
                    for(;i<lines.Length&&lines[i].TrimStart().StartsWith('|');i++) {
                        var cells=lines[i].Trim().Trim('|').Split('|');
                        for(int column=0;column<cells.Length;column++)Controls.Add(Paragraph((column<headers.Length?headers[column]+"：":"")+cells[column].Trim(),column==0));
                    }
                    i--;continue;
                }
                if(line.StartsWith('#'))Controls.Add(Paragraph(line.TrimStart('#').Trim(),true));
                else Controls.Add(Paragraph(line.StartsWith("- ")||line.StartsWith("* ")?"•  "+line[2..]:line));
            }
            if(Controls.Count==0)Controls.Add(Paragraph("暂无更新说明。"));
        }finally{ResumeLayout(true);}
    }
    protected override void Dispose(bool disposing){base.Dispose(disposing);if(disposing)_heading.Dispose();}
}
