namespace AIBotBridge;

// Coordinates use TFT center/radius conventions, not GDI ellipse bounds.
internal sealed class WeatherCanvas(Graphics g)
{
    internal void fillRect(int x,int y,int w,int h,Color c) { using var b=new SolidBrush(c); g.FillRectangle(b,x,y,w,h); }
    internal void fillEllipse(int x,int y,int rx,int ry,Color c) { using var b=new SolidBrush(c); g.FillEllipse(b,x-rx,y-ry,rx*2+1,ry*2+1); }
    internal void fillCircle(int x,int y,int r,Color c)=>fillEllipse(x,y,r,r,c);
    internal void drawLine(int x,int y,int x2,int y2,Color c) { using var p=new Pen(c); g.DrawLine(p,x,y,x2,y2); }
    internal void drawFastHLine(int x,int y,int w,Color c)=>fillRect(x,y,w,1,c);
    internal void drawFastVLine(int x,int y,int h,Color c)=>fillRect(x,y,1,h,c);
    internal void drawPixel(int x,int y,Color c)=>fillRect(x,y,1,1,c);
    internal void drawRect(int x,int y,int w,int h,Color c) { using var p=new Pen(c); g.DrawRectangle(p,x,y,w-1,h-1); }
    internal void fillRoundRect(int x,int y,int w,int h,int r,Color c) { using var b=new SolidBrush(c); g.FillRoundedRectangle(b,new Rectangle(x,y,w,h),r); }
    internal void fillTriangle(int x,int y,int x2,int y2,int x3,int y3,Color c) { using var b=new SolidBrush(c); g.FillPolygon(b,new Point[]{new(x,y),new(x2,y2),new(x3,y3)}); }
    internal void setTextColor(Color foreground,Color background) { } // No text in weather animations.
    internal void drawArc(int x,int y,int outer,int inner,int start,int end,Color c,Color background)
    {
        using var p=new Pen(c,outer-inner);
        g.DrawArc(p,x-outer,y-outer,outer*2,outer*2,start,end-start);
    }
}
