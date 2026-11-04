using System.Text.Json;
namespace AIBotBridge;

// Opened only by an explicit tray action. The request stays on this computer.
internal sealed class ClaudeApprovalForm:Form
{
    internal ClaudeApprovalForm(ClaudeWorkspace.Approval approval){
        Text="Claude 操作确认";ClientSize=new(720,440);MinimumSize=new(560,360);StartPosition=FormStartPosition.CenterScreen;Font=SystemFonts.MessageBoxFont;MinimizeBox=false;MaximizeBox=false;
        var title=new Label{Text="Claude 请求使用 "+approval.Tool,AutoSize=false,Dock=DockStyle.Top,Height=44,Padding=new(16,12,16,0)};
        var info=new Label{Text="请核对本次操作参数。允许仅适用于这一次请求。",Dock=DockStyle.Top,Height=36,Padding=new(16,4,16,0)};
        var text=new TextBox{Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Both,WordWrap=false,Dock=DockStyle.Fill,Font=new Font(FontFamily.GenericMonospace,10),Text=JsonSerializer.Serialize(approval.Input,new JsonSerializerOptions{WriteIndented=true,Encoder=System.Text.Encodings.Web.JavaScriptEncoder.Create(System.Text.Unicode.UnicodeRanges.All)})};
        var body=new Panel{Dock=DockStyle.Fill,Padding=new(16,0,16,12)};body.Controls.Add(text);
        var actions=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=56,Padding=new(12,8,12,8),FlowDirection=FlowDirection.RightToLeft};
        var later=new Button{Text="稍后处理",DialogResult=DialogResult.Cancel,AutoSize=true};var allow=new Button{Text="允许本次",DialogResult=DialogResult.Yes,AutoSize=true};var deny=new Button{Text="拒绝",DialogResult=DialogResult.No,AutoSize=true};actions.Controls.AddRange([later,allow,deny]);
        Controls.Add(body);Controls.Add(info);Controls.Add(title);Controls.Add(actions);AcceptButton=deny;CancelButton=later;
        Shown+=(_,_)=>{text.Select(0,0);ActiveControl=deny;};
    }
}
