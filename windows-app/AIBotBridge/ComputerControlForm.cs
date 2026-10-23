namespace AIBotBridge;

internal sealed class ComputerControlForm : Form
{
    private readonly ComputerLauncherStore _store;
    private readonly List<(TextBox Name,TextBox Path,string Id)> _rows=[];
    private readonly Label _message=new(){AutoSize=true,Dock=DockStyle.Fill,ForeColor=SettingsWindow.Muted,Margin=new(0,0,0,16)};
    internal string EditFingerprint=>System.Text.Json.JsonSerializer.Serialize(_rows.Select(x=>new{x.Id,Name=x.Name.Text,Path=x.Path.Text}));
    internal ComputerControlForm(ComputerLauncherStore? store=null) {
        _store=store??new();Text="TAB5 · 电脑控制";Font=SettingsWindow.BodyFont();AutoScaleMode=AutoScaleMode.Dpi;AutoScaleDimensions=new(96,96);
        ClientSize=new(720,700);MinimumSize=new(640,560);StartPosition=FormStartPosition.CenterParent;BackColor=Color.White;ForeColor=SettingsWindow.Ink;
        var layout=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new(24),ColumnCount=1,RowCount=4};layout.ColumnStyles.Add(new(SizeType.Percent,100));
        layout.RowStyles.Add(new(SizeType.Absolute,46));layout.RowStyles.Add(new(SizeType.Absolute,34));layout.RowStyles.Add(new(SizeType.Percent,100));layout.RowStyles.Add(new(SizeType.Absolute,58));Controls.Add(layout);
        var header=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,Margin=Padding.Empty};header.ColumnStyles.Add(new(SizeType.Percent,100));header.ColumnStyles.Add(new(SizeType.AutoSize));layout.Controls.Add(header,0,0);
        var title=SettingsWindow.Title("电脑控制");title.Margin=Padding.Empty;header.Controls.Add(title,0,0);
        var pinSettings=new Button{Name="pin-settings",Text="一键解锁设置",Anchor=AnchorStyles.Top|AnchorStyles.Right,Margin=Padding.Empty};SettingsWindow.StyleButton(pinSettings);pinSettings.MinimumSize=new(140,34);pinSettings.Click+=(_,_)=>{using var form=new Tab5PinForm();form.ShowDialog(this);};header.Controls.Add(pinSettings,1,0);
        _message.Text="快捷启动项（最多 4 项）";_message.Margin=Padding.Empty;layout.Controls.Add(_message,0,1);
        var viewport=new Panel{Dock=DockStyle.Fill,AutoScroll=true,Margin=Padding.Empty};layout.Controls.Add(viewport,0,2);
        var list=new TableLayoutPanel{Dock=DockStyle.Top,Height=504,ColumnCount=1,RowCount=4,Margin=Padding.Empty};list.ColumnStyles.Add(new(SizeType.Percent,100));viewport.Controls.Add(list);
        ComputerLauncher[] items=[];try{items=_store.Load();}catch(Exception ex) when(ex is IOException or System.Text.Json.JsonException or UnauthorizedAccessException){_message.Text="常用启动配置未能读取，请重新选择并保存。";}
        for(int index=0;index<4;index++) {
            int slot=index;var item=items.ElementAtOrDefault(index);var row=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new(14,12,14,10),BackColor=SettingsWindow.Surface,ColumnCount=2,RowCount=3,Margin=new(0,0,0,10)};
            row.ColumnStyles.Add(new(SizeType.Absolute,72));row.ColumnStyles.Add(new(SizeType.Percent,100));row.RowStyles.Add(new(SizeType.Absolute,30));row.RowStyles.Add(new(SizeType.Absolute,28));row.RowStyles.Add(new(SizeType.Percent,100));
            var name=new TextBox{Name="launcher-name-"+index,Text=item?.Name??"",MaxLength=16,Dock=DockStyle.Fill,PlaceholderText="启动名称",Margin=Padding.Empty};
            var path=new TextBox{Name="launcher-path-"+index,Text=item?.Path??"",ReadOnly=true,Dock=DockStyle.Fill,TabStop=false,BorderStyle=BorderStyle.None,BackColor=SettingsWindow.Surface,ForeColor=SettingsWindow.Muted,PlaceholderText="未选择软件或文件夹",Margin=new(0,4,0,4)};
            row.Controls.Add(new Label{Text="启动 "+(index+1),AutoSize=false,Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft,Margin=new(0,0,8,0)},0,0);row.Controls.Add(name,1,0);row.Controls.Add(path,0,1);row.SetColumnSpan(path,2);
            var actions=new FlowLayoutPanel{AutoSize=true,Dock=DockStyle.Fill,Margin=Padding.Empty,WrapContents=false};
            void Button(string text,Action action){var button=new Button{Text=text,AutoSize=true,Margin=new(0,0,8,0)};SettingsWindow.StyleButton(button);button.Click+=(_,_)=>action();actions.Controls.Add(button);}
            Button("选择软件",()=>{using var picker=new OpenFileDialog{Title="选择本机软件",Filter="应用程序 (*.exe)|*.exe",CheckFileExists=true};if(picker.ShowDialog(this)==DialogResult.OK)Choose(slot,picker.FileName);});
            Button("选择文件夹",()=>{using var picker=new FolderBrowserDialog{Description="选择要从 TAB5 打开的文件夹",UseDescriptionForTitle=true};if(picker.ShowDialog(this)==DialogResult.OK)Choose(slot,picker.SelectedPath);});
            Button("移除",()=>{name.Clear();path.Clear();_rows[slot]=(name,path,Guid.NewGuid().ToString("D"));});
            row.Controls.Add(actions,0,2);row.SetColumnSpan(actions,2);list.RowStyles.Add(new(SizeType.Absolute,126));list.Controls.Add(row,0,index);_rows.Add((name,path,item?.Id??Guid.NewGuid().ToString("D")));
        }
        var footer=new FlowLayoutPanel{AutoSize=true,Dock=DockStyle.Fill,FlowDirection=FlowDirection.RightToLeft,Margin=new(0,12,0,0)};
        var save=new Button{Name="save-launchers",Text="保存",AutoSize=true};SettingsWindow.StyleButton(save,true);footer.Controls.Add(save);
        var cancel=new Button{Text="取消",AutoSize=true,DialogResult=DialogResult.Cancel};SettingsWindow.StyleButton(cancel);footer.Controls.Add(cancel);CancelButton=cancel;
        save.Click+=(_,_)=>{try{_store.Save(_rows.Where(x=>x.Path.Text.Length>0).Select(x=>new ComputerLauncher(x.Id,x.Name.Text.Trim(),x.Path.Text)).ToArray());DialogResult=DialogResult.OK;Close();}catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or ArgumentException){_message.Text=ex.Message;_message.ForeColor=Color.Firebrick;}};
        layout.Controls.Add(footer,0,3);SettingsWindow.FitScreen(this);UnsavedChanges.Track(this);
    }
    private void Choose(int index,string path) {
        var row=_rows[index];row.Path.Text=path;if(string.IsNullOrWhiteSpace(row.Name.Text))row.Name.Text=Path.GetFileNameWithoutExtension(path.TrimEnd(Path.DirectorySeparatorChar))[..Math.Min(16,Path.GetFileNameWithoutExtension(path.TrimEnd(Path.DirectorySeparatorChar)).Length)];
        _rows[index]=(row.Name,row.Path,Guid.NewGuid().ToString("D"));
    }
}
