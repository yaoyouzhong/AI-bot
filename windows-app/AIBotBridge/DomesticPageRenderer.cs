namespace AIBotBridge;

internal static class DomesticPageRenderer
{
    internal static bool Windowed(string provider,DomesticProviderQuotaSnapshot? quota)=>provider=="kimi"||quota?.PrimaryPercent is not null||quota?.WeeklyPercent is not null;
    internal static double? DisplayPercent(DomesticProviderQuotaSnapshot? quota)=>quota?.WeeklyPercent??quota?.PlanPercent;
    internal static string Remaining(DomesticProviderQuotaSnapshot? quota)=>quota?.PlanPercent is double plan?(Math.Truncate(Math.Max(0,100-plan)*100)/100).ToString("0.00",System.Globalization.CultureInfo.InvariantCulture)+"% LEFT":"QUOTA UNKNOWN";
    internal static void Draw(Graphics g, StatusSnapshot s, string mode)
    {
        var provider = mode.Replace("domestic_", "");
        if (provider == "domestic") provider = BridgeSettings.Load().Get("domestic_provider", "alibaba").ToLowerInvariant();
        var quota = provider switch { "kimi"=>s.DomesticQuotas?.Kimi, "minimax"=>s.DomesticQuotas?.MiniMax,
            "deepseek"=>s.DomesticQuotas?.DeepSeek, "zhipu"=>s.DomesticQuotas?.Zhipu, _=>s.DomesticQuotas?.Alibaba };
        g.Clear(Color.Black);
        var percent=DisplayPercent(quota);
        bool windowed=Windowed(provider,quota);
        UsagePageRenderer.Ring(g, quota?.Balance is not null ? 0 : (int)Math.Clamp(percent??0,0,100),false);
        g.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using var green=new SolidBrush(Color.FromArgb(0,217,51));
        using var heading=new SolidBrush(provider=="zhipu"?Color.FromArgb(139,156,255):Color.FromArgb(0,217,51));g.FillEllipse(heading,21,26,8,8);
        using(var nameFont=new Font("Consolas",16,FontStyle.Bold,GraphicsUnit.Pixel))g.DrawString(provider=="alibaba"?"QWEN":provider=="zhipu"?"GLM":provider.ToUpperInvariant(),nameFont,heading,36,22);
        var membership=DomesticDisplayText.Membership(provider,quota?.Plan,quota?.Balance is not null);
        if(membership.Length>0) {
            using var badgeFont=new Font("Consolas",10,FontStyle.Bold,GraphicsUnit.Pixel);
            int badgeWidth=Math.Clamp((int)Math.Ceiling(g.MeasureString(membership,badgeFont).Width)+12,34,112),x=218-badgeWidth;
            using var badgePath=new System.Drawing.Drawing2D.GraphicsPath();
            foreach(var arc in new[]{(x,22,180),(208,22,270),(208,30,0),(x,30,90)})badgePath.AddArc(arc.Item1,arc.Item2,10,10,arc.Item3,90);
            badgePath.CloseFigure();var color=Color.FromArgb(255,159,10);using var fill=new SolidBrush(Color.FromArgb(35,color));using var pen=new Pen(color);
            g.FillPath(fill,badgePath);g.DrawPath(pen,badgePath);Label(g,membership,x,22,badgeWidth,18,10,color,true);
        }
        else {
            using var modelFont=new Font("Consolas",12,FontStyle.Regular,GraphicsUnit.Pixel);
            using var right=new StringFormat{Alignment=StringAlignment.Far,LineAlignment=StringAlignment.Center,FormatFlags=StringFormatFlags.NoWrap,Trimming=StringTrimming.EllipsisCharacter};
            g.DrawString(string.IsNullOrEmpty(quota?.Plan)?"--":quota.Plan,modelFont,Brushes.LightGray,new RectangleF(106,20,112,22),right);
        }
        using var muted=new SolidBrush(Color.FromArgb(123,125,123));g.FillRectangle(muted,20,53,200,1);
        var balance=quota?.Balance;
        if(provider=="zhipu"&&!balance.HasValue) {
            Label(g,"AVAILABLE BALANCE",20,73,200,20,12,Color.Gray,true);
            Label(g,"--",20,100,200,50,36,Color.FromArgb(255,251,222),true);
            Label(g,"Authorize in bridge",20,177,200,38,12,Color.FromArgb(139,156,255),true);
            return;
        }
        if(!balance.HasValue) {
            using var caption=new Font("Consolas",9,FontStyle.Regular,GraphicsUnit.Pixel);
            using var center=new StringFormat{Alignment=StringAlignment.Center,LineAlignment=StringAlignment.Center,FormatFlags=StringFormatFlags.NoWrap,Trimming=StringTrimming.EllipsisCharacter};
            g.DrawString(windowed?"WEEKLY":"PLAN",caption,muted,new RectangleF(0,69,240,16),center);
        }
        var number=balance?.ToString("0.00",System.Globalization.CultureInfo.InvariantCulture)??DomesticDisplayText.Percent(percent);
        var suffix=balance.HasValue?quota?.Currency??"":percent.HasValue?"%":"";
        using var font=new Font("Consolas",number.Length<=3?54:number.Length<=6?40:number.Length<=9?30:24,FontStyle.Bold,GraphicsUnit.Pixel);
        using var unit=new Font("Consolas",font.Size<=30?14:22,FontStyle.Bold,GraphicsUnit.Pixel);
        using var numericBrush=new SolidBrush(Color.FromArgb(255,251,222));
        var width=g.MeasureString(number,font).Width; var unitWidth=g.MeasureString(suffix,unit).Width;
        var left=(240-width-unitWidth-(suffix.Length>0?4:0))/2;
        if(balance.HasValue)DrawBalance(g,number,suffix);
        else {
        var measured=g.MeasureString(number,font);float numberY=90+(54-measured.Height)/2;
        g.DrawString(number,font,numericBrush,left,numberY);
        g.DrawString(suffix,unit,green,left+width+4,numberY+measured.Height-unit.Height-1);
        }
        if(!balance.HasValue) {
            using var caption=new Font("Consolas",9,FontStyle.Regular,GraphicsUnit.Pixel);
            using var value=new Font("Consolas",12,FontStyle.Bold,GraphicsUnit.Pixel);
            using var format=(StringFormat)StringFormat.GenericTypographic.Clone();format.FormatFlags|=StringFormatFlags.NoWrap;
            float ascent(Font f)=>f.Size*f.FontFamily.GetCellAscent(f.Style)/f.FontFamily.GetEmHeight(f.Style);
            g.DrawString(windowed?"RESET":"REMAINING",caption,muted,new PointF(37,164-ascent(caption)),format);
            format.Alignment=StringAlignment.Far;
            g.DrawString(windowed?UsagePageRenderer.Reset(s,quota?.WeeklyResetsAt):Remaining(quota),value,green,new PointF(203,164-ascent(value)),format);
        }
        var bounds=new Rectangle(20,177,200,38);
        using var panel=new SolidBrush(Color.FromArgb(16,16,16));g.FillRoundedRectangle(panel,bounds,8);
        using var path=new System.Drawing.Drawing2D.GraphicsPath();
        foreach(var arc in new[]{(20,177,180),(204,177,270),(204,199,0),(20,199,90)})path.AddArc(arc.Item1,arc.Item2,16,16,arc.Item3,90);
        path.CloseFigure();using var border=new Pen(Color.FromArgb(41,52,41));g.DrawPath(border,path);
        Label(g,balance.HasValue?"USED":windowed?"5H":"RESET",20,177,66,38,12,Color.FromArgb(0,217,51),true);
        if(balance.HasValue) {
            if(quota?.UsedCost is double cost) {
                var amount=cost.ToString("0.00",System.Globalization.CultureInfo.InvariantCulture);var currency=quota.Currency??"";
                using var small=new Font("Consolas",12,FontStyle.Bold,GraphicsUnit.Pixel);
                float amountWidth=g.MeasureString(amount,small).Width,currencyWidth=currency.Length==0?0:g.MeasureString(currency,small).Width,gap=currency.Length==0?0:5;
                float start=153.5f-(amountWidth+gap+currencyWidth)/2;
                Label(g,amount,start,177,amountWidth,38,12,Color.FromArgb(255,251,222),true);
                if(currency.Length>0)Label(g,currency,start+amountWidth+gap,177,currencyWidth,38,12,Color.FromArgb(0,217,51),true);
            } else Label(g,"--",87,177,133,38,12,Color.FromArgb(255,251,222),true);
        }
        else if(windowed) {
            Label(g,quota?.PrimaryPercent is double primary?$"{(int)Math.Clamp(primary,0,100)}%":"--",87,177,66,38,12,Color.White,true);
            Label(g,UsagePageRenderer.Reset(s,quota?.PrimaryResetsAt),154,177,66,38,12,Color.Cyan,true);
        } else {
            var reset=quota?.PlanResetsAt?.AddSeconds(s.UtcOffsetSeconds).UtcDateTime.ToString("MM-dd HH:mm",System.Globalization.CultureInfo.InvariantCulture)??"";
            Label(g,reset,87,177,133,38,13,Color.Cyan,true);
        }
        if(s.DomesticActivity?.ActiveProvider==provider&&s.DomesticActivity.NeedsInput&&s.CapturedAt.ToUnixTimeMilliseconds()%800<400)
        { using var pen=new Pen(Color.FromArgb(255,59,48),8);g.DrawRectangle(pen,5,5,230,230); }
    }
    private static void DrawBalance(Graphics g,string amount,string currency)
    {
        using var format=(StringFormat)StringFormat.GenericTypographic.Clone();format.FormatFlags|=StringFormatFlags.NoWrap;
        for(int size=40;size>=20;size--) {
            int captionSize=Math.Clamp((int)Math.Round(size*.4),10,16),unitSize=Math.Clamp((int)Math.Round(size*.5),12,20);
            int lineGap=Math.Clamp((int)Math.Round(size*.2),5,8),gap=currency.Length==0?0:Math.Clamp((int)Math.Round(size*.15),4,6);
            using var caption=new Font("Consolas",captionSize,FontStyle.Regular,GraphicsUnit.Pixel);
            using var value=new Font("Consolas",size,FontStyle.Bold,GraphicsUnit.Pixel);
            using var unit=new Font("Consolas",unitSize,FontStyle.Bold,GraphicsUnit.Pixel);
            float captionWidth=g.MeasureString("AVAILABLE BALANCE",caption,PointF.Empty,format).Width;
            float valueWidth=g.MeasureString(amount,value,PointF.Empty,format).Width,unitWidth=currency.Length==0?0:g.MeasureString(currency,unit,PointF.Empty,format).Width;
            float totalWidth=valueWidth+gap+unitWidth,totalHeight=caption.GetHeight(g)+lineGap+value.GetHeight(g);
            if(size>20&&(Math.Max(captionWidth,totalWidth)>184||totalHeight>90))continue;
            float top=62+(106-totalHeight)/2,valueTop=top+caption.GetHeight(g)+lineGap,left=120-totalWidth/2;
            using var muted=new SolidBrush(Color.FromArgb(123,125,123));using var numeric=new SolidBrush(Color.FromArgb(255,251,222));using var green=new SolidBrush(Color.FromArgb(0,217,51));
            g.DrawString("AVAILABLE BALANCE",caption,muted,120-captionWidth/2,top,format);
            g.DrawString(amount,value,numeric,left,valueTop,format);
            float ascent(Font f)=>f.Size*f.FontFamily.GetCellAscent(f.Style)/f.FontFamily.GetEmHeight(f.Style);
            if(currency.Length>0)g.DrawString(currency,unit,green,left+valueWidth+gap,valueTop+ascent(value)-ascent(unit),format);
            break;
        }
    }
    private static void Label(Graphics g,string text,float x,float y,float w,float h,int size,Color color,bool center=false)
    {
        using var font=new Font("Consolas",size,FontStyle.Bold,GraphicsUnit.Pixel);using var brush=new SolidBrush(color);
        using var format=new StringFormat{Alignment=center?StringAlignment.Center:StringAlignment.Near,LineAlignment=StringAlignment.Center,FormatFlags=StringFormatFlags.NoWrap,Trimming=StringTrimming.EllipsisCharacter};
        g.DrawString(text,font,brush,new RectangleF(x,y,w,h),format);
    }
}
