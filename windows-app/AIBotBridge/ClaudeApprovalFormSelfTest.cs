using System.Text.Json;
namespace AIBotBridge;

internal static class ClaudeApprovalFormSelfTest
{
    internal static void Run(string output){
        Directory.CreateDirectory(output);
        var approval=new ClaudeWorkspace.Approval("synthetic-request","synthetic-task","Read",JsonSerializer.SerializeToElement(new{file_path="合成项目/说明.txt",reason="这是隔离审批预览，不执行任何工具。",lines=Enumerable.Range(1,30).Select(n=>"合成参数 "+n).ToArray()}));
        foreach(var size in new[]{new Size(720,440),new Size(560,360)}){
            using var form=new ClaudeApprovalForm(approval){StartPosition=FormStartPosition.Manual,Location=new(-30000,-30000),ShowInTaskbar=false,ClientSize=size};
            form.Show();Application.DoEvents();form.PerformLayout();
            var controls=Descendants(form).ToArray();var text=controls.OfType<TextBox>().Single();
            if(form.AcceptButton is not Button{DialogResult:DialogResult.No}||form.CancelButton is not Button{DialogResult:DialogResult.Cancel}||!text.ReadOnly||!text.Text.Contains("合成项目")||text.Height<100||text.Width<450)throw new InvalidOperationException("Approval rendering or safe defaults failed");
            using var image=new Bitmap(form.Width,form.Height);form.DrawToBitmap(image,new(Point.Empty,form.Size));image.Save(Path.Combine(output,$"claude-approval-{size.Width}.png"));
            form.Close();
        }
        Console.WriteLine("CLAUDE_APPROVAL_NATIVE_UI_OK readable parameters, normal/minimum layout; default deny, cancel defers; no tool decision sent");
    }
    private static IEnumerable<Control> Descendants(Control root){foreach(Control child in root.Controls){yield return child;foreach(var nested in Descendants(child))yield return nested;}}
}
