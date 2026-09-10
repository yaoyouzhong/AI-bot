namespace AIBotBridge;

// Original geometry shared with firmware drawPixelPetBody; no image or cache required.
internal static class ByteSproutRenderer
{
    internal static void Draw(Graphics g, int x, int y, bool working, long milliseconds)
    {
        using var shell = new SolidBrush(working ? Color.Cyan : Color.FromArgb(123,125,123));
        using var face = new SolidBrush(Color.Black);
        using var eye = new SolidBrush(working ? Color.Lime : Color.LightGray);
        var saved = g.Save();
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;
        g.FillRectangle(shell,x+27,y,4,10);g.FillRectangle(shell,x+23,y,12,4);
        Round(x+8,y+10,42,35);g.FillRectangle(face,x+14,y+17,30,19);
        g.FillRectangle(eye,x+20,y+23,5,6);g.FillRectangle(eye,x+34,y+23,5,6);
        Round(x+12,y+48,34,42);g.FillRectangle(face,x+20,y+59,18,5);
        g.FillRectangle(shell,x+5,y+54,7,27);g.FillRectangle(shell,x+46,y+54,7,27);
        bool step=working && (milliseconds/240)%2==1;
        g.FillRectangle(shell,x+(step?6:15),y+90,17,8);
        g.FillRectangle(shell,x+(step?32:41),y+90,17,8);
        g.Restore(saved);
        void Round(int left,int top,int width,int height)
        {
            g.FillRectangle(shell,left+5,top,width-10,height);
            g.FillRectangle(shell,left,top+5,width,height-10);
            foreach(var corner in new[]{(left,top),(left+width-10,top),(left,top+height-10),(left+width-10,top+height-10)})
                g.FillEllipse(shell,corner.Item1,corner.Item2,10,10);
        }
    }
}
