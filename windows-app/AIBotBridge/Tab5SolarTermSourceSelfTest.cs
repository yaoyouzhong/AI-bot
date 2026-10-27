namespace AIBotBridge;

internal static class Tab5SolarTermSourceSelfTest
{
    internal static void Run() {
        static void Check(bool value){if(!value)throw new Exception("Domestic solar source check failed");}
        static void Reject(Action action){try{action();}catch(ArgumentException){return;}throw new Exception("Invalid solar source accepted");}
        int[] dates=[105,120,204,218,305,320,405,420,505,521,605,621,707,723,807,823,907,923,1008,1023,1107,1122,1207,1222];
        string[] months=["一","二","三","四","五","六","七","八","九","十","十一","十二"];
        string[] names=["小寒","大寒","立春","雨水","惊蛰","春分","清明","谷雨","立夏","小满","芒种","夏至","小暑","大暑","立秋","处暑","白露","秋分","寒露","霜降","立冬","小雪","大雪","冬至"];
        string text="二○二六年 日历资料 中国科学院紫金山天文台编制\n"+string.Join("\n",Enumerable.Range(0,12).Select(m=>"二○二六年"+months[m]+"月大 "+string.Concat(Enumerable.Range(0,2).Select(t=>$"【{names[m*2+t]}】{dates[m*2+t]%100}日12时30分"))))+"二○二七年一月大【小寒】5日22时10分【大寒】20日15时30分";
        string good=Tab5SolarTermSource.TableText(text,2026);
        Check(Tab5SolarTerms.Parse(good,2026).SequenceEqual(dates));
        string catalog="<a href=\"./example.pdf\"><span>《2026年日历资料》</span></a>";
        Check(Tab5SolarTermSource.Documents(catalog)[2026].Host=="pmo.cas.cn");
        Check(!Tab5SolarTermSource.Documents(catalog).ContainsKey(2027));
        foreach(string bad in new[]{text.Replace("二○二六","二○二五"),text.Replace("紫金山","其他"),text.Replace("二○二六年一月大",""),text.Replace("二○二六年二月大","二○二六年一月大"),text.Replace("【小寒】","【大寒】"),text.Replace("【冬至】",""),text.Replace("12时30分","24时30分"),text.Replace("12时30分","12时60分"),text.Replace("【雨水】18日","【雨水】30日"),text+text,new string('x',131073)})Reject(()=>Tab5SolarTermSource.TableText(bad,2026));
        Reject(()=>Tab5SolarTermSource.TableText(text,2027));
        foreach(string bad in new[]{"<html>unavailable</html>",catalog.Replace("./example.pdf","https://untrusted.example/example.pdf"),catalog.Replace("./example.pdf","http://pmo.cas.cn/example.pdf"),catalog+catalog.Replace("example.pdf","different.pdf")})Reject(()=>Tab5SolarTermSource.Documents(bad));
        Reject(()=>Tab5SolarTermSource.Table("<html>error</html>"u8.ToArray(),2026));
        string folder=Path.Combine(Path.GetTempPath(),"aibot-pmo-test-"+Guid.NewGuid().ToString("N"));
        try {
            Tab5SolarTerms.Save(folder,2026,good);
            Reject(()=>Tab5SolarTerms.Save(folder,2026,Tab5SolarTermSource.TableText("unavailable",2026)));
            Check(File.ReadAllText(Path.Combine(folder,"terms-2026.txt"))==good);
            Check(Tab5SolarTerms.Get(2026)!.SequenceEqual(dates));
        }finally{if(Directory.Exists(folder))Directory.Delete(folder,true);}
        Console.WriteLine("DOMESTIC_SOLAR_SOURCE_PASS catalog/year/month/24-term validation, future partial year rejected, last good cache");
    }
}
