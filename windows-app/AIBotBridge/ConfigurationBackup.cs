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
    private readonly Action _restored;
    private readonly TableLayoutPanel _home = new() { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1, Padding = new(20) };
    private readonly TableLayoutPanel _review = new() { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new(16), Visible = false };
    private readonly DataGridView _grid = new() { Dock = DockStyle.Fill, AllowUserToAddRows = false, AllowUserToDeleteRows = false,
        RowHeadersVisible = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
        ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize, AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells };
    private readonly Button _apply = new() { Text = "恢复所选", Enabled = false };
    private readonly Label _selection = new() { AutoSize = true, Anchor = AnchorStyles.Left, ForeColor = SettingsWindow.Muted };
    private ConfigurationArchive? _archive;
    private Size _homeSize, _homeMinimum;

    internal ConfigurationBackupForm(Action restored)
    {
        _restored = restored;
        Text = "AI-bot · 配置迁移";
        Font = SettingsWindow.BodyFont();
        AutoScaleDimensions = new(96, 96);
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new(600, 290);
        MinimumSize = SizeFromClientSize(new(460, 230));
        _home.ColumnStyles.Add(new(SizeType.Percent, 100));
        _home.Controls.Add(SettingsWindow.Title("备份与恢复"));
        _home.Controls.Add(new Label { Text = "可迁移显示设置、自选股票和生日。", AutoSize = true,
            ForeColor = SettingsWindow.Muted, Margin = new(0, 0, 0, 16) });
        AddChoice("备份这台电脑", "把当前配置保存为文件。", "导出备份…", Export);
        AddChoice("从备份恢复", "勾选要恢复的配置。", "打开备份…", ChooseBackup);
        Controls.Add(_home);
        Controls.Add(_review);

        _review.ColumnStyles.Add(new(SizeType.Percent, 100));
        _review.RowStyles.Add(new(SizeType.AutoSize));
        _review.RowStyles.Add(new(SizeType.Percent, 100));
        _review.RowStyles.Add(new(SizeType.AutoSize));
        _review.Controls.Add(SettingsWindow.Title("选择要恢复的项目"), 0, 0);
        _grid.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
        _grid.Columns.Add(new DataGridViewCheckBoxColumn { HeaderText = "选择", AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells,
            MinimumWidth = 72, SortMode = DataGridViewColumnSortMode.NotSortable });
        foreach (string column in new[] { "项目", "当前值", "备份值" })
        {
            var field = _grid.Columns[_grid.Columns.Add(column, column)];
            field.ReadOnly = true;
            field.SortMode = DataGridViewColumnSortMode.NotSortable;
        }
        _review.Controls.Add(_grid, 0, 1);
        var footer = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 3, Margin = new(0, 12, 0, 0) };
        footer.ColumnStyles.Add(new(SizeType.AutoSize)); footer.ColumnStyles.Add(new(SizeType.Percent, 100)); footer.ColumnStyles.Add(new(SizeType.AutoSize));
        var back = new Button { Text = "返回" };
        SettingsWindow.StyleButton(back);
        SettingsWindow.StyleButton(_apply, true);
        footer.Controls.Add(back, 0, 0); footer.Controls.Add(_selection, 1, 0); footer.Controls.Add(_apply, 2, 0);
        _review.Controls.Add(footer, 0, 2);
        _grid.CurrentCellDirtyStateChanged += (_, _) => { if (_grid.IsCurrentCellDirty) _grid.CommitEdit(DataGridViewDataErrorContexts.Commit); };
        _grid.CellValueChanged += (_, _) => RefreshSelection();
        back.Click += (_, _) => { ShowHome(); };
        _apply.Click += (_, _) => Restore();
        Shown += (_, _) => { if (_archive is null) ClientSize = new(ClientSize.Width, _home.GetPreferredSize(new(ClientSize.Width, 0)).Height); };
        SettingsWindow.FitScreen(this);
    }

    private void AddChoice(string title, string description, string action, EventHandler handler)
    {
        var row = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 2, RowCount = 2,
            Padding = new(14, 10, 14, 10), Margin = new(0, 0, 0, 8), BackColor = SettingsWindow.Surface };
        row.ColumnStyles.Add(new(SizeType.Percent, 100)); row.ColumnStyles.Add(new(SizeType.AutoSize));
        row.Controls.Add(new Label { Text = title, Font = SettingsWindow.SectionFont(), AutoSize = true }, 0, 0);
        row.Controls.Add(new Label { Text = description, AutoSize = true, ForeColor = SettingsWindow.Muted, Dock = DockStyle.Fill }, 0, 1);
        var button = new Button { Text = action, Anchor = AnchorStyles.Right };
        SettingsWindow.StyleButton(button);
        button.Click += handler;
        row.Controls.Add(button, 1, 0); row.SetRowSpan(button, 2);
        _home.Controls.Add(row);
    }

    private void Export(object? sender, EventArgs e)
    {
        using var dialog = new SaveFileDialog { Filter = "配置备份 (*.json)|*.json", Title = "导出配置（包含生日姓名）",
            FileName = $"AI-bot-settings-{DateTime.Now:yyyyMMdd-HHmmss}.json", OverwritePrompt = true };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try { ConfigurationBackup.Export(dialog.FileName, ConfigurationBackup.Capture()); MessageBox.Show(this, "已保存备份文件。", "导出完成"); }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "导出失败"); }
    }

    private void ChooseBackup(object? sender, EventArgs e)
    {
        using var dialog = new OpenFileDialog { Filter = "配置备份 (*.json)|*.json" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try { ShowBackup(ConfigurationBackup.Read(dialog.FileName), ConfigurationBackup.Capture()); }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "打开备份失败"); }
    }

    internal void ShowBackup(ConfigurationArchive backup, ConfigurationArchive current)
    {
        _archive = backup;
        _grid.Rows.Clear();
        foreach (var pair in backup.Settings)
            _grid.Rows[_grid.Rows.Add(false, ConfigurationBackup.Fields[pair.Key], DisplayValue(pair.Key, current.Settings.GetValueOrDefault(pair.Key, "")), DisplayValue(pair.Key, pair.Value))].Tag = pair.Key;
        _grid.Rows[_grid.Rows.Add(false, "生日列表", Describe(current.Birthdays), Describe(backup.Birthdays))].Tag = "birthdays";
        _homeSize = Size;
        _homeMinimum = MinimumSize;
        var area = Screen.FromControl(this).WorkingArea;
        var minimum = SizeFromClientSize(new Size(Math.Max(510, Font.Height * 22), Math.Max(360, Font.Height * 14)));
        MinimumSize = new(Math.Min(minimum.Width, area.Width), Math.Min(minimum.Height, area.Height));
        _home.Hide(); _review.Show();
        Height = Math.Min(area.Height, Math.Max(Height, SizeFromClientSize(new Size(0, Font.Height * 18)).Height));
        if (StartPosition != FormStartPosition.Manual) Location = new(Math.Clamp(Left, area.Left, Math.Max(area.Left, area.Right - Width)), Math.Clamp(Top, area.Top, Math.Max(area.Top, area.Bottom - Height)));
        RefreshSelection();
    }

    private void ShowHome()
    {
        _review.Hide();
        _home.Show();
        _archive = null;
        MinimumSize = _homeMinimum;
        Size = _homeSize;
    }

    private void RefreshSelection()
    {
        int count = _grid.Rows.Cast<DataGridViewRow>().Count(row => row.Cells[0].Value is true);
        _selection.Text = $"已选 {count} 项";
        _apply.Enabled = count > 0;
    }

    private void Restore()
    {
        if (_archive is null) return;
        _grid.EndEdit();
        var keys = _grid.Rows.Cast<DataGridViewRow>().Where(r => r.Cells[0].Value is true).Select(r => (string)r.Tag!).ToArray();
        if (keys.Length == 0) return;
        try {
            string backup = ConfigurationBackup.Restore(_archive, keys.Where(k => k != "birthdays").ToArray(), keys.Contains("birthdays"));
            _restored();
            MessageBox.Show(this, $"已恢复 {keys.Length} 项。原配置已备份到\n{backup}", "恢复完成");
            ShowHome();
        } catch (Exception ex) { MessageBox.Show(this, ex.Message, "恢复失败"); }
    }

    private static string DisplayValue(string key, string value) => key is "display_cycle_pages" or "stock_symbols" ? value.Replace(",", ", ") : value;

    private static string Describe(Birthday[] values) => values.Length == 0 ? "无" : string.Join("；", values.Select(b =>
        $"{b.Name} {b.Month}/{b.Day}{(b.Lunar ? " 农历" : "")}{(b.Leap ? " 闰月" : "")} 提前{b.RemindDays}天"));
}
