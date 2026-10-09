namespace AIBotBridge;

internal sealed class MirrorForm : Form
{
    private readonly Func<StatusSnapshot> _capture;
    private readonly Func<string> _mode;
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 70 };
    private readonly Label _status = new() {TextAlign=ContentAlignment.MiddleCenter,ForeColor=SystemColors.GrayText};
    private readonly Label _level = new() {Text="--",TextAlign=ContentAlignment.MiddleRight,ForeColor=SystemColors.GrayText};
    private readonly PreviewBrightness _brightness = new();
    private sealed record PageChoice(string Label,string Mode){public override string ToString()=>Label;}
    private readonly PreviewButton _automatic=new(){Name="preview-auto",Text="自动轮播",Glyph="auto"};
    private readonly PreviewButton _pages=new(){Name="preview-pages",Glyph="pages",AccessibleName="选择轮播页面"};
    private readonly PreviewButton _previous=new(){Glyph="previous",AccessibleName="上一页"},_next=new(){Glyph="next",AccessibleName="下一页"},_close=new(){Glyph="close",AccessibleName="收起预览"};
    private readonly ContextMenuStrip _pageMenu=new(){ShowImageMargin=false,ShowCheckMargin=true};
    private PageChoice[] _choices=[];
    private double _progress;
    private StatusSnapshot? _frame;
    private string _frameMode="auto";
    internal string? LastPaintedMode {get;private set;}
    internal int PaintCount {get;private set;}
    internal bool PassiveTestWindow {get;set;}
    protected override bool ShowWithoutActivation=>PassiveTestWindow||base.ShowWithoutActivation;
    private readonly Label _playback=new(){Name="preview-playback",TextAlign=ContentAlignment.MiddleLeft,ForeColor=Color.FromArgb(109,121,139)};
    private readonly Label _sun=new(){Text="亮度",TextAlign=ContentAlignment.MiddleCenter,ForeColor=SystemColors.GrayText};
    private readonly TrendEntryButton _trendButton=new(){Text="Codex 额度趋势",AccessibleDescription="查看每日额度使用记录",Cursor=Cursors.Hand};
    private readonly DisplayPolicy _fallbackPolicy=DisplayModes.Load(BridgeSettings.Load());
    private string _pageSignature="";
    private long _lastBrightness;
    private readonly Func<string,bool>? _select;
    private long _autoFeedbackUntil;
    private string _autoFeedback="";
    private readonly Func<int,bool>? _sendBrightness;
    private readonly Func<Task<UsbDeviceInfo>>? _readDevice;
    private QuotaTrendForm? _trend;
    private bool _openingQuotaTrend;
    private bool _openingPageMenu;

    internal MirrorForm(Func<StatusSnapshot> capture, Func<string> mode,Func<string,bool>? select=null,Func<int,bool>? brightness=null,Func<Task<UsbDeviceInfo>>? readDevice=null)
    {
        _capture = capture;
        _mode = mode;
        _select=select;_sendBrightness=brightness;_readDevice=readDevice;
        Text = "AI-bot 240×240 镜像";
        AutoScaleMode=AutoScaleMode.None;FormBorderStyle=FormBorderStyle.None;StartPosition=FormStartPosition.Manual;
        ShowInTaskbar=false;TopMost=true;ClientSize=new Size(360,498);BackColor=Color.FromArgb(246,248,251);
        DoubleBuffered = true;
        Controls.AddRange([_automatic,_previous,_pages,_next,_close,_playback,_sun,_brightness,_level,_status,_trendButton]);
        _automatic.Click+=(_,_)=>SelectPage("auto");
        _pages.Click+=(_,_)=>ShowPageMenu();
        _pages.ContextMenuStrip=_pageMenu;
        _previous.Click+=(_,_)=>StepPage(-1);_next.Click+=(_,_)=>StepPage(1);_close.Click+=(_,_)=>Hide();
        _brightness.Scroll+=(_,_)=>SendBrightness(false);_brightness.Committed+=(_,_)=>SendBrightness(true);
        _trendButton.Click+=(_,_)=>ShowQuotaTrend();
        SyncModes();LayoutShortcuts();
        DpiChanged+=(_,_)=>LayoutShortcuts();
        _timer.Tick += (_, _) => {SyncModes();Invalidate();};
        VisibleChanged+=async(_,_)=>{if(Visible){_timer.Start();SyncModes();await RefreshDevice();}else _timer.Stop();};
        Deactivate+=(_,_)=>{if(!_openingPageMenu&&!_pageMenu.Visible)Hide();};
        _pageMenu.Closed+=(_,e)=>{
            if(e.CloseReason==ToolStripDropDownCloseReason.AppFocusChange||
               e.CloseReason==ToolStripDropDownCloseReason.AppClicked&&!Bounds.Contains(Cursor.Position))Hide();
        };
    }
    internal void ShowQuotaTrend()
    {
        if (_openingQuotaTrend) return;
        _openingQuotaTrend = true;
        try
        {
            // Keep the active popup alive until the destination has acquired its window
            // and focus; hiding it first hands foreground activation to another app.
            if (_trend is null || _trend.IsDisposed) _trend = new QuotaTrendForm();
            if (_trend.WindowState == FormWindowState.Minimized) _trend.WindowState = FormWindowState.Normal;
            _trend.ShowForUser();
            Hide();
        }
        finally { _openingQuotaTrend = false; }
    }
    private sealed class TrendEntryButton : Button
    {
        internal TrendEntryButton() { FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0; }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Parent?.BackColor??BackColor);
            float scale = DeviceDpi / 96f;
            using var font = new Font("Microsoft YaHei UI", 11 * scale, FontStyle.Regular, GraphicsUnit.Pixel);
            TextRenderer.DrawText(e.Graphics, Text, font, ClientRectangle, Color.FromArgb(97,120,157), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
            if (Focused) ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(ClientRectangle, -3, -3));
        }
    }
    internal string DisplayedMode=>_frameMode;
    private void SelectPage(string mode)
    {
        bool wasCycling=_automatic.Active;
        bool applied=_select?.Invoke(mode)==true;
        _autoFeedbackUntil=0;
        if(mode=="auto"){
            _autoFeedback=applied?(wasCycling?"已重新开始":"已开始轮播"):"未生效";
            _autoFeedbackUntil=Environment.TickCount64+1400;
        }
        SyncModes();Invalidate();
    }
    internal string[] ShortcutPages=>_choices.Select(p=>p.Mode).ToArray();
    internal void ShowPageMenu()
    {
        _pageMenu.Items.Clear();string current=DisplayedMode;
        foreach(var page in _choices){var item=new ToolStripMenuItem(page.Label){Checked=page.Mode==current,Tag=page.Mode};item.Click+=(_,_)=>SelectPage(page.Mode);_pageMenu.Items.Add(item);}
        _openingPageMenu=true;
        try{_pageMenu.Show(_pages,new Point(0,_pages.Height+4));}finally{_openingPageMenu=false;}
    }
    private void StepPage(int direction)
    {
        if(_choices.Length==0)return;int index=Array.FindIndex(_choices,p=>p.Mode==DisplayedMode);
        SelectPage(_choices[(index+direction+_choices.Length)%_choices.Length].Mode);
    }
    internal void SyncModes()
    {
        var live=DisplayModes.ExpireCompletionNotice(_capture());var policy=live.DisplayPolicy??_fallbackPolicy;
        var pages=policy.Pages.Where(id=>DisplayModes.Pages.Any(p=>p.Mode==id)).Distinct().ToArray();
        string signature=string.Join(',',pages);
        if(_pageSignature!=signature||_choices.Length==0) {
            _choices=pages.Select(id=>{var p=DisplayModes.Pages.First(p=>p.Mode==id);return new PageChoice(p.Label,p.Mode);}).ToArray();_pageSignature=signature;
            if(_pageMenu.Visible)_pageMenu.Close();
        }
        string selected=live.DisplayPolicy?.SelectedMode??_mode();string effective=DisplayModes.Resolve(live,selected);
        _frame=live;_frameMode=effective;
        bool cycling=selected=="auto"&&policy.CycleEnabled;
        if(_automatic.Active!=cycling){_automatic.Active=cycling;_automatic.Invalidate();}
        var notice=DisplayModes.AutomaticNotice(live);
        _automatic.Text=Environment.TickCount64<_autoFeedbackUntil&&(!cycling||notice is null)?_autoFeedback:cycling?(notice is null?"轮播中":"提醒中"):"自动轮播";
        _automatic.AccessibleName=_automatic.Text;
        _automatic.AccessibleDescription=cycling?"自动轮播已启用；再次点击重新开始轮播。":"启用自动轮播。";
        string label=DisplayModes.Pages.FirstOrDefault(p=>p.Mode==effective).Label??(effective=="screensaver"?"屏保":effective);
        _pages.Text=label;_pages.Enabled=_previous.Enabled=_next.Enabled=_choices.Length>0;
        _progress=0;
        if(cycling&&pages.Length>0) {
            int interval=Math.Max(1,policy.IntervalSeconds);long elapsed=Math.Max(0,live.EpochUtc-policy.CycleStartedAt);int index=(int)(elapsed/interval%pages.Length);
            _playback.Text=notice is not null?$"{notice} · {label}":pages.Length==1?"自动轮播 · 已选 1 页":$"轮播 {index+1}/{pages.Length} · {interval-elapsed%interval} 秒后切换";
            if(notice is null)_progress=(double)(elapsed%interval)/interval;
        }else _playback.Text=selected=="screensaver"?"屏保中":selected=="auto"?"智能跟随":"手动查看";
    }
    private void LayoutShortcuts()
    {
        float scale=DeviceDpi/96f;
        int Px(float value)=>(int)Math.Round(value*scale);
        void Place(Control control,int x,int y,int width,int height)=>control.SetBounds(Px(x),Px(y),Px(width),Px(height));
        Place(_status,228,14,94,20);Place(_close,326,12,22,24);
        Place(_previous,14,344,28,32);Place(_pages,44,344,142,32);Place(_next,188,344,28,32);Place(_automatic,230,344,116,32);
        Place(_playback,18,387,328,20);
        Place(_sun,18,422,32,26);Place(_brightness,58,422,232,26);Place(_level,298,422,46,26);
        Place(_trendButton,14,461,332,26);ClientSize=new(Px(360),Px(501));
        using var outline=PreviewButton.Round(new RectangleF(0,0,Width,Height),14*scale);var previous=Region;Region=new Region(outline);previous?.Dispose();
    }
    private async Task RefreshDevice(){if(_readDevice is null)return;var requestedAt=_lastBrightness;try{var info=await _readDevice();if(IsDisposed||!Visible||requestedAt!=_lastBrightness)return;_brightness.Value=Math.Clamp(info.Brightness,0,100);_level.Text=info.Brightness+"%";_status.Text="USB 已连接";}catch(Exception ex)when(ex is IOException or TimeoutException or InvalidOperationException){if(!IsDisposed&&requestedAt==_lastBrightness)_status.Text="设备暂不可用，请连接后重试。";}}
    private void SendBrightness(bool final){_level.Text=_brightness.Value+"%";if(!final&&Environment.TickCount64-_lastBrightness<250)return;_lastBrightness=Environment.TickCount64;_status.Text=_sendBrightness?.Invoke(_brightness.Value)==true?"亮度已发送":"亮度未发送：设备未连接或正在诊断。";}
    internal void ShowAtTray(){SyncModes();LayoutShortcuts();var area=Screen.FromPoint(Cursor.Position).WorkingArea;Location=new Point(Math.Clamp(Cursor.Position.X-Width/2,area.Left+8,Math.Max(area.Left+8,area.Right-Width-8)),Math.Max(area.Top,area.Bottom-Height-8));Show();Activate();}
    protected override bool ProcessCmdKey(ref Message msg,Keys keyData){if(keyData==Keys.Escape){Hide();return true;}return base.ProcessCmdKey(ref msg,keyData);}

    protected override void Dispose(bool disposing)
    {
        if (disposing) { _timer.Dispose(); _pageMenu.Dispose(); _trend?.Dispose(); }
        base.Dispose(disposing);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if(_frame is null)return;
        using var logical = RenderSnapshot(_frame, _frameMode);
        e.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
        e.Graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
        float scale=DeviceDpi/96f;
        e.Graphics.DrawImage(logical,new Rectangle((int)(36*scale),(int)(44*scale),(int)(288*scale),(int)(288*scale)));
        LastPaintedMode=_frameMode;PaintCount++;
        e.Graphics.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using var title=new Font("Microsoft YaHei UI",12*scale,FontStyle.Bold,GraphicsUnit.Pixel);
        TextRenderer.DrawText(e.Graphics,"小屏预览",title,new Point((int)(18*scale),(int)(15*scale)),Color.FromArgb(53,65,81));
        using var line=new Pen(Color.FromArgb(220,227,237),2*scale);e.Graphics.DrawLine(line,18*scale,410*scale,342*scale,410*scale);
        if(_progress>0){using var progress=new Pen(Color.FromArgb(109,146,209),2*scale);e.Graphics.DrawLine(progress,18*scale,410*scale,(18+324*(float)_progress)*scale,410*scale);}
    }

    internal static Bitmap RenderSnapshot(StatusSnapshot status, string mode)
    {
        var bitmap = new Bitmap(240, 240);
        using var graphics = Graphics.FromImage(bitmap);
        Draw(graphics, status, mode);
        return bitmap;
    }



    private static void Draw(Graphics graphics, StatusSnapshot status, string mode)
    {
        graphics.Clear(Color.Black);
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
        switch (mode)
        {
            case "weather": WeatherSceneRenderer.Draw(graphics, status); break;
            case "stocks": DrawStocks(graphics, status); break;
            case "claude": case "codex": case "dual": case "quotas": UsagePageRenderer.Draw(graphics, status, mode); break;
            case "domestic": case "domestic_alibaba": case "domestic_kimi": case "domestic_minimax": case "domestic_deepseek": case "domestic_zhipu": case "domestic_stepfun": case "domestic_baidu": case "domestic_xiaomi": DomesticPageRenderer.Draw(graphics, status, mode); break;
            case "system": SystemPageRenderer.Draw(graphics, status); break;
            case "music": DrawMusic(graphics, status); break;
            case "pet": DrawPet(graphics, status); break;
            case "screensaver": DrawScreenSaver(graphics, status); break;
            default: DrawDashboard(graphics, status); break;
        }
        if (mode is "claude" or "codex" or "pet")
        {
            bool claude = mode=="claude";
            var activity = claude ? status.Claude : status.Codex;
            if (activity.NeedsInput && status.CapturedAt.ToUnixTimeMilliseconds()%800<400)
            {
                DrawAlertBorder(graphics,Color.FromArgb(255,59,48));
            }
            else if (!activity.NeedsInput && !claude && activity.CompletionActive)
            {
                long phase = Math.Max(0,status.CapturedAt.ToUnixTimeMilliseconds()-activity.CompletionAt*1000)/70;
                if(phase<50)
                {
                    int[] levels=[40,88,144,208,255,255,208,144,88,0];
                    using var pen=new Pen(Color.FromArgb(0,levels[phase%10],0),8); graphics.DrawRectangle(pen,5,5,230,230);
                }
            }
        }
    }

    internal static void DrawAlertBorder(Graphics graphics,Color color)
    {
        using var brush=new SolidBrush(color);
        foreach(var rectangle in new[]{new Rectangle(4,4,232,10),new Rectangle(4,226,232,10),new Rectangle(4,4,10,232),new Rectangle(226,4,10,232)})
            graphics.FillRectangle(brush,rectangle);
    }

    private static void DrawDashboard(Graphics g, StatusSnapshot s)
    {
        Center(g, "AI-bot", 18, 24, Color.Cyan, true);
        Center(g, s.Time, 68, 30, Color.White, true);
        g.DrawLine(Pens.DimGray, 20, 110, 220, 110);
        Row(g, "CODEX", s.Codex.State.ToUpperInvariant(), 120, StateColor(s.Codex.State));
        Row(g, "CLAUDE", s.Claude.State.ToUpperInvariant(), 146, StateColor(s.Claude.State));
        if (s.Quotas?.Codex?.ResetCreditsAvailable is > 0)
            DrawText(g, $"R*{s.Quotas.Codex.ResetCreditsAvailable}", 86, 120, 12,
                s.Quotas.Codex.Stale ? Color.Orange : Color.LimeGreen, true);
        Center(g,"本机今日 Token",169,12,Color.FromArgb(148,148,148));
        DrawText(g,"Codex",22,189,12,Color.LightGray);
        DrawRight(g,s.Codex.TokensToday.ToString(),218,189,14,Color.LimeGreen);
        DrawText(g,"Claude",22,209,12,Color.LightGray);
        DrawRight(g,s.Claude.TokensToday.ToString(),218,209,14,Color.LimeGreen);
    }

    private static void DrawWeather(Graphics g, StatusSnapshot s)
    {
        Center(g, "WEATHER", 6, 14, Color.Cyan, true);
        if (s.Weather is not { } w) { Center(g, "等待数据", 105, 16, Color.Gray); return; }
        Center(g, $"{w.City}  {w.Condition}", 35, 18, Color.White, true);
        Center(g, $"{w.Temperature:0}°C", 70, 38, Color.Orange, true);
        Center(g, $"{w.Low:0} / {w.High:0}", 119, 16, Color.LightGray);
        Row(g, "湿度", $"{w.Humidity}%", 157, Color.White);
        Row(g, "PM2.5", w.Pm25?.ToString("0.0") ?? "--", 190, Color.White);
        DrawText(g, "OPEN-METEO", 6, 225, 8, Color.DimGray);
    }

    private static void DrawStocks(Graphics g, StatusSnapshot s)
    {
        var all = s.Stocks?.Quotes ?? [];
        var pages = Math.Max(1, (all.Count + 3) / 4);
        var page = (int)(s.EpochUtc / 5 % pages);
        var quotes = all.Skip(page * 4).Take(4).ToArray();
        using (var footer = new Font("Consolas",8,FontStyle.Regular,GraphicsUnit.Pixel))
        using (var centered = new StringFormat { Alignment=StringAlignment.Center })
            g.DrawString(quotes.Length==0?"Waiting for bridge...":pages>1?$"STOCKS {page+1}/{pages}":"STOCKS",
                footer,Brushes.Gray,new RectangleF(0,quotes.Length==0?104:228,240,12),centered);
        if (quotes.Length == 0) return;
        for (var index = 0; index < quotes.Length; index++)
        {
            var q = quotes[index];
            var y = 6 + index * 54;
            using var codeFont=new Font("Consolas",12,FontStyle.Regular,GraphicsUnit.Pixel);
            using var nameFont=new Font("Microsoft YaHei UI",12,FontStyle.Regular,GraphicsUnit.Pixel);
            using var valueFont=new Font("Consolas",24,FontStyle.Regular,GraphicsUnit.Pixel);
            using var right=new StringFormat{Alignment=StringAlignment.Far,Trimming=StringTrimming.EllipsisCharacter};
            g.DrawString(q.Code,codeFont,Brushes.Gray,14,y);
            g.DrawString(q.Name,nameFont,Brushes.LightGray,new RectangleF(70,y,156,20),right);
            g.DrawString(q.Price,valueFont,Brushes.White,14,y+20);
            using var change=new SolidBrush(q.Trend>0?Color.Red:q.Trend<0?Color.FromArgb(0,217,51):Color.LightGray);
            g.DrawString(q.ChangePercent,valueFont,change,new RectangleF(130,y+20,96,31),right);
        }
    }

    private static void DrawQuotas(Graphics g, StatusSnapshot s)
    {
        Center(g, "ACCOUNT QUOTAS", 5, 14, Color.White, true);
        Provider(g, "CLAUDE", s.Quotas?.Claude, 32);
        Provider(g, "CODEX", s.Quotas?.Codex, 112);
        DrawResetCredits(g, s, 196);
    }

    private static void DrawResetCredits(Graphics g, StatusSnapshot s, int y)
    {
        var rows = ResetCreditDisplay.Rows(s.Quotas?.Codex, s.UtcOffsetSeconds);
        if (rows.Count == 0) return;
        var pages = (rows.Count + 1) / 2;
        var page = (int)((s.EpochUtc / 4) % pages);
        var color = s.Quotas?.Codex?.Stale == true ? Color.Orange : Color.LimeGreen;
        for (var index = page * 2; index < rows.Count && index < page * 2 + 2; index++)
        {
            var top = y + index % 2 * 18;
            DrawText(g, $"R*{rows[index].Count}", 16, top, 14, color, true);
            DrawRight(g, rows[index].Date, 180, top, 12, color);
        }
        if (pages > 1) DrawRight(g, $"{page + 1}/{pages}", 228, y + 5, 9, Color.LightGray);
    }

    private static void Provider(Graphics g, string name, ProviderQuotaSnapshot? value, int y)
    {
        DrawText(g, name, 12, y, 14, Color.Cyan, true);
        DrawRight(g, value?.Stale == true ? "STALE" : value?.Plan ?? "", 228, y, 12, Color.Gray);
        Row(g, "5H", Percent(value?.PrimaryPercent), y + 24, Color.White);
        Row(g, "7D", Percent(value?.WeeklyPercent), y + 47, Color.White);
    }

    private static void DrawDomestic(Graphics g, StatusSnapshot s)
    {
        Center(g, "DOMESTIC QUOTAS", 5, 14, Color.White, true);
        DomesticRow(g, "QWEN", s.DomesticQuotas?.Alibaba, 34);
        DomesticRow(g, "KIMI", s.DomesticQuotas?.Kimi, 80);
        DomesticRow(g, "MINIMAX", s.DomesticQuotas?.MiniMax, 126);
        DomesticRow(g, "DEEPSEEK", s.DomesticQuotas?.DeepSeek, 172);
    }

    private static void DomesticRow(Graphics g, string name, DomesticProviderQuotaSnapshot? value, int y)
    {
        DrawText(g, name, 10, y, 13, value?.Stale == true ? Color.Orange : Color.Cyan, true);
        var display = value?.WeeklyPercent is double weekly ? $"{weekly:0.0}% WK"
            : value?.PrimaryPercent is double primary ? $"{primary:0.0}% 5H" : "--";
        if (value?.Balance is double balance)
        {
            using var numberFont = new Font("Microsoft YaHei UI", 22, FontStyle.Bold, GraphicsUnit.Pixel);
            using var currencyFont = new Font("Microsoft YaHei UI", 11, FontStyle.Regular, GraphicsUnit.Pixel);
            var number = balance.ToString("0.00");
            var width = g.MeasureString(number, numberFont).Width;
            DrawRight(g, number, 230, y, 22, Color.White, true);
            var baselineOffset = FontAscent(numberFont) - FontAscent(currencyFont);
            DrawRight(g, value.Currency ?? "", 226 - width, y + baselineOffset, 11, Color.White);
        }
        else DrawRight(g, display, 230, y, 13, Color.White, true);
        DrawText(g, value?.Plan ?? "", 10, y + 28, 9, Color.Gray);
    }

    private static void DrawSystem(Graphics g, StatusSnapshot s)
    {
        Center(g, "SYSTEM", 7, 16, Color.Cyan, true);
        if (s.SystemMetrics is not { } m) { Center(g, "等待数据", 105, 16, Color.Gray); return; }
        Row(g, "CPU", $"{m.CpuPercent:0.0}%", 47, Color.LimeGreen);
        Row(g, "MEMORY", $"{m.MemoryPercent:0.0}%", 87, Color.Orange);
        g.DrawLine(Pens.DimGray, 18, 125, 222, 125);
        Row(g, "UPLOAD", Rate(m.UploadBytesPerSecond), 145, Color.Yellow);
        Row(g, "DOWNLOAD", Rate(m.DownloadBytesPerSecond), 185, Color.Cyan);
    }

    private static void DrawMusic(Graphics g, StatusSnapshot s)
    {
        var m = s.Music;
        if (m?.CoverRgb565 is { Length: PetAnimation.FrameBytes } pixels)
        {
            using var cover = new PetAnimation([200], [pixels]).BitmapAt(0);
            g.InterpolationMode=System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode=System.Drawing.Drawing2D.PixelOffsetMode.Half;
            g.DrawImage(cover, 56, 16, 128, 128);
        }
        else
        {
            using var blank=new SolidBrush(Color.FromArgb(64,64,64));g.FillRectangle(blank,56,16,128,128);
            using var font=new Font("Consolas",13,FontStyle.Bold,GraphicsUnit.Pixel);
            using var centered=new StringFormat{Alignment=StringAlignment.Center};
            g.DrawString("No Art",font,Brushes.LightGray,new RectangleF(56,72,128,20),centered);
        }
        using(var titleFont=new Font("Microsoft YaHei UI",15,FontStyle.Bold,GraphicsUnit.Pixel))
        using(var artistFont=new Font("Microsoft YaHei UI",12,FontStyle.Regular,GraphicsUnit.Pixel))
        using(var centered=new StringFormat{Alignment=StringAlignment.Center,Trimming=StringTrimming.EllipsisCharacter,FormatFlags=StringFormatFlags.NoWrap}) {
            g.DrawString(string.IsNullOrEmpty(m?.Title)?"No Music":m.Title.Split('\n').Last(),titleFont,Brushes.White,new RectangleF(12,154,216,24),centered);
            g.DrawString(m?.Artist??"",artistFont,Brushes.LightGray,new RectangleF(12,178,216,20),centered);
        }
        var ratio = m?.DurationSeconds > 0 ? Math.Clamp(m.ElapsedSeconds / m.DurationSeconds, 0, 1) : 0;
        using var background=new SolidBrush(Color.FromArgb(64,64,64));g.FillRectangle(background,20,210,200,8);
        using var progress = new SolidBrush(m?.Playing==true ? Color.FromArgb(0,217,51) : Color.Gray);
        g.FillRectangle(progress, 20, 210, (float)(200 * ratio), 8);
    }

    private static void DrawPet(Graphics g, StatusSnapshot s)
    {
        var working = s.Codex.State == "working" || s.Claude.State == "working";
        Center(g, "BYTE SPROUT", 12, 14, working ? Color.LimeGreen : Color.Cyan, true);
        var owner = s.Codex.State == "working" && s.Claude.State == "working" ? "CODEX + CLAUDE"
            : s.Codex.State == "working" ? "CODEX" : s.Claude.State == "working" ? "CLAUDE" : "READY";
        Center(g, owner, 32, 9, Color.LightGray);
        if (PetAnimationStore.Shared.Draw(g, 64, 49, s.Claude.State == "working" && s.Codex.State != "working" ? "claude" : "codex", working))
        {
            Center(g, working ? "WORKING" : "IDLE", 180, 16, working ? Color.LimeGreen : Color.Yellow, true);
            DrawResetCredits(g, s, 200);
            return;
        }
        ByteSproutRenderer.Draw(g,91,54,working,s.CapturedAt.ToUnixTimeMilliseconds());
        Center(g, working ? "WORKING" : "IDLE", 180, 16, working ? Color.LimeGreen : Color.Yellow, true);
        DrawResetCredits(g, s, 200);
    }

    private static void DrawScreenSaver(Graphics g, StatusSnapshot s) => ScreenSaverRenderer.Draw(g, s);

    private static void Row(Graphics g, string left, string right, int y, Color rightColor)
    {
        DrawText(g, left, 18, y, 14, Color.LightGray);
        DrawRight(g, right, 222, y, 14, rightColor, true);
    }

    private static void Center(Graphics g, string value, float y, float size, Color color, bool bold = false)
    {
        using var font = new Font("Microsoft YaHei UI", size, bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel);
        using var brush = new SolidBrush(color);
        using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Near,
            Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };
        g.DrawString(value, font, brush, new RectangleF(4, y, 232, size + 8), format);
    }

    private static void DrawText(Graphics g, string value, float x, float y, float size, Color color, bool bold = false)
    {
        using var font = new Font("Microsoft YaHei UI", size, bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel);
        using var brush = new SolidBrush(color);
        g.DrawString(value, font, brush, x, y);
    }

    private static void DrawRight(Graphics g, string value, float x, float y, float size, Color color, bool bold = false)
    {
        using var font = new Font("Microsoft YaHei UI", size, bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel);
        using var brush = new SolidBrush(color);
        using var format = new StringFormat { Alignment = StringAlignment.Far };
        g.DrawString(value, font, brush, new PointF(x, y), format);
    }

    private static float FontAscent(Font font) => font.Size * font.FontFamily.GetCellAscent(font.Style) /
        font.FontFamily.GetEmHeight(font.Style);

    private static Color StateColor(string state) => state == "working" ? Color.LimeGreen
        : state == "idle" ? Color.Yellow : Color.DimGray;
    private static string Percent(double? value) => value.HasValue ? $"{value:0.0}%" : "--";
    private static string Rate(long bytes) => bytes >= 1024 * 1024 ? $"{bytes / 1048576.0:0.0} MB/s"
        : bytes >= 1024 ? $"{bytes / 1024.0:0.0} KB/s" : $"{bytes} B/s";
}

internal static class GraphicsExtensions
{
    internal static void FillRoundedRectangle(this Graphics graphics, Brush brush, RectangleF bounds, float radius)
    {
        using var path = new System.Drawing.Drawing2D.GraphicsPath();
        var diameter = radius * 2;
        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        graphics.FillPath(brush, path);
    }
}
