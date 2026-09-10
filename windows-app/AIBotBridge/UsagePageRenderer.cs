namespace AIBotBridge;

internal static class UsagePageRenderer
{
    private static readonly Color Green = Color.FromArgb(0, 217, 51);
    internal static void Draw(Graphics g, StatusSnapshot s, string mode)
    {
        g.Clear(Color.Black);
        if (mode is "dual" or "quotas")
        {
            g.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            DualText(g,"USAGE OVERVIEW",new(0,8,240,18),9,Color.White,FontStyle.Bold,StringAlignment.Center);
            using var divider=new Pen(Color.FromArgb(45,45,45));g.DrawLine(divider,18,121,222,121);
            Section(g,s,"CLAUDE",s.Quotas?.Claude,s.Claude.State,31,false);
            Section(g,s,"CODEX",s.Quotas?.Codex,s.Codex.State,132,s.Quotas?.Codex?.PrimaryPercent is null);
            return;
        }
        var claude = mode == "claude";
        var quota = claude ? s.Quotas?.Claude : s.Quotas?.Codex;
        var percent = claude ? quota?.PrimaryPercent : quota?.WeeklyPercent ?? quota?.PrimaryPercent;
        Ring(g, percent);
        if(!LocalPageLogos.Draw(g,claude))Text(g,claude ? "CLAUDE" : "CODEX",new(14,18,40,40),8,claude?Color.Orange:Color.Cyan,true);
        SinglePlanBadge(g,quota?.Plan ?? "",!claude && (quota?.ResetCreditsAvailable>0 || quota?.ResetCreditExpiresAt.Count>0));
        var animate = (claude ? s.Claude.State : s.Codex.State) == "working" || !claude && s.Codex.CompletionActive && DateTimeOffset.UtcNow.ToUnixTimeSeconds()-s.Codex.CompletionAt<4;
        if (!PetAnimationStore.Shared.Draw(g,64,64,claude ? "claude" : "codex", animate))
            ByteSproutRenderer.Draw(g,91,70,animate,s.CapturedAt.ToUnixTimeMilliseconds());
        if (!claude)
        {
            DrawCreditBadge(g,s,quota);
        }
        if (quota?.PrimaryPercent is null && (!claude || quota?.WeeklyPercent is not null))
            SingleRow(g,s,"WK",quota?.WeeklyPercent,quota?.WeeklyResetsAt,191,true);
        else {
            SingleRow(g,s,"5H",quota?.PrimaryPercent,quota?.PrimaryResetsAt,178);
            SingleRow(g,s,"WK",quota?.WeeklyPercent,quota?.WeeklyResetsAt,201);
        }
    }
    private static void Section(Graphics g,StatusSnapshot s,string name,ProviderQuotaSnapshot? q,string state,int top,bool weeklyOnly)
    {
        using var dot=new SolidBrush(state=="working"?Green:state=="idle"?Color.FromArgb(255,204,0):Color.FromArgb(90,90,90));
        g.FillEllipse(dot,18,top+4,7,7);
        DualText(g,name,new(31,top,100,22),10,name=="CLAUDE"?Color.Orange:Color.Cyan);
        if(name=="CODEX"&&q?.ResetCreditsAvailable>0) DualText(g,$"R*{q.ResetCreditsAvailable}",new(81,top,34,17),10,Green,FontStyle.Bold,StringAlignment.Center,true);
        var plan=PlanDisplay.Normalize(q?.Plan);
        if(!string.IsNullOrWhiteSpace(plan)) {
            using var font=new Font("Consolas",6.5f*96f/72f,FontStyle.Regular,GraphicsUnit.Pixel);
            int width=Math.Clamp((int)Math.Ceiling(g.MeasureString(plan,font).Width)+16,40,112);
            var bounds=new Rectangle(220-width,top,width,16);var color=PlanColor(plan);
            using var path=new System.Drawing.Drawing2D.GraphicsPath();
            foreach(var arc in new[]{(bounds.Left,bounds.Top,180),(bounds.Right-8,bounds.Top,270),(bounds.Right-8,bounds.Bottom-8,0),(bounds.Left,bounds.Bottom-8,90)})path.AddArc(arc.Item1,arc.Item2,8,8,arc.Item3,90);
            path.CloseFigure();using var fill=new SolidBrush(Color.FromArgb(35,color));using var pen=new Pen(color);
            g.FillPath(fill,path);g.DrawPath(pen,path);
            DualText(g,plan,bounds,6.5f,color,FontStyle.Regular,StringAlignment.Center,true);
        }
        if(weeklyOnly) BarRow(g,s,"WK",q?.WeeklyPercent,q?.WeeklyResetsAt,top+35);
        else { BarRow(g,s,"5H",q?.PrimaryPercent,q?.PrimaryResetsAt,top+24); BarRow(g,s,"WK",q?.WeeklyPercent,q?.WeeklyResetsAt,top+57); }
    }
    private static void BarRow(Graphics g,StatusSnapshot s,string label,double? pct,DateTimeOffset? reset,int y)
    {
        var muted=Color.FromArgb(145,145,145);
        DualText(g,label,new(20,y+3,30,18),7.5f,muted);
        DualText(g,Reset(s,reset),new(52,y+5,98,18),6.5f,muted,FontStyle.Regular);
        DualText(g,pct.HasValue?$"{Math.Round(pct.Value):0}%":"--",new(150,y,70,20),12,Color.White,FontStyle.Bold,StringAlignment.Far);
        using var track=new SolidBrush(Color.FromArgb(42,42,42));g.FillRectangle(track,20,y+23,200,6);
        using var fill=new SolidBrush(pct>=99.5?Color.Red:pct>=80?Color.FromArgb(255,204,0):Green);
        if(pct.HasValue)g.FillRectangle(fill,20,y+23,(float)(2*Math.Clamp(pct.Value,0,100)),6);
    }
    private static void DualText(Graphics g,string text,Rectangle bounds,float points,Color color,FontStyle style=FontStyle.Bold,StringAlignment align=StringAlignment.Near,bool middle=false)
    {
        // The scene is a 240px logical canvas, then scaled once by MirrorForm.
        // Point units inherit desktop DPI and otherwise enlarge/crop these rows.
        using var font=new Font("Consolas",points*96f/72f,style,GraphicsUnit.Pixel);using var brush=new SolidBrush(color);
        using var format=new StringFormat{Alignment=align,LineAlignment=middle?StringAlignment.Center:StringAlignment.Near,FormatFlags=StringFormatFlags.NoWrap};
        g.DrawString(text,font,brush,bounds,format);
    }
    private static void SingleRow(Graphics g,StatusSnapshot s,string label,double? pct,DateTimeOffset? reset,int y,bool single=false)
    {
        using var panel=new SolidBrush(Color.FromArgb(16,16,16));
        int height=single?24:21,radius=single?7:6;
        var bounds=new Rectangle(20,y,200,height);
        g.FillRoundedRectangle(panel,bounds,radius);
        using var path=new System.Drawing.Drawing2D.GraphicsPath();
        int diameter=radius*2;
        foreach(var arc in new[]{(bounds.Left,bounds.Top,180),(bounds.Right-diameter,bounds.Top,270),(bounds.Right-diameter,bounds.Bottom-diameter,0),(bounds.Left,bounds.Bottom-diameter,90)})path.AddArc(arc.Item1,arc.Item2,diameter,diameter,arc.Item3,90);
        path.CloseFigure();using var border=new Pen(Color.FromArgb(41,52,41));g.DrawPath(border,path);
        if(single)y++;
        Text(g,label,new(20,y,66,22),11,Color.FromArgb(145,145,145),true);
        Text(g,pct.HasValue?$"{Math.Clamp((int)pct.Value,0,100)}%":"--",new(87,y,66,22),14,Color.White,true);
        Text(g,Reset(s,reset),new(154,y,66,22),11,Color.Cyan,true);
    }
    internal static string Reset(StatusSnapshot s,DateTimeOffset? reset)
    {
        if(reset is null)return "";
        var minutes=Math.Max(0,(int)Math.Ceiling((reset.Value.ToUnixTimeSeconds()-s.EpochUtc)/60.0));
        return minutes>=1440?$"{minutes/1440}d {minutes%1440/60}h":minutes>=60?$"{minutes/60}h {minutes%60}m":$"{minutes}m";
    }
    internal static void Ring(Graphics g,double? pct,bool showTrack=true)
    {
        using var track=new SolidBrush(Color.FromArgb(42,42,42));
        if(showTrack) {
        g.FillRectangle(track,4,4,232,10);g.FillRectangle(track,226,4,10,232);
        g.FillRectangle(track,4,226,232,10);g.FillRectangle(track,4,4,10,232);
        }
        using var brush=new SolidBrush(Green);
        var remaining=928*(float)(Math.Clamp(pct??0,0,100)/100);
        foreach(var side in new[]{new RectangleF(4,4,232,10),new RectangleF(226,4,10,232),new RectangleF(4,226,232,10),new RectangleF(4,4,10,232)})
        {
            var length=Math.Clamp(remaining,0,232);remaining-=232;
            if(length==0)continue;
            var rect=side;if(rect.Width>rect.Height){rect.Width=length;if(side.Y==226)rect.X=236-length;}
            else{rect.Height=length;if(side.X==4)rect.Y=236-length;}
            g.FillRectangle(brush,rect);
        }
    }
    private static void Badge(Graphics g,string text,Rectangle bounds)
    {
        if(text.Length==0)return;
        g.DrawRectangle(Pens.DarkGoldenrod,bounds);Text(g,text,bounds,10,Color.Gold,true);
    }
    private static void SinglePlanBadge(Graphics g,string plan,bool credits)
    {
        plan=PlanDisplay.Normalize(plan);
        if(string.IsNullOrEmpty(plan))return;
        Color color=PlanColor(plan);
        using var font=new Font("Consolas",10,FontStyle.Bold,GraphicsUnit.Pixel);
        int width=Math.Clamp((int)Math.Ceiling(g.MeasureString(plan,font).Width)+12,34,credits?88:100);
        var bounds=new Rectangle(61,29,width,18);
        using var fill=new SolidBrush(Color.FromArgb(35,color));g.FillRoundedRectangle(fill,bounds,5);
        using var path=new System.Drawing.Drawing2D.GraphicsPath();
        foreach(var arc in new[]{(bounds.Left,bounds.Top,180),(bounds.Right-10,bounds.Top,270),(bounds.Right-10,bounds.Bottom-10,0),(bounds.Left,bounds.Bottom-10,90)})path.AddArc(arc.Item1,arc.Item2,10,10,arc.Item3,90);
        path.CloseFigure();using var pen=new Pen(color);g.DrawPath(pen,path);Text(g,plan,bounds,10,color,true);
    }
    internal static RectangleF CreditBounds(int rows)=>new(153,Math.Max(15,29-(rows-1)*9.5f),68,rows*19-1);
    private static Color PlanColor(string plan)=>plan switch { "PRO" or "PRO LITE"=>Color.FromArgb(255,159,10),"PLUS"=>Color.FromArgb(10,210,255),
        "TEAM" or "BUSINESS" or "ENTERPRISE"=>Color.FromArgb(191,90,242),"MAX" or "MAX 5X" or "MAX 20X"=>Color.FromArgb(255,125,45),_=>Color.FromArgb(174,174,178)};
    internal static (int Count,string Date)[] SingleCreditRows(StatusSnapshot s,ProviderQuotaSnapshot? quota)
    {
        if(quota is null)return [];
        if(quota.ResetCreditExpiresAt.Count>0)return quota.ResetCreditExpiresAt.Select(epoch=>
        {
            string date="";
            if(epoch>s.EpochUtc)try {date=DateTimeOffset.FromUnixTimeSeconds(epoch).AddSeconds(s.UtcOffsetSeconds).ToString("M/d",System.Globalization.CultureInfo.InvariantCulture);}catch(ArgumentOutOfRangeException) { }
            return (1,date);
        }).ToArray();
        return quota.ResetCreditsAvailable>0?[(quota.ResetCreditsAvailable.Value,"")]:[];
    }
    private static void DrawCreditBadge(Graphics g,StatusSnapshot s,ProviderQuotaSnapshot? quota)
    {
        var rows=SingleCreditRows(s,quota);
        if(rows.Length==0)return;
        var bounds=CreditBounds(rows.Length);
        using var background=new SolidBrush(Color.FromArgb(35,Green));
        using var path=new System.Drawing.Drawing2D.GraphicsPath();
        foreach(var arc in new[]{(bounds.Left,bounds.Top,180),(bounds.Right-10,bounds.Top,270),(bounds.Right-10,bounds.Bottom-10,0),(bounds.Left,bounds.Bottom-10,90)})path.AddArc(arc.Item1,arc.Item2,10,10,arc.Item3,90);
        path.CloseFigure();g.FillPath(background,path);using var outline=new Pen(Green);g.DrawPath(outline,path);
        using var font=new Font("Consolas",10,FontStyle.Bold,GraphicsUnit.Pixel);
        using var format=(StringFormat)StringFormat.GenericTypographic.Clone();format.FormatFlags|=StringFormatFlags.NoWrap;
        using var brush=new SolidBrush(Green);
        for(int i=0;i<rows.Length;i++) {
            var count=$"R*{rows[i].Count}";var date=rows[i].Date=="--"?"":rows[i].Date;
            float countWidth=g.MeasureString(count,font,PointF.Empty,format).Width,dateWidth=date.Length==0?0:g.MeasureString(date,font,PointF.Empty,format).Width,gap=date.Length==0?0:3;
            float x=bounds.X+(bounds.Width-countWidth-gap-dateWidth)/2,y=bounds.Y+i*19+(18-font.GetHeight(g))/2;
            g.DrawString(count,font,brush,x,y,format);if(date.Length>0)g.DrawString(date,font,brush,x+countWidth+gap,y,format);
        }
    }
    private static void Text(Graphics g,string text,Rectangle bounds,int size,Color color,bool center=false)
    {
        using var font=new Font("Consolas",size,FontStyle.Bold,GraphicsUnit.Pixel);
        using var brush=new SolidBrush(color);
        using var format=new StringFormat{Alignment=center?StringAlignment.Center:StringAlignment.Near,LineAlignment=StringAlignment.Center,Trimming=StringTrimming.EllipsisCharacter,FormatFlags=StringFormatFlags.NoWrap};
        g.DrawString(text,font,brush,bounds,format);
    }
}
