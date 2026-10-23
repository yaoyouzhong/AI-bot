using System.Drawing.Drawing2D;
namespace AIBotBridge;

internal sealed class TransportStatusLabel(int symbol):Label
{
    internal bool ShowSymbol {get;set;}
    protected override void OnPaint(PaintEventArgs e){
        base.OnPaint(e);if(!ShowSymbol)return;
        var g=e.Graphics;var saved=g.Save();
        try{
            g.SmoothingMode=SmoothingMode.AntiAlias;
            float tile=Font.Height+8,y=(Height-tile)/2f;
            using var background=new SolidBrush(Color.FromArgb(0x20,0x27,0x2a));g.FillRectangle(background,0,y,tile,tile);
            Color tint=ForeColor==Color.FromArgb(24,123,72)?Color.FromArgb(0xa8,0xd6,0xbb):ForeColor==Color.FromArgb(162,112,25)?Color.FromArgb(0xe6,0xb6,0x5c):Color.FromArgb(0x68,0x78,0x7f);
            using var path=Tab5TransportGlyphs.Create(symbol);var bounds=path.GetBounds();float scale=(tile-10)/Math.Max(bounds.Width,bounds.Height);
            using var matrix=new Matrix(scale,0,0,-scale,(tile-bounds.Width*scale)/2-bounds.X*scale,y+(tile+bounds.Height*scale)/2+bounds.Y*scale);
            path.Transform(matrix);using var ink=new SolidBrush(tint);g.FillPath(ink,path);
        }finally{g.Restore(saved);}
    }
}
