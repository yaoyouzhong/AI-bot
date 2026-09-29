namespace AIBotBridge;

internal sealed class DeviceAppearanceForm : Form
{
    internal DeviceAppearanceForm(string animation,Action<string> selectAnimation,Action<string?> importPet,Action<string> restorePet,Action<Form>? browse=null)
    {
        SuspendLayout();Text="ESP8266 · 外观设置";
        AutoScaleDimensions=new(96,96);AutoScaleMode=AutoScaleMode.Dpi;
        Font=new Font("Microsoft YaHei UI",9F);
        ClientSize=new(430,340);MinimumSize=new(410,340);StartPosition=FormStartPosition.CenterScreen;
        MaximizeBox=false;MinimizeBox=false;
        var body=new FlowLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(16),AutoScroll=true,FlowDirection=FlowDirection.TopDown,WrapContents=false};Controls.Add(body);
        Label Heading(string text)=>new(){Text=text,AutoSize=true,Font=new Font(Font,FontStyle.Bold),Margin=new Padding(3,4,3,8)};
        body.Controls.Add(Heading("天气动画"));
        string[] values=["robot","house","plant","pet","off"];
        var choices=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList};
        choices.Items.AddRange(["天气机器人","像素小屋","像素盆栽","天气萌宠","关闭动画"]);
        choices.SelectedIndex=Math.Max(0,Array.IndexOf(values,animation));
        choices.SelectedIndexChanged+=(_,_)=>selectAnimation(values[choices.SelectedIndex]);body.Controls.Add(choices);
        var heading=Heading("桌宠动画");heading.Margin=new Padding(3,16,3,8);body.Controls.Add(heading);
        var rows=new TableLayoutPanel{AutoSize=true,ColumnCount=3,RowCount=3,Margin=Padding.Empty};
        rows.ColumnStyles.Add(new(SizeType.AutoSize));rows.ColumnStyles.Add(new(SizeType.Percent,50));rows.ColumnStyles.Add(new(SizeType.Percent,50));
        var feedback=new Label{AutoSize=true,ForeColor=Color.DimGray,Text="更改后自动保存",Margin=new Padding(3,10,3,3)};
        int row=0;
        foreach(var (label,owner) in new (string,string?)[]{("通用",null),("Claude","claude"),("Codex","codex")}) {
            rows.RowStyles.Add(new(SizeType.AutoSize));
            rows.Controls.Add(new Label{Text=label,AutoSize=true,Anchor=AnchorStyles.Left,Margin=new Padding(3,6,6,6)},0,row);
            var choose=DeviceCenterForm.Button("选择动画",()=>importPet(owner));choose.Anchor=AnchorStyles.Left;choose.Margin=new Padding(4);rows.Controls.Add(choose,1,row);
            if(owner is not null){var reset=DeviceCenterForm.Button("恢复默认",()=>{
                try{restorePet(owner);feedback.ForeColor=Color.ForestGreen;feedback.Text=label+" 已恢复默认动画";}
                catch(Exception ex){feedback.ForeColor=Color.Firebrick;feedback.Text=ex.Message;}
            });reset.Anchor=AnchorStyles.Left;reset.Margin=new Padding(4);rows.Controls.Add(reset,2,row);}
            row++;
        }
        body.Controls.Add(rows);
        if(browse is not null)body.Controls.Add(DeviceCenterForm.Button("素材图库",()=>browse(this)));
        body.Controls.Add(feedback);SettingsWindow.FitFlow(body);SettingsWindow.FitScreen(this);ResumeLayout(true);
    }
}
