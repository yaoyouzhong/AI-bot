namespace AIBotBridge;

internal sealed class DeviceActionButton : Button
{
    private readonly string _description;
    internal int PreferredCardHeight=>Font.Height*2+Math.Max(20,Font.Height*2/3);
    internal int PreferredCardWidth {
        get {
            using var title=new Font(Font,FontStyle.Bold);
            return Math.Max(TextRenderer.MeasureText(Text,title).Width,TextRenderer.MeasureText(_description,Font).Width)+HorizontalInset*2;
        }
    }
    private int HorizontalInset=>Math.Max(10,Font.Height/2);
    internal DeviceActionButton(string title,string description){Text=title;AccessibleName=title;AccessibleDescription=description;_description=description;AutoSize=false;Cursor=Cursors.Hand;FlatStyle=FlatStyle.Flat;Height=PreferredCardHeight;}
    protected override void OnFontChanged(EventArgs e){base.OnFontChanged(e);Height=PreferredCardHeight;}
    protected override void OnPaint(PaintEventArgs e){
        e.Graphics.Clear(Enabled?SettingsWindow.Surface:Color.FromArgb(249,249,249));
        using var border=new Pen(Focused?SettingsWindow.Accent:SettingsWindow.Border);
        e.Graphics.DrawRectangle(border,0,0,Math.Max(0,Width-1),Math.Max(0,Height-1));
        using var title=new Font(Font,FontStyle.Bold);
        int inset=HorizontalInset,top=Math.Max(6,(Height-Font.Height*2-4)/2),width=Math.Max(1,Width-inset*2);
        TextRenderer.DrawText(e.Graphics,Text,title,new Rectangle(inset,top,width,Font.Height+2),Enabled?SettingsWindow.Ink:Color.Gray,TextFormatFlags.SingleLine|TextFormatFlags.EndEllipsis);
        TextRenderer.DrawText(e.Graphics,_description,Font,new Rectangle(inset,top+Font.Height+4,width,Font.Height+2),SettingsWindow.Muted,TextFormatFlags.SingleLine|TextFormatFlags.EndEllipsis);
    }
}
