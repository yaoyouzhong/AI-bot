namespace AIBotBridge;

internal sealed class DeviceDataForm : Form
{
    internal DeviceDataForm(DeviceRegistryStore store,string id)
    {
        var device=store.Snapshot.Devices.Single(d=>d.Id==id);
        SuspendLayout();
        Text=$"{device.Name} · 数据设置";
        Font=new Font("Microsoft YaHei UI",9F);AutoScaleDimensions=new(96,96);AutoScaleMode=AutoScaleMode.Dpi;
        ClientSize=new(536,480);MinimumSize=new(500,420);StartPosition=FormStartPosition.CenterScreen;
        BackColor=Color.White;MinimizeBox=false;MaximizeBox=false;
        var root=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=2,Padding=new Padding(16),Margin=Padding.Empty};
        root.ColumnStyles.Add(new(SizeType.Percent,100));root.RowStyles.Add(new(SizeType.Percent,100));root.RowStyles.Add(new(SizeType.AutoSize));
        var body=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,AutoScroll=true,Margin=Padding.Empty};
        root.Controls.Add(body,0,0);
        Label Note(string text)=>new(){Text=text,AutoSize=true,ForeColor=Color.DimGray,Margin=new Padding(0,0,0,8)};
        Label Heading(string text)=>new(){Text=text,AutoSize=true,Font=new Font(Font,FontStyle.Bold),Margin=new Padding(0,6,0,6)};
        body.Controls.Add(Note("选择此设备需要的数据，账号在「账号数据」中统一管理。"));
        body.Controls.Add(Heading("采集内容"));
        var sourceGrid=new TableLayoutPanel{AutoSize=true,ColumnCount=3,RowCount=2,Margin=new Padding(0,0,0,10)};
        for(int column=0;column<3;column++)sourceGrid.ColumnStyles.Add(new(SizeType.Percent,100F/3));
        for(int row=0;row<2;row++)sourceGrid.RowStyles.Add(new(SizeType.AutoSize));
        var sources=new Dictionary<string,CheckBox>();
        string[] labels=["会话动态","模型额度","天气预报","股票行情","音乐信息","系统状态"];
        for(int i=0;i<DeviceRegistryStore.SourceIds.Length;i++) {
            string key=DeviceRegistryStore.SourceIds[i];
            var check=new CheckBox{Text=labels[i],Name="source-"+key,AutoSize=true,Checked=device.Sources.Contains(key),Margin=new Padding(0,4,8,4)};
            sources.Add(key,check);sourceGrid.Controls.Add(check,i%3,i/3);
        }
        body.Controls.Add(sourceGrid);body.Controls.Add(Heading("国产模型"));
        var relation=Note("");relation.Name="quota-relation";body.Controls.Add(relation);
        var models=new TableLayoutPanel{AutoSize=true,ColumnCount=3,RowCount=9,Margin=Padding.Empty,Name="provider-options"};
        models.ColumnStyles.Add(new(SizeType.Percent,36));models.ColumnStyles.Add(new(SizeType.Percent,25));models.ColumnStyles.Add(new(SizeType.Percent,39));
        Label Cell(string text,bool heading=false)=>new(){Text=text,AutoSize=true,Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft,
            ForeColor=heading?Color.DimGray:Color.FromArgb(45,55,69),Margin=Padding.Empty,Padding=new Padding(8,5,4,5),
            BackColor=heading?Color.FromArgb(240,244,248):Color.Transparent};
        models.RowStyles.Add(new(SizeType.AutoSize));
        models.Controls.Add(Cell("模型 / 平台",true),0,0);models.Controls.Add(Cell("英文名称",true),1,0);models.Controls.Add(Cell("采集内容",true),2,0);
        var providers=new Dictionary<string,CheckBox>();
        (string Id,string Chinese,string English,string Quota)[] entries=[
            ("qwen","通义千问","Qwen","套餐额度"),
            ("kimi","月之暗面","Kimi","套餐额度"),
            ("minimax","稀宇科技","MiniMax","套餐额度"),
            ("deepseek","深度求索","DeepSeek","账户余额"),
            ("zhipu","智谱","GLM","账户余额"),
            ("stepfun","阶跃星辰","StepFun","账户余额"),
            ("baidu","百度千帆","Qianfan","资源包余量"),
            ("xiaomi","小米","MiMo","套餐额度")];
        int index=1;
        foreach(var entry in entries) {
            models.RowStyles.Add(new(SizeType.AutoSize));
            var check=new CheckBox{Text=entry.Chinese,Name="provider-"+entry.Id,AccessibleName=entry.Chinese+" "+entry.English,
                Checked=device.Providers.Contains(entry.Id),AutoSize=true,Dock=DockStyle.Fill,Margin=new Padding(8,4,4,4)};
            var english=Cell(entry.English);var detail=Cell(entry.Quota);
            providers.Add(entry.Id,check);models.Controls.Add(check,0,index);models.Controls.Add(english,1,index);models.Controls.Add(detail,2,index);
            // English names and descriptions toggle the same checkbox as the Chinese name.
            english.Click+=(_,_)=>{if(check.Enabled)check.Checked=!check.Checked;};detail.Click+=(_,_)=>{if(check.Enabled)check.Checked=!check.Checked;};
            index++;
        }
        body.Controls.Add(models);
        if(device.Kind==HardwareKind.Esp8266){var note=Note("小屏轮播中已选的模型也会采集。");note.Margin=new Padding(0,8,0,0);body.Controls.Add(note);}
        void UpdateQuotaOptions() {
            models.Enabled=sources["quotas"].Checked;
            relation.Text=models.Enabled?"下方选择“模型额度”的国产模型；Codex 额度自动采集。":"先勾选上方“模型额度”即可启用；已选模型会保留。";
        }
        sources["quotas"].CheckedChanged+=(_,_)=>UpdateQuotaOptions();UpdateQuotaOptions();
        var footer=new FlowLayoutPanel{AutoSize=true,Dock=DockStyle.Fill,FlowDirection=FlowDirection.RightToLeft,Margin=new Padding(0,12,0,0),Name="data-actions"};
        var cancel=DeviceCenterForm.Button("取消",()=>DialogResult=DialogResult.Cancel);cancel.Name="cancel-data";
        var save=DeviceCenterForm.Button("保存",()=>{
            try {
                var current=store.Snapshot.Devices.Single(d=>d.Id==id);
                store.Update(current with {Sources=DeviceRegistryStore.SourceIds.Where(key=>sources[key].Checked).ToArray(),
                    Providers=DeviceRegistryStore.ProviderIds.Where(key=>providers[key].Checked).ToArray()});
                DialogResult=DialogResult.OK;
            }catch(Exception ex){MessageBox.Show(this,ex.Message,"数据设置未保存");}
        },true);save.Name="save-data";
        footer.Controls.Add(cancel);footer.Controls.Add(save);root.Controls.Add(footer,0,1);Controls.Add(root);
        AcceptButton=save;CancelButton=cancel;SettingsWindow.FitFlow(body);SettingsWindow.FitScreen(this);ResumeLayout(true);
    }
}
