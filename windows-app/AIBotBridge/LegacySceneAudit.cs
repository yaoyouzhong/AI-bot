using System.Reflection;
using System.Runtime.Loader;
using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace AIBotBridge;

// Read-only oracle: load the user's installed renderer, never its application entry point.
// Reference screenshots remain local artifacts; no legacy assembly or pixels are distributed.
internal static class LegacySceneAudit
{
    internal static void Run(string assemblyPath, string output)
    {
        var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.GetFullPath(assemblyPath));
        var type = assembly.GetType("AIClockBridge.MirrorControl", true)!;
        using var control = (Control)Activator.CreateInstance(type, true)!;
        control.Size=new Size(240,240);
        void Set(string name, object? value) => type.GetField(name, BindingFlags.Public | BindingFlags.Instance)!.SetValue(control, value);
        var now = DateTimeOffset.UtcNow;
        var status = new StatusSnapshot(1, "12:34", now.ToUnixTimeSeconds(), 28800, now,
            new ToolState("idle", 0), new ToolState("idle", 0));
        Directory.CreateDirectory(output);
        int failures = 0;
        void Compare(string name, string method, string mode, StatusSnapshot fixture)
        {
            using var before = new Bitmap(240, 240);
            // Dual scene's point fonts require a 96-DPI logical oracle. A desktop-
            // DPI bitmap reproduces the old clipping bug, not the intended layout.
            if(mode is "dual" or "quotas") before.SetResolution(96,96);
            using (var g = Graphics.FromImage(before))
            {
                g.Clear(Color.Black); g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                var draw=type.GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)!;
                draw.Invoke(draw.IsStatic?null:control, method=="OnPaint"?[new PaintEventArgs(g,new Rectangle(0,0,240,240))]:[g]);
            }
            using var after = MirrorForm.RenderSnapshot(fixture, mode);
            using var difference = new Bitmap(240, 240);
            int changed = 0;
            for (int y = 0; y < 240; y++) for (int x = 0; x < 240; x++)
            {
                bool differs = before.GetPixel(x,y).ToArgb() != after.GetPixel(x,y).ToArgb();
                if (differs) changed++;
                difference.SetPixel(x,y, differs ? Color.Magenta : Color.Black);
            }
            before.Save(Path.Combine(output, name+"-legacy.png"));
            after.Save(Path.Combine(output, name+"-new.png"));
            difference.Save(Path.Combine(output, name+"-diff.png"));
            Console.WriteLine($"SCENE {name}: differingPixels={changed}/57600");
            if (changed != 0) failures++;
        }
        Compare("stocks-empty", "DrawStockScene", "stocks", status);
        var quotes = new[] { new StockQuote("sh000001","000001","上证指数","3821.4","+0.85%",1),
            new StockQuote("hk00700","00700","腾讯控股","612.50","-1.20%",-1),
            new StockQuote("us.IXIC",".IXIC","纳斯达克","20000","0.00%",0),
            new StockQuote("sz300750","300750","很长的股票名称测试","321.20","+0.14%",1) };
        var rowType = assembly.GetType("AIClockBridge.StockMonitor+Row", true)!;
        var rows = Array.CreateInstance(rowType, quotes.Length);
        for (int i=0;i<quotes.Length;i++) { var q=quotes[i]; rows.SetValue(Activator.CreateInstance(rowType,[q.Symbol,q.Code,q.Name,q.Price,q.ChangePercent,q.Trend]),i); }
        Set("Stocks",rows);
        Compare("stocks-four", "DrawStockScene", "stocks", status with {Stocks=new(quotes,now,false)});
        Compare("music-empty", "DrawMusicScene", "music", status);
        foreach(bool playing in new[]{true,false})
        {
            Set("MusicTitle","测试音乐 Song"); Set("MusicArtist","歌手 Artist");
            Set("MusicElapsed",75d);Set("MusicDuration",240d);Set("MusicPlaying",playing);
            Compare("music-"+playing,"DrawMusicScene","music",status with {Music=new("测试音乐 Song","歌手 Artist","",playing,75,240,now)});
        }
        Set("NetCPU",37);Set("NetMem",61);Set("NetHeaderDL","5K");Set("NetHeaderUL","2K");
        var samples=Enumerable.Range(0,224).Select(i=>new NetworkSample(i%17*120,i%23*330)).ToArray();
        var push=type.GetMethod("PushNetSample")!;
        foreach(var p in samples)push.Invoke(control,[(double)p.Download,(double)p.Upload]);
        Compare("system-history","DrawNetScene","system",status with {SystemMetrics=new(37,61,2000,5000,now,samples)});
        object Model(string name)=>Activator.CreateInstance(assembly.GetType("AIClockBridge."+name,true)!,true)!;
        void Field(object target,string name,object? value)=>target.GetType().GetField(name)!.SetValue(target,value);
        var dual=type.GetField("Dual")!.GetValue(control)!;
        var c=dual.GetType().GetField("Claude")!.GetValue(dual)!;
        var x=dual.GetType().GetField("Codex")!.GetValue(dual)!;
        Field(c,"Status","idle");Field(x,"Status","idle");
        foreach(string plan in new[]{"PRO","PLUS","MAX 5X","TEAM"})
        {
            Field(c,"Plan","MAX");Field(c,"FiveHourPct",34d);Field(c,"SevenDayPct",81d);
            Field(c,"FiveHourResetMin",61);Field(c,"SevenDayResetMin",1500);
            Field(x,"Plan",plan);Field(x,"PrimaryPct",12d);Field(x,"WeeklyPct",99.5d);
            Field(x,"PrimaryResetMin",30);Field(x,"WeeklyResetMin",180);Field(x,"ResetCreditsAvailable",2);
            var cq=new ProviderQuotaSnapshot("claude","MAX",34,now.AddMinutes(61),81,now.AddMinutes(1500),null,[],now,false);
            var xq=new ProviderQuotaSnapshot("codex",plan,12,now.AddMinutes(30),99.5,now.AddMinutes(180),2,[],now,false);
            Compare("dual-"+plan.Replace(' ','-'),"DrawDualScene","dual",status with{Quotas=new(cq,xq)});
        }
        foreach(string provider in new[]{"alibaba","kimi","minimax","deepseek"})
        {
            var domestic=Model("DomesticStatus");var active=Model("DomesticProviderStatus");
            Field(domestic,"ActiveProvider",provider=="alibaba"?"qwen":provider);Field(domestic,"Active",active);
            Field(active,"Model",provider=="kimi"?"Ultra":"");
            bool balance=provider=="deepseek",window=provider is "kimi" or "minimax";
            if(balance){Field(active,"Balance",28.5d);Field(active,"UsedCost",9.75d);Field(active,"Currency","CNY");Field(active,"Model","API PAYG");Field(active,"MembershipBadge",true);}
            else if(window){Field(active,"PlanPct",42d);Field(active,"FiveHourPct",20d);Field(active,"WeeklyPct",42d);Field(active,"FiveHourResetMin",30);Field(active,"WeeklyResetMin",180);Field(active,"MembershipBadge",provider=="kimi");}
            else {Field(active,"PlanPct",12.5d);}
            Set("Domestic",domestic);
            var q=new DomesticProviderQuotaSnapshot(provider,provider=="kimi"?"Ultra":"",window?20:null,window?now.AddMinutes(30):null,
                window?42:null,window?now.AddMinutes(180):null,balance?28.5:null,balance?9.75:null,balance?"CNY":null,now,false){PlanPercent=provider=="alibaba"?12.5:null};
            Compare("domestic-"+provider,"DrawDomesticScene","domestic_"+provider,status with {DomesticQuotas=new(provider=="alibaba"?q:null,provider=="kimi"?q:null,provider=="minimax"?q:null,provider=="deepseek"?q:null)});
        }
        var clockNow=DateTimeOffset.Now;
        Compare("screensaver","DrawScreenSaverScene","screensaver",status with{EpochUtc=clockNow.ToUnixTimeSeconds(),UtcOffsetSeconds=(int)clockNow.Offset.TotalSeconds});
        foreach(string role in new[]{"claude","codex"})
        {
            var animation=PetAnimationStore.Shared.Selection(role)??throw new InvalidOperationException("Local legacy pet is required for the audit.");
            using var frame=animation.BitmapAt(0);
            Set("Frames",new List<Bitmap>{frame});Set("SpriteW",animation.Width);Set("SpriteH",animation.Height);
            Set("ShowingClaude",role=="claude");Set("DeviceOK",true);Set("Plan","PRO");Set("RingPct",role=="claude"?34d:81d);
            Set("FiveHourPct",34d);Set("WeeklyPct",81d);Set("FiveHourResetMin",61);Set("WeeklyResetMin",1500);
            var q=new ProviderQuotaSnapshot(role,"PRO",34,now.AddMinutes(61),81,now.AddMinutes(1500),null,[],now,false);
            Compare("single-"+role,"OnPaint",role,status with{Quotas=new(role=="claude"?q:null,role=="codex"?q:null)});
            Set("NeedsInput",true);Set("FlashOn",true);
            Compare("attention-"+role,"OnPaint",role,status with{CapturedAt=DateTimeOffset.FromUnixTimeMilliseconds(800),Codex=new("idle",0,NeedsInput:role=="codex"),Claude=new("idle",0,NeedsInput:role=="claude"),Quotas=new(role=="claude"?q:null,role=="codex"?q:null)});
            Set("NeedsInput",false);Set("FlashOn",false);
        }
        foreach(var city in new[]{"南京雨花台区","北京"})
        {
            var weather=Model("WeatherMonitor+Snapshot");
            void Property(string name,object value)=>weather.GetType().GetProperty(name)!.SetValue(weather,value);
            Property("City",city);Property("Condition","多云");Property("AirQuality","优");
            Property("Temperature",26d);Property("Low",19d);Property("High",31d);Property("Humidity",58);Property("UtcOffsetS",28800);
            Set("Weather",weather);
            var weatherNow=DateTimeOffset.UtcNow;
            Compare("weather-"+(city=="北京"?"short":"long"),"DrawWeatherScene","weather",status with {EpochUtc=weatherNow.ToUnixTimeSeconds(),Weather=new(city,"多云",26,31,19,58,2,13,46,"fixture",weatherNow,false,"优",UtcOffsetSeconds:28800)});
        }
        var codexAnimation=PetAnimationStore.Shared.Selection("codex")!;
        using var codexFrame=codexAnimation.BitmapAt(0);
        Set("Frames",new List<Bitmap>{codexFrame});
        foreach(int count in new[]{0,1,2,5})
        {
            var dates=Enumerable.Range(0,count).Select(i=>now.AddDays(1+i/2).ToUnixTimeSeconds()).ToArray();
            Set("FiveHourPct",null);Set("WeeklyPct",100d);Set("RingPct",100d);
            Set("ResetCreditsAvailable",count);Set("ResetCreditExpiresAtList",dates);
            var q=new ProviderQuotaSnapshot("codex","PRO",null,null,100,now.AddMinutes(1500),count,dates,now,false);
            Compare("codex-weekly-credits-"+count,"OnPaint","codex",status with{Quotas=new(null,q)});
        }
        Console.WriteLine($"LEGACY_SCENE_AUDIT: mismatchedCases={failures}; reference={assembly.ManifestModule.ModuleVersionId}");
        Environment.ExitCode = failures==0?0:1;
    }
}
