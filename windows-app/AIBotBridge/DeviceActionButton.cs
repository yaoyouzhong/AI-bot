namespace AIBotBridge;

internal sealed class DeviceActionButton : Button
{
    private string _description;
    internal bool ShowsUpgradeArrow { get; private set; }
    internal void SetUpgradeAvailable(bool value) {
        if(ShowsUpgradeArrow==value)return;
        ShowsUpgradeArrow=value;
        Invalidate();
    }
    internal int PreferredCardHeight=>Font.Height*2+Math.Max(20,Font.Height*2/3);
    internal int PreferredCardWidth {
        get {
            using var title=new Font(Font,FontStyle.Bold);
            return Math.Max(TextRenderer.MeasureText(Text,title).Width,TextRenderer.MeasureText(_description,Font).Width)+HorizontalInset*2;
        }
    }
    private int HorizontalInset=>Math.Max(10,Font.Height/2);
    internal DeviceActionButton(string title,string description){Text=title;AccessibleName=title;AccessibleDescription=description;_description=description;AutoSize=false;Cursor=Cursors.Hand;FlatStyle=FlatStyle.Flat;Height=PreferredCardHeight;}
    internal void SetDescription(string description) {
        if(_description==description)return;
        _description=description;
        AccessibleDescription=description;
        Invalidate();
    }
    protected override void OnFontChanged(EventArgs e){base.OnFontChanged(e);Height=PreferredCardHeight;}
    protected override void OnPaint(PaintEventArgs e){
        e.Graphics.Clear(Enabled?SettingsWindow.Surface:Color.FromArgb(249,249,249));
        using var border=new Pen(Focused?SettingsWindow.Accent:SettingsWindow.Border);
        e.Graphics.DrawRectangle(border,0,0,Math.Max(0,Width-1),Math.Max(0,Height-1));
        using var title=new Font(Font,FontStyle.Bold);
        int inset=HorizontalInset,top=Math.Max(6,(Height-Font.Height*2-4)/2),width=Math.Max(1,Width-inset*2);
        const TextFormatFlags textFlags=TextFormatFlags.SingleLine|TextFormatFlags.EndEllipsis;
        int titleWidth=width;
        if(ShowsUpgradeArrow) {
            float unit=DeviceDpi/96f,size=14*unit,edge=8*unit;
            var badge=new RectangleF(Width-edge-size,edge,size,size);
            int available=(int)(badge.Left-inset-4*unit);
            if(available>0&&badge.Bottom<Height) {
                float x=badge.Left+size/2,tip=badge.Top+size/2-3*unit;
                using var background=new SolidBrush(Color.FromArgb(33,133,74));
                using var arrow=new Pen(Color.White,1.6f*unit) { StartCap=System.Drawing.Drawing2D.LineCap.Round,EndCap=System.Drawing.Drawing2D.LineCap.Round,LineJoin=System.Drawing.Drawing2D.LineJoin.Round };
                e.Graphics.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                e.Graphics.FillEllipse(background,badge);
                e.Graphics.DrawLine(arrow,x,tip,x,tip+6*unit);
                e.Graphics.DrawLines(arrow,new PointF[]{new(x-3*unit,tip+3*unit),new(x,tip),new(x+3*unit,tip+3*unit)});
                titleWidth=Math.Min(width,available);
            }
        }
        TextRenderer.DrawText(e.Graphics,Text,title,new Rectangle(inset,top,titleWidth,Font.Height+2),Enabled?SettingsWindow.Ink:Color.Gray,textFlags);
        TextRenderer.DrawText(e.Graphics,_description,Font,new Rectangle(inset,top+Font.Height+4,width,Font.Height+2),SettingsWindow.Muted,textFlags);
    }
}
