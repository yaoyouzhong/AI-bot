using System.Text.Json;

namespace AIBotBridge;
internal sealed record ConfigurationArchive(int Version,DateTimeOffset CreatedAt,Dictionary<string,string> Settings,Birthday[] Birthdays);
internal static class ConfigurationBackup
{
    internal static readonly IReadOnlyDictionary<string,string> Fields=new Dictionary<string,string> {
        ["display_mode"]="ESP8266 显示页面",["display_cycle_enabled"]="ESP8266 启用轮播",["display_cycle_interval_seconds"]="ESP8266 轮播间隔",["display_cycle_pages"]="ESP8266 轮播页面与顺序",
        ["screensaver_timeout_minutes"]="ESP8266 屏保等待",["weather_animation"]="ESP8266 天气动画",["stock_symbols"]="自选股票"
    };
    internal static ConfigurationArchive Capture() {
        var settings=BridgeSettings.Load();var policy=DisplayModes.Load(settings);
        var values=new Dictionary<string,string>{["display_mode"]=policy.SelectedMode,["display_cycle_enabled"]=policy.CycleEnabled?"1":"0",["display_cycle_interval_seconds"]=policy.IntervalSeconds.ToString(),["display_cycle_pages"]=string.Join(',',policy.Pages),
            ["screensaver_timeout_minutes"]=settings.Get("screensaver_timeout_minutes","0"),["weather_animation"]=settings.Get("weather_animation","robot"),["stock_symbols"]=settings.Get("stock_symbols","sh000001")};
        var archive=new ConfigurationArchive(1,DateTimeOffset.UtcNow,values,BirthdayStore.Load());Validate(archive);return archive;
    }
    internal static void Validate(ConfigurationArchive archive) {
        if(archive.Version!=1||archive.Settings is null||archive.Birthdays is null||archive.Settings.Count>Fields.Count||archive.Settings.Any(p=>!Fields.ContainsKey(p.Key)||p.Value is null||p.Value.Length>2048))throw new InvalidDataException("备份版本或字段不支持。");
        foreach(var (key,value) in archive.Settings){bool valid=key switch {
            "display_mode"=>DisplayModes.IsValid(value),"display_cycle_enabled"=>value is "0" or "1","display_cycle_interval_seconds"=>value is "10" or "15" or "30" or "60",
            "display_cycle_pages"=>value.Split(',') is {Length:>0 and <=32} pages&&pages.Distinct().Count()==pages.Length&&pages.All(p=>DisplayModes.Pages.Any(entry=>entry.Mode==p)),
            "screensaver_timeout_minutes"=>int.TryParse(value,out int minutes)&&minutes is >=0 and <=1440,
            "weather_animation"=>value is "robot" or "house" or "plant" or "off" or "pet",
            "stock_symbols"=>value.Length==0||value.Split(',') is {Length:<=20} symbols&&symbols.All(s=>!string.IsNullOrEmpty(StockService.Normalize(s))),_=>false};
            if(!valid)throw new InvalidDataException("备份设置无效："+Fields[key]);}
        if(archive.Birthdays.Any(b=>b is null))throw new InvalidDataException("备份生日条目为空。");BirthdayStore.Validate(archive.Birthdays);
    }
    internal static void Export(string path,ConfigurationArchive archive) {
        Validate(archive);using var stream=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None);JsonSerializer.Serialize(stream,archive,new JsonSerializerOptions{WriteIndented=true});
    }
    internal static ConfigurationArchive Read(string path) {
        using var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);if(stream.Length>65536)throw new InvalidDataException("备份文件超过 64 KB。");
        var archive=JsonSerializer.Deserialize<ConfigurationArchive>(stream,new JsonSerializerOptions{UnmappedMemberHandling=System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow})??throw new InvalidDataException("备份内容为空。");Validate(archive);return archive;
    }
    internal static string Restore(ConfigurationArchive archive,IReadOnlyCollection<string> selected,bool birthdays) {
        Validate(archive);if(selected.Any(k=>!archive.Settings.ContainsKey(k)))throw new InvalidDataException("选择了备份中不存在的设置。");
        if(selected.Count==0&&!birthdays)throw new InvalidOperationException("请至少选择一个恢复项目。");
        var before=Capture();string folder=UserPreferenceFile.PathFor("configuration-backups");Directory.CreateDirectory(folder);
        string backup=Path.Combine(folder,$"before-restore-{DateTime.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.json");Export(backup,before);
        // Birthdays are saved first. If the settings write fails, restore that file;
        // the original complete allowlisted values also remain in the recovery backup.
        if(birthdays)BirthdayStore.Save(archive.Birthdays);
        if(!BridgeSettings.Load().SaveEditable(selected.ToDictionary(k=>k,k=>archive.Settings[k]),out string error)) {
            try{if(birthdays)BirthdayStore.Save(before.Birthdays);}catch(Exception ex){throw new IOException($"恢复失败，生日回滚也未完成。请从 {backup} 恢复。",ex);}
            throw new IOException(error+" 原配置备份："+backup);
        }
        return backup;
    }
}
internal sealed class ConfigurationBackupForm : Form
{
    internal ConfigurationBackupForm(Action restored) {
        Text="AI-bot · 配置迁移";Font=new("Microsoft YaHei UI",9);AutoScaleMode=AutoScaleMode.Dpi;AutoScaleDimensions=new(96,96);ClientSize=new(680,410);MinimumSize=new(580,340);
        var root=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new(16),ColumnCount=1,RowCount=4};root.RowStyles.Add(new(SizeType.AutoSize));root.RowStyles.Add(new(SizeType.AutoSize));root.RowStyles.Add(new(SizeType.Percent,100));root.RowStyles.Add(new(SizeType.AutoSize));root.ColumnStyles.Add(new(SizeType.Percent,100));Controls.Add(root);
        root.Controls.Add(new Label{Text="ESP8266 显示 · 自选股票 · 生日",AutoSize=true,Dock=DockStyle.Fill},0,0);
        var actions=new FlowLayoutPanel{AutoSize=true,Dock=DockStyle.Fill};var export=new Button{Text="导出备份",AutoSize=true};var read=new Button{Text="打开备份",AutoSize=true};var restore=new Button{Text="恢复所选",AutoSize=true,Enabled=false};actions.Controls.AddRange([export,read]);root.Controls.Add(actions,0,1);
        var grid=new DataGridView{Dock=DockStyle.Fill,AllowUserToAddRows=false,AllowUserToDeleteRows=false,RowHeadersVisible=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill,AutoSizeRowsMode=DataGridViewAutoSizeRowsMode.AllCells,BackgroundColor=Color.White};grid.DefaultCellStyle.WrapMode=DataGridViewTriState.True;grid.Columns.Add(new DataGridViewCheckBoxColumn{HeaderText="恢复",AutoSizeMode=DataGridViewAutoSizeColumnMode.ColumnHeader,MinimumWidth=64});foreach(string name in new[]{"项目","当前值","备份值"}){int i=grid.Columns.Add(name,name);grid.Columns[i].ReadOnly=true;}root.Controls.Add(grid,0,2);
        var footer=new TableLayoutPanel{Dock=DockStyle.Fill,AutoSize=true,ColumnCount=2};footer.ColumnStyles.Add(new(SizeType.Percent,100));footer.ColumnStyles.Add(new(SizeType.AutoSize));
        var state=new TextBox{ReadOnly=true,Multiline=true,Height=40,Dock=DockStyle.Fill,BorderStyle=BorderStyle.None,Text="打开备份以选择恢复项目"};footer.Controls.Add(state,0,0);footer.Controls.Add(restore,1,0);root.Controls.Add(footer,0,3);ConfigurationArchive? archive=null;
        export.Click+=(_,_)=>{using var dialog=new SaveFileDialog{Filter="配置备份 (*.json)|*.json",Title="导出配置（包含生日姓名）",FileName=$"AI-bot-settings-{DateTime.Now:yyyyMMdd-HHmmss}.json",OverwritePrompt=true};if(dialog.ShowDialog(this)!=DialogResult.OK)return;try{ConfigurationBackup.Export(dialog.FileName,ConfigurationBackup.Capture());state.Text="配置已导出。";}catch(Exception ex){state.Text=ex.Message;}};
        read.Click+=(_,_)=>{using var dialog=new OpenFileDialog{Filter="配置备份 (*.json)|*.json"};if(dialog.ShowDialog(this)!=DialogResult.OK)return;archive=null;restore.Enabled=false;grid.Rows.Clear();try{archive=ConfigurationBackup.Read(dialog.FileName);var current=ConfigurationBackup.Capture();foreach(var p in archive.Settings){int row=grid.Rows.Add(false,ConfigurationBackup.Fields[p.Key],current.Settings.GetValueOrDefault(p.Key,""),p.Value);grid.Rows[row].Tag=p.Key;}int b=grid.Rows.Add(false,"生日列表",Describe(current.Birthdays),Describe(archive.Birthdays));grid.Rows[b].Tag="birthdays";state.Text=$"备份时间：{archive.CreatedAt.ToLocalTime():yyyy-MM-dd HH:mm}";restore.Enabled=true;}catch(Exception ex){archive=null;state.Text=ex.Message;}};
        restore.Click+=(_,_)=>{if(archive is null)return;grid.EndEdit();var keys=grid.Rows.Cast<DataGridViewRow>().Where(r=>r.Cells[0].Value is true).Select(r=>(string)r.Tag!).ToArray();try{string backup=ConfigurationBackup.Restore(archive,keys.Where(k=>k!="birthdays").ToArray(),keys.Contains("birthdays"));restored();state.Text="所选配置已恢复。原配置备份："+backup;restore.Enabled=false;}catch(Exception ex){state.Text=ex.Message;}};SettingsWindow.FitScreen(this);
    }
    private static string Describe(Birthday[] values)=>values.Length==0?"无":string.Join("；",values.Select(b=>$"{b.Name} {b.Month}/{b.Day}{(b.Lunar?" 农历":"")}{(b.Leap?" 闰月":"")} 提前{b.RemindDays}天"));
}
