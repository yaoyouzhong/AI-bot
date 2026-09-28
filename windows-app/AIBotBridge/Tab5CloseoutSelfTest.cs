namespace AIBotBridge;

internal static class Tab5CloseoutSelfTest
{
    internal static void Run(string fixture) {
        static void Check(bool ok,string reason){if(!ok)throw new Exception(reason);}
        string table=File.ReadAllText(fixture);
        var terms=Tab5SolarTerms.Parse(table,2029);
        Check(terms.Length==24&&terms[0]==105&&terms[^1]==1221,"2029 HKO dates changed");
        Check(Tab5SolarTerms.Parse(table.Replace("  ","\t"),2029).SequenceEqual(terms),"Whitespace parsing failed");
        foreach(string bad in new[]{"<html>unavailable</html>",table.Replace("2029年","2030年"),table+"\n2029年1月5日 十七 星期一 小寒",table.Replace("小寒","缺失")}) {
            try{Tab5SolarTerms.Parse(bad,2029);throw new Exception("Invalid table accepted");}catch(ArgumentException){}
        }
        string folder=Path.Combine(Path.GetTempPath(),"tab5-terms-test-"+Guid.NewGuid().ToString("N"));
        try {
            Tab5SolarTerms.Save(folder,2029,table);
            Check(Tab5Calendar.SolarTerm(new DateTime(2029,1,5))=="今日小寒","Downloaded future year does not reach calendar");
            try{Tab5SolarTerms.Save(folder,2029,"unavailable");throw new Exception("Bad cache committed");}catch(ArgumentException){}
            Check(File.ReadAllText(Path.Combine(folder,"terms-2029.txt"))==table,"Last good offline table lost");
            Check(Tab5Calendar.SolarTerm(new DateTime(2029,1,5))=="今日小寒","Bad update changed active calendar");
        }finally{if(Directory.Exists(folder))Directory.Delete(folder,true);}
        long now=100;var observed=new Tab5FirmwareObservation(()=>now);
        const string id="001122334455",version="0.2.38-ui";
        Check(observed.Summary(id,null,false)=="未提供固件","No offer state");
        Check(observed.Summary(id,version,false).Contains("等待设备"),"Unknown version must remain unknown");
        observed.Observe(id,version);
        Check(observed.Summary(id,version,false).Contains("已运行"),"Installed version still offered as new");
        Check(observed.Summary("abcdefabcdef",version,false).Contains("等待设备"),"Wrong device observation used");
        Check(observed.Summary(id,"0.2.39-ui",false).Contains("可供设备升级"),"Different version state");
        Check(observed.Summary(id,version,true).Contains("正在传输"),"Transfer state");
        now+=15001;
        Check(observed.Summary(id,version,false).Contains("等待设备"),"Stale installed version used");
        var parsed=Tab5ReleaseNotes.Parse("## 改进\r\n- 第一项\n* 第二项\n\n说明");
        Check(parsed.Length==4&&parsed[0]==(true,"改进")&&parsed[1]==(false,"• 第一项"),"Notes heading/list parse");
        Console.WriteLine("TAB5_CLOSEOUT_PASS official 2029 terms, malformed/duplicate/year rejection, last good cache, firmware receipt freshness, release notes");
    }
}
