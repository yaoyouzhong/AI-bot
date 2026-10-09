using System.Net;
using System.Text;
using System.Text.Json;
namespace AIBotBridge;

// Render the production update window with synthetic releases; no installation or device access.
internal static class UpdateLayoutCapture
{
    internal static void Run(string output,bool retained=false)
    {
        string bridge=retained?"0.6.2":ReleaseMedia.BridgeVersion,tab5=retained?"0.2.150-ui":ReleaseMedia.Tab5Version;
        string releases=JsonSerializer.Serialize(new[]{
            UpdateSelfTest.Release("bridge",bridge,$"AIBotBridge-{bridge}-setup-win-x64.exe",[]),
            UpdateSelfTest.Release("tab5",tab5,$"TAB5-upgrade-{tab5}.zip",[]),
            UpdateSelfTest.Release("esp8266",ReleaseMedia.EspVersion,$"AI-bot-{ReleaseMedia.EspVersion}-firmware-materials.zip",[])});
        using var service=new UpdateService(new UpdateSelfTest.Handler{Reply=(_,_)=>Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(releases,Encoding.UTF8,"application/json")})});
        UpdateDevice[] devices=[new("bridge","电脑端 AI-bot",retained?ReleaseMedia.BridgeVersion:"0.6.1","运行中",true,"bridge","安装程序",true),
            new("tab","M5Stack TAB5",ReleaseMedia.Tab5Version,"在线",true,"upgrade-tab5","在设备上确认安装",true),
            new("esp","ESP8266 小屏",retained?ReleaseMedia.EspVersion:"0.5.0","在线",true,"flash","连接 USB，先备份再升级",true)];
        foreach(float scale in new[]{1f,1.5f,2f})
        foreach(bool minimum in new[]{false,true}) {
            using var form=new UpdateCenterForm(()=>devices,(_,_)=>throw new InvalidOperationException("Preview must not install"),service);
            form.StartPosition=FormStartPosition.Manual;form.Location=new(-30000,-30000);form.ShowInTaskbar=false;
            form.Show();Application.DoEvents();
            var deadline=DateTime.UtcNow.AddSeconds(10);
            while(service.CheckedAt is null){if(DateTime.UtcNow>deadline)throw new TimeoutException();Application.DoEvents();Thread.Sleep(10);}
            if(scale!=1)form.Scale(new SizeF(scale,scale));
            if(minimum)form.Size=form.MinimumSize;
            var grid=(DataGridView)form.Controls.Find("updates",true).Single();
            for(int row=0;row<devices.Length;row++) {
                grid.CurrentCell=grid[0,row];form.PerformLayout();Application.DoEvents();
                AssertVisible(form);
                var action=form.Controls.Find("download",true).Single();
                if(row==1&&action.Visible||retained&&row==0&&action.Visible)throw new InvalidOperationException("Retained version advertises an upgrade");
                int gap=form.Controls.Find("update-actions",true).Single().RectangleToScreen(form.Controls.Find("update-actions",true).Single().ClientRectangle).Top-grid.RectangleToScreen(grid.ClientRectangle).Bottom;
                if(gap>24*scale)throw new InvalidOperationException("Grid leaves unused space before actions");
                var footer=form.Controls.Find("update-footer",true).Single();
                if(form.RectangleToScreen(form.ClientRectangle).Bottom-footer.RectangleToScreen(footer.ClientRectangle).Bottom>footer.Parent!.Padding.Bottom+2)throw new InvalidOperationException("Footer leaves unused vertical space");
                using var bitmap=new Bitmap(form.Width,form.Height);form.DrawToBitmap(bitmap,new(Point.Empty,form.Size));
                bitmap.Save(Path.Combine(output,$"updates-{devices[row].Component}-{scale:0.0}{(minimum?"-minimum":"")}.png"));
            }
            form.Close();
        }
        UpdateNotesSelfTest.Run(output);
        Console.WriteLine("UPDATE_LAYOUT_OK production window; three selected components, default/minimum sizes, 100/150/200 percent; visible controls and version/status cells fit; synthetic metadata only");
    }
    private static void AssertVisible(Form form)
    {
        foreach(var control in Descendants(form).Where(c=>c.Visible&&c is Button or CheckBox or Label)) {
            var bounds=control.RectangleToScreen(control.ClientRectangle);
            for(Control? parent=control.Parent;parent is not null;parent=parent.Parent) {
                var clip=parent.RectangleToScreen(parent.ClientRectangle);clip.Inflate(2,2);
                if(!clip.Contains(bounds))throw new InvalidOperationException($"Clipped {control.Text} {bounds} by {clip}");
            }
        }
        var grid=(DataGridView)form.Controls.Find("updates",true).Single();
        foreach(DataGridViewRow row in grid.Rows)foreach(DataGridViewCell cell in row.Cells) {
            var style=cell.InheritedStyle;
            int width=TextRenderer.MeasureText(cell.Value?.ToString()??"",style.Font,Size.Empty,TextFormatFlags.SingleLine).Width+style.Padding.Horizontal;
            if(width>cell.OwningColumn!.Width)throw new InvalidOperationException($"Clipped cell {cell.Value}: needs {width}, column {cell.OwningColumn.Width}; window {form.Size}, minimum {form.MinimumSize}, DPI {form.DeviceDpi}");
        }
    }
    private static IEnumerable<Control> Descendants(Control parent){foreach(Control child in parent.Controls){yield return child;foreach(var next in Descendants(child))yield return next;}}
}
