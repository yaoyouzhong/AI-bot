namespace AIBotBridge;

internal sealed class DeviceActionButton : Button
{
    private readonly string _description;
    internal DeviceActionButton(string title,string description){Text=title;AccessibleName=title;AccessibleDescription=description;_description=description;AutoSize=false;Cursor=Cursors.Hand;FlatStyle=FlatStyle.Flat;Height=Font.Height*2+28;}
    protected override void OnFontChanged(EventArgs e){base.OnFontChanged(e);Height=Font.Height*2+28;}
    protected override void OnPaint(PaintEventArgs e){
        e.Graphics.Clear(Enabled?Color.FromArgb(247,249,252):Color.FromArgb(249,249,249));
        using var border=new Pen(Focused?Color.FromArgb(34,109,215):Color.FromArgb(225,230,237));
        e.Graphics.DrawRectangle(border,0,0,Math.Max(0,Width-1),Math.Max(0,Height-1));
        using var title=new Font(Font,FontStyle.Bold);
        TextRenderer.DrawText(e.Graphics,Text,title,new Rectangle(12,10,Math.Max(1,Width-24),Font.Height+4),Enabled?Color.FromArgb(37,50,65):Color.Gray,TextFormatFlags.SingleLine|TextFormatFlags.EndEllipsis);
        TextRenderer.DrawText(e.Graphics,_description,Font,new Rectangle(12,Font.Height+15,Math.Max(1,Width-24),Font.Height+4),Color.DimGray,TextFormatFlags.SingleLine|TextFormatFlags.EndEllipsis);
    }
}
