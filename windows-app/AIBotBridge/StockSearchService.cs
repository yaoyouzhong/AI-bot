using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace AIBotBridge;

internal sealed record StockSearchResult(string Symbol,string Name,string Pinyin)
{
    internal string Market=>Symbol[..2] switch{"sh"=>"沪市","sz"=>"深市","bj"=>"北交所","hk"=>"港股","us"=>"美股",_=>""};
}
internal sealed class StockSearchService(HttpClient? http=null)
{
    private static readonly HttpClient Shared=new(){Timeout=TimeSpan.FromSeconds(8)};
    internal async Task<StockSearchResult[]> SearchAsync(string query,CancellationToken token)
    {
        query=query.Trim();
        if(query.Length==0)return [];
        if(query.Length>40||query.Any(char.IsControl))throw new ArgumentException("请输入不超过 40 个字符的代码、名称或拼音。");
        using var request=new HttpRequestMessage(HttpMethod.Get,"https://smartbox.gtimg.cn/s3/?q="+Uri.EscapeDataString(query)+"&t=all");
        request.Headers.UserAgent.ParseAdd("AI-bot/"+Application.ProductVersion.Split('+')[0]);
        using var response=await (http??Shared).SendAsync(request,HttpCompletionOption.ResponseHeadersRead,token);response.EnsureSuccessStatusCode();
        // The public suggestion response is data, never executable JavaScript.
        await using var stream=await response.Content.ReadAsStreamAsync(token);
        using var buffer=new MemoryStream();byte[] chunk=new byte[4096];int count;
        while((count=await stream.ReadAsync(chunk,token))>0){if(buffer.Length+count>65536)throw new InvalidDataException("搜索结果过大，请缩小查询范围。");buffer.Write(chunk,0,count);}
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Parse(Encoding.GetEncoding("GB18030").GetString(buffer.ToArray()));
    }
    internal static StockSearchResult[] Parse(string response)
    {
        var match=Regex.Match(response.Trim(),"^v_hint\\s*=\\s*(\"(?:[^\"\\\\]|\\\\.)*\")\\s*;?$",RegexOptions.Singleline);
        if(!match.Success)throw new InvalidDataException("搜索服务返回了无法识别的数据，请稍后重试。");
        string payload=JsonSerializer.Deserialize<string>(match.Groups[1].Value)??"";
        var results=new List<StockSearchResult>();
        foreach(string entry in payload.Split('^',StringSplitOptions.RemoveEmptyEntries)){
            var fields=entry.Split('~');if(fields.Length<5)continue;
            if(fields[4] is not ("GP" or "GP-A" or "ZS" or "ETF" or "FJ" or "LOF"))continue;
            string code=fields[1];if(fields[0]=="us")code=Regex.Replace(code,@"\.(?:oq|n|ps|am)$","",RegexOptions.IgnoreCase);
            string symbol=StockService.Normalize(fields[0]+code);
            if(!ValidSymbol(symbol)||fields[2].Length is 0 or >100||fields[2].Any(char.IsControl))continue;
            if(results.Any(r=>r.Symbol==symbol))continue;
            results.Add(new(symbol,fields[2],fields[3]));if(results.Count==20)break;
        }
        return results.ToArray();
    }
    internal static bool ValidSymbol(string symbol)=>Regex.IsMatch(symbol,@"^(?:(?:sh|sz|bj)\d{6}|hk(?:\d{5}|[A-Z][A-Z0-9]{0,15})|us[A-Z.^][A-Z0-9.^-]{0,19})$");
}
