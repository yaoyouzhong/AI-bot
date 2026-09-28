namespace AIBotBridge;

internal sealed class LegacyDeviceSettingsForm : Form
{
    internal LegacyDeviceSettingsForm() {
        var settings=BridgeSettings.Load();Text="ESP8266 · 连接与屏保";Font=new Font("Microsoft YaHei UI",9F);AutoScaleDimensions=new(96,96);AutoScaleMode=AutoScaleMode.Dpi;
        ClientSize=new(460,285);MinimumSize=new(400,260);StartPosition=FormStartPosition.CenterParent;
        var panel=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,AutoScroll=true,Padding=new Padding(18)};Controls.Add(panel);SettingsWindow.FitFlow(panel);
        panel.Controls.Add(new Label{Text="USB 串口（留空自动查找已绑定设备）",AutoSize=true});
        var port=new TextBox{Text=settings.Get("serial_port"),CharacterCasing=CharacterCasing.Upper};panel.Controls.Add(port);
        panel.Controls.Add(new Label{Text="屏保等待（分钟，0 为关闭）",AutoSize=true,Margin=new Padding(3,12,3,3)});
        var saver=new NumericUpDown{Minimum=0,Maximum=1440,Value=int.TryParse(settings.Get("screensaver_timeout_minutes"),out int minutes)?Math.Clamp(minutes,0,1440):0};panel.Controls.Add(saver);
        panel.Controls.Add(new Label{Text="串口更改后，停用并重新启用此设备生效。",AutoSize=true,ForeColor=Color.DimGray});
        var row=DeviceCenterForm.Row();row.Controls.Add(DeviceCenterForm.Button("保存",()=> {
            string value=port.Text.Trim().ToUpperInvariant();
            if(value.Length>0&&!(value.Length>3&&value.StartsWith("COM")&&value[3..].All(char.IsAsciiDigit))){MessageBox.Show(this,"请输入 COM 后跟数字，或留空。");return;}
            if(!settings.SaveEditable(new Dictionary<string,string>{["serial_port"]=value,["screensaver_timeout_minutes"]=((int)saver.Value).ToString()},out string error)){MessageBox.Show(this,error);return;}
            DialogResult=DialogResult.OK;
        },true));var cancel=DeviceCenterForm.Button("取消",Close);row.Controls.Add(cancel);CancelButton=cancel;panel.Controls.Add(row);SettingsWindow.FitScreen(this);
    }
}
