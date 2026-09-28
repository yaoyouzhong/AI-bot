namespace AIBotBridge;

internal static class SettingsWindow
{
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
        button.AutoSize = true;
        button.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        button.MinimumSize = new Size(64, 30);
        button.Size = new Size(64, 30);
        button.Padding = new Padding(8, 0, 8, 0);
        button.Cursor = Cursors.Hand;
        if (!primary) return;
        button.BackColor = Color.FromArgb(34, 109, 215);
        button.ForeColor = Color.White;
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 0;
    }

    internal static void FitScreen(Form form)
    {
        // Called once after WinForms has applied DPI scaling. Use the actual
        // monitor work area, so the taskbar and bottom actions stay reachable.
        form.Shown += (_, _) => {
            var area = Screen.FromControl(form).WorkingArea;
            int width = Math.Max(320, area.Width - 32), height = Math.Max(240, area.Height - 32);
            form.MinimumSize = new Size(Math.Min(form.MinimumSize.Width,width), Math.Min(form.MinimumSize.Height,height));
            form.Size = new Size(Math.Min(form.Width,width),Math.Min(form.Height,height));
        };
    }

    // A vertical, scrollable layout must follow the viewport, not its virtual
    // scroll extent. Reserve the scrollbar gutter even before it appears.
    internal static void FitFlow(FlowLayoutPanel panel)
    {
        bool arranging = false;
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
                    else if (child is not Button) child.Width = available;
                }
            }
            finally { arranging = false; }
        }
        panel.SizeChanged += (_, _) => Fit();
        panel.ControlAdded += (_, _) => Fit();
        panel.HandleCreated += (_, _) => Fit();
        Fit();
    }
}
