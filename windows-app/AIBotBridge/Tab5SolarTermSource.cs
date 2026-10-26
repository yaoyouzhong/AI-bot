using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace AIBotBridge;

internal static class Tab5SolarTermSource
{
    internal static readonly Uri Catalog=new("https://pmo.cas.cn/xwdt2019/kpdt2019/202203/t20220309_6386774.html");
    internal static bool Allowed(Uri url)=>url.Scheme=="https"&&url.Host is "pmo.cas.cn" or "www.pmo.cas.cn";
    private static MatchCollection Matches(string pattern,string text)=>Regex.Matches(text,pattern,RegexOptions.Singleline|RegexOptions.IgnoreCase);
    internal static Dictionary<int,Uri> Documents(string html) {
        if(html.Length>524288)throw new ArgumentException("日历目录过大");
        var result=new Dictionary<int,Uri>();
        foreach(Match entry in Matches("<a\\b[^>]*href=[\"']([^\"']+)[\"'][^>]*>(.*?)</a>",html)) {
            string label=Regex.Replace(Regex.Replace(entry.Groups[2].Value,"<[^>]*>",""),@"\s+","");
            var match=Regex.Match(label,@"^《(\d{4})年日历资料》$");
            if(!match.Success)continue;
            int year=int.Parse(match.Groups[1].Value,CultureInfo.InvariantCulture);
            if(year is <1901 or >2099||!Uri.TryCreate(Catalog,entry.Groups[1].Value,out var url)||!Allowed(url)||!url.AbsolutePath.EndsWith(".pdf",StringComparison.OrdinalIgnoreCase)||result.TryGetValue(year,out var prior)&&prior!=url)
                throw new ArgumentException("日历年份或来源无效");
            result[year]=url;
        }
        if(result.Count==0)throw new ArgumentException("缺少年度日历目录");
        return result;
    }
    internal static string Table(byte[] data,int year) {
        if(data.Length>4194304||!data.AsSpan().StartsWith("%PDF-"u8))throw new ArgumentException("日历文档无效");
        try {
            using var document=PdfDocument.Open(data);
            if(document.NumberOfPages is <1 or >16)throw new ArgumentException("日历页数无效");
            var text=new StringBuilder();
            foreach(var page in document.GetPages()) {
                text.Append(ContentOrderTextExtractor.GetText(page));
                if(text.Length>131072)throw new ArgumentException("日历文字过大");
            }
            return TableText(text.ToString(),year);
        }catch(Exception){throw new ArgumentException("无法读取完整年度日历");}
    }
    internal static string TableText(string text,int year) {
        if(text.Length>131072||year is <1901 or >2099)throw new ArgumentException("日历年份或文字大小无效");
        string compact=Regex.Replace(text.Normalize(NormalizationForm.FormKC),@"\s+","").Replace('○','〇').Replace('零','〇');
        const string digits="〇一二三四五六七八九";
        string yearText=string.Concat(year.ToString(CultureInfo.InvariantCulture).Select(c=>digits[c-'0']));
        if(!compact.StartsWith(yearText+"年日历资料中国科学院紫金山天文台编制",StringComparison.Ordinal))throw new ArgumentException("日历封面年份或来源无效");
        var headers=Matches(@"([〇一二三四五六七八九]{4})年([一二三四五六七八九十]{1,3})月[大小平]",compact);
        string[] months=["一","二","三","四","五","六","七","八","九","十","十一","十二"];
        string[] names=["小寒","大寒","立春","雨水","惊蛰","春分","清明","谷雨","立夏","小满","芒种","夏至","小暑","大暑","立秋","处暑","白露","秋分","寒露","霜降","立冬","小雪","大雪","冬至"];
        var dates=new List<int>();int seenMonths=0;
        for(int i=0;i<headers.Count;i++) {
            var header=headers[i];if(header.Groups[1].Value!=yearText)continue;
            if(seenMonths>=12||header.Groups[2].Value!=months[seenMonths])throw new ArgumentException("日历月份缺失或重复");
            int start=header.Index+header.Length,end=i+1<headers.Count?headers[i+1].Index:compact.Length;
            var terms=Matches("【("+string.Join("|",names)+@")】(\d{1,2})日(\d{1,2})时(\d{1,2})分",compact[start..end]);
            if(terms.Count!=2)throw new ArgumentException("月份节气不完整");
            foreach(Match term in terms) {
                int day=int.Parse(term.Groups[2].Value,CultureInfo.InvariantCulture),hour=int.Parse(term.Groups[3].Value,CultureInfo.InvariantCulture),minute=int.Parse(term.Groups[4].Value,CultureInfo.InvariantCulture);
                if(term.Groups[1].Value!=names[dates.Count]||day is <1 or >28||hour is <0 or >23||minute is <0 or >59)throw new ArgumentException("节气日期或顺序无效");
                dates.Add((seenMonths+1)*100+day);
            }
            seenMonths++;
        }
        if(seenMonths!=12||dates.Count!=24||dates.Distinct().Count()!=24||!dates.SequenceEqual(dates.Order()))throw new ArgumentException("节气年度不完整");
        string[] cacheNames=["小寒","大寒","立春","雨水","驚蟄","春分","清明","穀雨","立夏","小滿","芒種","夏至","小暑","大暑","立秋","處暑","白露","秋分","寒露","霜降","立冬","小雪","大雪","冬至"];
        return $"# AI-bot solar terms; source=PMO; UTC+8; year={year}\n"+string.Join("\n",dates.Select((md,i)=>$"{year}年{md/100}月{md%100}日 - - {cacheNames[i]}"));
    }
}
