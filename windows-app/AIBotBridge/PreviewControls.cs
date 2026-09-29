using System.Drawing.Drawing2D;

namespace AIBotBridge;

internal sealed class PreviewButton : Button
{
    internal string Glyph {get;init;}="";
    internal bool Active {get;set;}
    private bool _hover,_pressed;
    internal PreviewButton(){FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;Cursor=Cursors.Hand;SetStyle(ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer,true);}
    protected override void OnMouseEnter(EventArgs e){_hover=true;Invalidate();base.OnMouseEnter(e);}
    protected override void OnMouseLeave(EventArgs e){_hover=false;Invalidate();base.OnMouseLeave(e);}
    protected override void OnMouseDown(MouseEventArgs e){if(e.Button==MouseButtons.Left){_pressed=true;Invalidate();Update();}base.OnMouseDown(e);}
    protected override void OnMouseUp(MouseEventArgs e){_pressed=false;Invalidate();base.OnMouseUp(e);}
    protected override void OnMouseCaptureChanged(EventArgs e){_pressed=false;Invalidate();base.OnMouseCaptureChanged(e);}
    protected override void OnKeyDown(KeyEventArgs e){if(e.KeyCode==Keys.Space){_pressed=true;Invalidate();}base.OnKeyDown(e);}
    protected override void OnKeyUp(KeyEventArgs e){_pressed=false;Invalidate();base.OnKeyUp(e);}
    protected override void OnLostFocus(EventArgs e){_pressed=false;Invalidate();base.OnLostFocus(e);}
    internal static GraphicsPath Round(RectangleF r,float radius)
    {
        var path=new GraphicsPath();float d=radius*2;
        path.AddArc(r.X,r.Y,d,d,180,90);path.AddArc(r.Right-d,r.Y,d,d,270,90);
        path.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);path.AddArc(r.X,r.Bottom-d,d,d,90,90);path.CloseFigure();return path;
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        var g=e.Graphics;float s=DeviceDpi/96f;g.SmoothingMode=SmoothingMode.AntiAlias;g.Clear(Parent?.BackColor??BackColor);
        bool pressed=Enabled&&_pressed;
        var ink=!Enabled?SystemColors.GrayText:Active?Color.White:Color.FromArgb(45,57,74);
        float inset=pressed?s:0;
        using var shape=Round(new RectangleF(inset,inset,Width-1-inset*2,Height-1-inset*2),8*s);
        using var fill=new SolidBrush(Active?(pressed?Color.FromArgb(37,74,142):_hover?Color.FromArgb(66,108,181):Color.FromArgb(49,91,166)):pressed?Color.FromArgb(209,219,234):_hover?Color.FromArgb(230,234,240):Glyph is "close" or "previous" or "next"?Parent?.BackColor??BackColor:Color.White);
        g.FillPath(fill,shape);
        using var pen=new Pen(ink,1.4f*s){StartCap=LineCap.Round,EndCap=LineCap.Round};
        float cx=Width/2f,cy=Height/2f;
        if(Glyph is "previous" or "next") {float sign=Glyph=="next"?1:-1;g.DrawLines(pen,new PointF[]{new(cx-sign*2*s,cy-4*s),new(cx+sign*2*s,cy),new(cx-sign*2*s,cy+4*s)});}
        else if(Glyph=="close"){g.DrawLine(pen,cx-3*s,cy-3*s,cx+3*s,cy+3*s);g.DrawLine(pen,cx+3*s,cy-3*s,cx-3*s,cy+3*s);}
        else {
            int left=(int)(Glyph=="auto"?26*s:10*s),right=(int)((Glyph=="pages"?24:10)*s);
            if(Glyph=="auto"){float x=14*s;if(Active)g.DrawLines(pen,new PointF[]{new(x-4*s,cy),new(x-s,cy+3*s),new(x+5*s,cy-4*s)});else{g.DrawArc(pen,x-5*s,cy-5*s,10*s,10*s,30,280);g.DrawLines(pen,new PointF[]{new(x+1*s,cy-7*s),new(x+5*s,cy-5*s),new(x+3*s,cy-1*s)});}}
            if(Glyph=="pages"){float x=Width-14*s;g.DrawLines(pen,new PointF[]{new(x-3*s,cy-1*s),new(x,cy+2*s),new(x+3*s,cy-1*s)});}
            using var font=new Font("Microsoft YaHei UI",12*s,Active?FontStyle.Bold:FontStyle.Regular,GraphicsUnit.Pixel);
            TextRenderer.DrawText(g,Text,font,new Rectangle(left,0,Math.Max(1,Width-left-right),Height),ink,TextFormatFlags.VerticalCenter|TextFormatFlags.HorizontalCenter|TextFormatFlags.SingleLine|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPadding);
        }
        if(Focused&&ShowFocusCues){using var focus=new Pen(Color.FromArgb(133,157,194),s);g.DrawPath(focus,shape);}
    }
}

internal sealed class PreviewBrightness : Control
{
    internal int Minimum=>0;
    internal int Maximum=>100;
    private int _value=100;
    internal int Value {get=>_value;set{_value=Math.Clamp(value,0,100);Invalidate();}}
    internal event EventHandler? Scroll;
    internal event EventHandler? Committed;
    internal PreviewBrightness(){Cursor=Cursors.Hand;TabStop=true;AccessibleName="屏幕亮度";AccessibleRole=AccessibleRole.Slider;SetStyle(ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.Selectable,true);}
    private void SetFromX(int x){float pad=8*DeviceDpi/96f;Value=(int)Math.Round((x-pad)/Math.Max(1,Width-pad*2)*100);Scroll?.Invoke(this,EventArgs.Empty);}
    protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);if(e.Button==MouseButtons.Left){Focus();Capture=true;SetFromX(e.X);}}
    protected override void OnMouseMove(MouseEventArgs e){base.OnMouseMove(e);if(Capture)SetFromX(e.X);}
    protected override void OnMouseUp(MouseEventArgs e){if(Capture){SetFromX(e.X);Capture=false;Committed?.Invoke(this,EventArgs.Empty);}base.OnMouseUp(e);}
    protected override bool IsInputKey(Keys keyData)=>(keyData&Keys.KeyCode) is Keys.Left or Keys.Right or Keys.Up or Keys.Down or Keys.Home or Keys.End||base.IsInputKey(keyData);
    protected override void OnKeyDown(KeyEventArgs e){int value=e.KeyCode switch{Keys.Left or Keys.Down=>Value-1,Keys.Right or Keys.Up=>Value+1,Keys.Home=>0,Keys.End=>100,_=>Value};if(value!=Value){Value=value;Scroll?.Invoke(this,EventArgs.Empty);Committed?.Invoke(this,EventArgs.Empty);e.Handled=true;}base.OnKeyDown(e);}
    protected override void OnPaint(PaintEventArgs e)
    {
        var g=e.Graphics;g.Clear(Parent?.BackColor??BackColor);g.SmoothingMode=SmoothingMode.AntiAlias;float s=DeviceDpi/96f,pad=8*s,y=Height/2f,x=pad+(Width-2*pad)*Value/100f;
        using var track=new Pen(Color.FromArgb(215,222,232),3*s){StartCap=LineCap.Round,EndCap=LineCap.Round};g.DrawLine(track,pad,y,Width-pad,y);
        using var fill=new Pen(Color.FromArgb(75,115,187),3*s){StartCap=LineCap.Round,EndCap=LineCap.Round};if(Value>0)g.DrawLine(fill,pad,y,x,y);
        g.FillEllipse(Brushes.White,x-5*s,y-5*s,10*s,10*s);using var edge=new Pen(Color.FromArgb(100,122,157),s);g.DrawEllipse(edge,x-5*s,y-5*s,10*s,10*s);
        if(Focused)ControlPaint.DrawFocusRectangle(g,Rectangle.Inflate(ClientRectangle,-2,-2));
    }
}
