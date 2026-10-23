namespace AIBotBridge;

internal static class UnlockSettingsStyle
{
    internal static void Apply(Form form,int height)
    {
        form.SuspendLayout();
        form.Font=SettingsWindow.BodyFont();form.ForeColor=SettingsWindow.Ink;form.BackColor=Color.White;
        form.AutoScaleMode=AutoScaleMode.Dpi;form.AutoScaleDimensions=new(96,96);
        form.ClientSize=new(480,height);form.MinimumSize=new(496,height+39);form.MaximizeBox=false;
        form.StartPosition=FormStartPosition.CenterParent;form.KeyPreview=true;
        form.KeyDown+=(_,e)=>{if(e.KeyCode==Keys.Escape){e.SuppressKeyPress=true;form.Close();}};
    }
    internal static void Complete(Form form){form.ResumeLayout(true);SettingsWindow.FitScreen(form);}
    internal static TableLayoutPanel Footer()
    {
        var row=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=3,RowCount=1,Margin=Padding.Empty};
        row.ColumnStyles.Add(new(SizeType.AutoSize));row.ColumnStyles.Add(new(SizeType.Percent,100));row.ColumnStyles.Add(new(SizeType.AutoSize));
        row.Paint+=(_,e)=>{using var pen=new Pen(Color.FromArgb(231,235,241));e.Graphics.DrawLine(pen,0,0,row.ClientSize.Width,0);};return row;
    }
    internal static Button Action(string name,string text,int width,bool primary=false)=>new UnlockActionButton(primary){Name=name,Text=text,AutoSize=false,MinimumSize=new(width,40),Size=new(width,40),Anchor=AnchorStyles.Bottom|AnchorStyles.Right,Margin=Padding.Empty};
    private sealed class UnlockActionButton:Button
    {
        private readonly bool primary;private bool hover;
        internal UnlockActionButton(bool primary){this.primary=primary;SettingsWindow.StyleButton(this,primary);SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);}
        protected override void OnMouseEnter(EventArgs e){hover=true;base.OnMouseEnter(e);Invalidate();}
        protected override void OnMouseLeave(EventArgs e){hover=false;base.OnMouseLeave(e);Invalidate();}
        protected override void OnPaint(PaintEventArgs e)
        {
            Color fill=!Enabled?SettingsWindow.Surface:primary?(hover?Color.FromArgb(29,95,190):SettingsWindow.Accent):hover?SettingsWindow.Surface:Color.White;
            using var brush=new SolidBrush(fill);e.Graphics.FillRectangle(brush,ClientRectangle);
            using var pen=new Pen(primary&&Enabled?fill:SettingsWindow.Border);e.Graphics.DrawRectangle(pen,0,0,Width-1,Height-1);
            TextRenderer.DrawText(e.Graphics,Text,Font,ClientRectangle,!Enabled?SettingsWindow.Muted:primary?Color.White:ForeColor,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine|TextFormatFlags.NoPadding);
            if(Focused&&ShowFocusCues)ControlPaint.DrawFocusRectangle(e.Graphics,Rectangle.Inflate(ClientRectangle,-4,-4));
        }
    }
}
