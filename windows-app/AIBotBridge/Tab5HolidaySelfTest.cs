using System.Text.Json.Nodes;

namespace AIBotBridge;
internal static class Tab5HolidaySelfTest
{
    internal static void Run(string fixture)
    {
        string json=File.ReadAllText(fixture);var year=Tab5HolidayStore.Parse(json,2026);
        if(!Tab5HolidayStore.Unpublished("{\"year\":2027,\"papers\":[],\"days\":[]}",2027)||Tab5HolidayStore.Unpublished(json,2026))throw new Exception("Unpublished notice detection failed");
        if(year.Holidays.Length!=7||year.Workdays.Length!=6||!year.Workdays.Contains(920)||
            !year.Holidays.Any(h=>h.Start==925&&h.End==927&&h.Name=="中秋节"))throw new Exception("2026 official-notice fixture changed");
        // Synthetic future-year schema fixture, never installed as real holidays.
        string future=json.Replace("2026","2030");
        if(Tab5HolidayStore.Parse(future,2030).Year!=2030)throw new Exception("Future-year dispatch failed");
        var cross=JsonNode.Parse(future)!;cross["days"]!.AsArray().Add(JsonNode.Parse("{\"name\":\"元旦\",\"date\":\"2029-12-31\",\"isOffDay\":true}"));
        var nextYear=Tab5HolidayStore.Parse(cross.ToJsonString(),2030);
        var lastYear=Tab5HolidayStore.Parse(json.Replace("2026","2029"),2029);
        if(!Tab5HolidayStore.Merge(lastYear,nextYear).Holidays.Any(h=>h.Start==1231&&h.End==1231))throw new Exception("Next-year notice December override lost");
        var node=JsonNode.Parse(json)!;
        var invalid=new List<string>{"{}",json.Replace("2026-01-01","2026-02-30"),json.Replace("www.gov.cn","example.com")};
        node["days"]!.AsArray().Add(node["days"]![0]!.DeepClone());invalid.Add(node.ToJsonString());
        node=JsonNode.Parse(json)!;node["days"]=new JsonArray();invalid.Add(node.ToJsonString());
        node=JsonNode.Parse(json)!;node["year"]=2027;invalid.Add(node.ToJsonString());
        foreach(string bad in invalid) {
            try{Tab5HolidayStore.Parse(bad,2026);throw new Exception("Invalid holiday data accepted");}catch(ArgumentException){}
        }
        string folder=Path.Combine(Path.GetTempPath(),"tab5-calendar-test-"+Guid.NewGuid().ToString("N"));
        try {
            Tab5HolidayStore.SaveDownloaded(folder,2026,json);
            string path=Path.Combine(folder,"2026.json");
            try{Tab5HolidayStore.SaveDownloaded(folder,2026,"{}");throw new Exception("Invalid update committed");}catch(ArgumentException){}
            if(File.ReadAllText(path)!=json||Tab5HolidayStore.Parse(File.ReadAllText(path),2026).Holidays.Length!=7)throw new Exception("Last good cache damaged");
        }finally {if(Directory.Exists(folder))Directory.Delete(folder,true);}
        Console.WriteLine("TAB5_HOLIDAY_PASS notice fixture, future schema, duplicates, invalid dates/source/year, empty/unpublished rejection");
    }
}
