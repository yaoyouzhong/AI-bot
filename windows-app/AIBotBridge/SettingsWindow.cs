namespace AIBotBridge;

internal static class SettingsWindow
{
    internal static readonly Color Ink=Color.FromArgb(32,43,59),Muted=Color.FromArgb(88,101,119),Accent=Color.FromArgb(34,109,215),Surface=Color.FromArgb(246,248,251),Border=Color.FromArgb(211,218,228);
    internal static Font BodyFont()=>new("Microsoft YaHei UI",9.5f);
    internal static Font TitleFont()=>new("Microsoft YaHei UI",17,FontStyle.Bold);
    internal static Font SectionFont()=>new("Microsoft YaHei UI",10.5f,FontStyle.Bold);
    private sealed class ButtonStyle {internal bool Primary;internal bool Listening;}
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Button,ButtonStyle> ButtonStyles=new();
    internal static Label Title(string text)=>new(){Name="page-title",Text=text,AutoSize=true,Font=TitleFont(),ForeColor=Ink,Margin=new(0,0,0,16)};
    internal static void AddTitle(TableLayoutPanel layout,string title)
    {
        layout.SuspendLayout();
        foreach(Control control in layout.Controls)layout.SetRow(control,layout.GetRow(control)+1);
        layout.RowCount++;layout.RowStyles.Insert(0,new(SizeType.AutoSize));
        var heading=Title(title);layout.Controls.Add(heading,0,0);layout.SetColumnSpan(heading,layout.ColumnCount);layout.ResumeLayout(true);
    }
    internal static Form? DialogOwner(Form? form)=>form is {IsDisposed:false,Disposing:false,Visible:true}?form:null;
    internal static void Present(Form form)
    {
        if (form.WindowState == FormWindowState.Minimized) form.WindowState = FormWindowState.Normal;
        if (!form.Visible) form.Show();
        // A hidden tray launch can supply SW_HIDE through STARTUPINFO. The
        // first native ShowWindow honors that startup value, so explicitly
        // show again after WinForms has created/shown this user-requested form.
        ShowWindow(form.Handle, 5 /* SW_SHOW: preserve maximized size */);
        form.BringToFront();
        form.Activate();
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr window, int command);
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    internal static extern bool IsWindowVisible(IntPtr window);

    internal static void StyleButton(Button button, bool primary = false)
    {
        var style=ButtonStyles.GetOrCreateValue(button);style.Primary=primary;
        button.AutoSize = true;
        button.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        button.MinimumSize = new Size(64, 30);
        button.Size = new Size(64, 30);
        button.Padding = new Padding(8, 0, 8, 0);
        button.Cursor = Cursors.Hand;
        button.UseVisualStyleBackColor=false;
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderColor=Border;
        button.FlatAppearance.MouseOverBackColor=primary?Color.FromArgb(29,95,190):Surface;
        button.FlatAppearance.MouseDownBackColor=primary?Color.FromArgb(24,81,163):Color.FromArgb(233,242,255);
        void Refresh(){bool active=style.Primary&&button.Enabled;button.BackColor=active?Accent:button.Enabled?Color.White:Surface;button.ForeColor=active?Color.White:button.Enabled?Ink:Muted;button.FlatAppearance.BorderSize=active?0:1;}
        if(!style.Listening){style.Listening=true;button.EnabledChanged+=(_,_)=>Refresh();}Refresh();
    }

    private static void StyleControls(Control parent)
    {
        foreach(Control child in parent.Controls) {
            if(child.GetType()==typeof(Button)) {
                var button=(Button)child;
                StyleButton(button,ButtonStyles.TryGetValue(button,out var style)?style.Primary:button.FlatStyle==FlatStyle.Flat&&button.ForeColor==Color.White);
            }
            if(child is LinkLabel link)link.LinkColor=Accent;
            if(child is DataGridView grid) {
                grid.BackgroundColor=Color.White;grid.BorderStyle=BorderStyle.None;grid.GridColor=Color.FromArgb(235,239,245);
                grid.EnableHeadersVisualStyles=false;grid.ColumnHeadersBorderStyle=DataGridViewHeaderBorderStyle.Single;
                grid.CellBorderStyle=DataGridViewCellBorderStyle.SingleHorizontal;
                grid.ColumnHeadersDefaultCellStyle.BackColor=grid.ColumnHeadersDefaultCellStyle.SelectionBackColor=Surface;
                grid.ColumnHeadersDefaultCellStyle.ForeColor=grid.ColumnHeadersDefaultCellStyle.SelectionForeColor=Muted;
                grid.DefaultCellStyle.BackColor=Color.White;grid.DefaultCellStyle.ForeColor=Ink;
                grid.DefaultCellStyle.SelectionBackColor=Color.FromArgb(233,242,255);grid.DefaultCellStyle.SelectionForeColor=Ink;
                grid.AlternatingRowsDefaultCellStyle.BackColor=Surface;
            }
            StyleControls(child);
        }
    }

    internal static void FitScreen(Form form)
    {
        if(form is not MirrorForm) {form.BackColor=Color.White;form.ForeColor=Ink;}
        StyleControls(form);
        if(form.StartPosition!=FormStartPosition.Manual)form.StartPosition=FormStartPosition.CenterScreen;
        // Called once after WinForms has applied DPI scaling. Use the actual
        // monitor work area, so the taskbar and bottom actions stay reachable.
        form.Shown += (_, _) => {
            var area = (form.StartPosition==FormStartPosition.Manual?Screen.FromControl(form):Screen.FromPoint(Cursor.Position)).WorkingArea;
            int width = Math.Max(320, area.Width - 32), height = Math.Max(240, area.Height - 32);
            form.MinimumSize = new Size(Math.Min(form.MinimumSize.Width,width), Math.Min(form.MinimumSize.Height,height));
            form.Size = new Size(Math.Min(form.Width,width),Math.Min(form.Height,height));
            if(form.StartPosition!=FormStartPosition.Manual)
                form.Location=new Point(area.Left+(area.Width-form.Width)/2,area.Top+(area.Height-form.Height)/2);
        };
    }

    // A vertical, scrollable layout must follow the viewport, not its virtual
    // scroll extent. Reserve the scrollbar gutter even before it appears.
    internal static void FitFlow(FlowLayoutPanel panel)
    {
        bool arranging = false,queued=false;
        void Fit()
        {
            if (arranging || panel.IsDisposed) return;
            arranging = true;
            try
            {
                int width = Math.Max(100, panel.Width - panel.Padding.Horizontal - SystemInformation.VerticalScrollBarWidth - 4);
                foreach (Control child in panel.Controls)
                {
                    int available = Math.Max(80, width-child.Margin.Horizontal);
                    if (child is Label label) { if(label.MaximumSize.Width!=available)label.MaximumSize = new Size(available, 0); }
                    else if (child is TableLayoutPanel or FlowLayoutPanel) {
                        if(child.MinimumSize.Width!=available)child.MinimumSize = new Size(available, 0);
                        if(child.MaximumSize.Width!=available)child.MaximumSize = new Size(available, 0);
                        if(child.Width!=available)child.Width = available;
                    }
                    else if (child is not Button&&child.Width!=available) child.Width = available;
                }
            }
            finally { arranging = false; }
        }
        panel.SizeChanged += (_, _) => Fit();
        // Parent scaling can resize children after SizeChanged. Fit once more
        // after that layout completes, using the final viewport and padding.
        panel.Layout+=(_,_)=>{
            if(arranging||queued||!panel.IsHandleCreated||panel.IsDisposed)return;queued=true;
            panel.BeginInvoke((Action)(()=>{queued=false;if(!panel.IsDisposed)Fit();}));
        };
        panel.ControlAdded += (_, _) => Fit();
        panel.HandleCreated += (_, _) => Fit();
        Fit();
    }
}
