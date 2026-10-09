namespace AIBotBridge;

internal static class UpdateNotesSelfTest
{
    internal static void Run(string directory) {
        string[] components=["bridge","tab5","esp8266"];
        string[] versions=["0.6.3","0.2.154-ui","0.5.1"];
        UpdateDevice[] devices=[new("bridge","电脑桥接",versions[0],"运行中",true,"bridge",""),new("tab","TAB5",versions[1],"在线",true,"upgrade-tab5",""),new("esp","小屏",versions[2],"在线",true,"flash","")];
        const string body="## 完整下载 / All downloads\n[完整下载中心](https://github.com/yaoyouzhong/AI-bot/blob/main/DOWNLOADS.md)\n\n| 用途 | 版本 | 下载 | SHA-256 |\n| --- | --- | --- | --- |\n| Windows 桥接 | 0.6.2 | [安装包](https://github.com/yaoyouzhong/AI-bot/releases/download/bridge-v0.6.2/setup.exe) | 0123456789abcdef |\n\n## 功能更新\n- **网易云音乐**：采集曲名、歌手和专辑，两个屏幕共享数据。\n- Fix the update view and preserve each component's independent version.\n";
        var published=components.Select((component,index)=>new ComponentUpdate(component,index==1?"0.2.150-ui":index==0?"0.6.2":"0.5.1",new(0,1,0),body)).ToDictionary(x=>x.Component);
        for(int index=0;index<components.Length;index++) {
            var bundle=UpdateNotesForm.Bundled(components[index]);
            if(bundle.Version!=versions[index]||bundle.Notes.Length<40)throw new Exception("Missing bundled component notes: "+components[index]);
        }
        var inline=MarkdownNotesView.Inline("**测试** [本地文件](../LOCAL.md) [不执行](javascript:alert) [凭据](https://user:password@example.org/a) [说明](https://github.com/yaoyouzhong/AI-bot)");
        if(inline.Links.Length!=1||inline.Text.Contains("](")||inline.Text.Contains("**"))throw new Exception("Markdown caption/link handling failed");
        foreach(float scale in new[]{1f,1.5f,2f}) {
            using var form=new UpdateNotesForm(published,devices,"tab5"){StartPosition=FormStartPosition.Manual,Location=new(-30000,-30000),ShowInTaskbar=false};
            form.Show();Application.DoEvents();if(scale!=1)form.Scale(new SizeF(scale,scale));
            var tabs=(TabControl)form.Controls.Find("notes-components",true).Single();
            if(tabs.TabPages.Count!=3||tabs.SelectedTab!.Name!="tab5")throw new Exception("Missing or wrong component tab");
            foreach(TabPage tab in tabs.TabPages) {
                tabs.SelectedTab=tab;Application.DoEvents();
                var source=(ComboBox)tab.Controls.Find("notes-source",true).Single();
                var view=tab.Controls.OfType<TableLayoutPanel>().Single().Controls.OfType<MarkdownNotesView>().Single();
                if(source.Items.Count!=2||source.SelectedIndex!=0||!source.Text.Contains("本地版本"))throw new Exception("Local and published notes are not distinguished");
                for(int choice=0;choice<2;choice++) {
                    source.SelectedIndex=choice;Application.DoEvents();
                    var labels=view.Controls.OfType<LinkLabel>().ToArray();
                    if(labels.Any(x=>x.Text.StartsWith("##")||x.Text.StartsWith('|')||x.Text.Contains("](")))throw new Exception("Raw markdown visible");
                    if(choice==1&&(labels[0].Text!="功能更新"||!labels.Any(x=>x.Text=="用途：Windows 桥接")||labels.Sum(x=>x.Links.Count)!=2))throw new Exception("Descriptions, download table or links lost");
                    if(view.HorizontalScroll.Visible)throw new Exception("Notes require horizontal scrolling");
                    if(scale==1){using var image=new Bitmap(form.Width,form.Height);form.DrawToBitmap(image,new(Point.Empty,form.Size));image.Save(Path.Combine(directory,$"notes-{tab.Name}-{(choice==0?"local":"published")}.png"));}
                }
            }
            form.Size=form.MinimumSize;Application.DoEvents();
            var close=form.Controls.Cast<Control>().SelectMany(x=>x.Controls.Cast<Control>()).OfType<Button>().Single();
            if(!form.RectangleToScreen(form.ClientRectangle).Contains(close.RectangleToScreen(close.ClientRectangle)))throw new Exception("Notes close button clipped at minimum size");
            using var minimum=new Bitmap(form.Width,form.Height);form.DrawToBitmap(minimum,new(Point.Empty,form.Size));minimum.Save(Path.Combine(directory,$"notes-minimum-{scale}.png"));form.Close();
        }
        using var unknown=new UpdateNotesForm(published,[],"esp8266");
        var unknownTabs=(TabControl)unknown.Controls.Find("notes-components",true).Single();
        foreach(TabPage tab in unknownTabs.TabPages)if(((ComboBox)tab.Controls.Find("notes-source",true).Single()).SelectedIndex!=1)throw new Exception("Unknown installed version labeled as local");
        Console.WriteLine("UPDATE_NOTES_OK three component resources, local/published/unknown version distinction, readable headings/lists/downloads/links, descriptions first, 100/150/200 percent and minimum-size captures; no browser or installation");
    }
}
