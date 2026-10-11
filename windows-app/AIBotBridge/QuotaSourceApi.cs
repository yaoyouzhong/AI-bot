using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AIBotBridge;

// Independently implemented from the vendors' documented read-only contracts.
internal static class QuotaSourceApi
{
    internal sealed record CloudCredentials(string AccessKey,string SecretKey);
    private static readonly HttpClient Client=new(new HttpClientHandler{AllowAutoRedirect=false}){Timeout=TimeSpan.FromSeconds(20)};
    internal static async Task<DomesticProviderQuotaSnapshot> FetchAsync(string provider,string credential,HttpClient? client=null)
    {
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(20));
        using var request=CreateRequest(provider,credential,DateTimeOffset.UtcNow,Guid.NewGuid().ToString("N"));
        using var response=await (client??Client).SendAsync(request,HttpCompletionOption.ResponseHeadersRead,timeout.Token);
        if(!response.IsSuccessStatusCode)throw new HttpRequestException("Quota source request failed.",null,response.StatusCode);
        await using var input=await response.Content.ReadAsStreamAsync(timeout.Token);
        using var bytes=new MemoryStream();var buffer=new byte[8192];int count;
        while((count=await input.ReadAsync(buffer,timeout.Token))>0){if(bytes.Length+count>1024*1024)throw new JsonException("Quota response too large.");bytes.Write(buffer,0,count);}
        using var json=JsonDocument.Parse(bytes.ToArray());return Parse(provider,json.RootElement);
    }
    internal static HttpRequestMessage CreateRequest(string provider,string credential,DateTimeOffset now,string nonce)
    {
        if(provider is "kimi" or "zhipu"){
            if(string.IsNullOrWhiteSpace(credential)||credential.Any(char.IsControl))throw new FormatException("Invalid credential.");
            var request=new HttpRequestMessage(HttpMethod.Get,provider=="kimi"?"https://api.moonshot.cn/v1/users/me/balance":"https://open.bigmodel.cn/api/monitor/usage/quota/limit");
            if(provider=="kimi")request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",credential.Trim());
            else request.Headers.Add("Authorization",credential.Trim()); // Official GLM usage plugin uses the raw Coding Plan key.
            return request;
        }
        var c=JsonSerializer.Deserialize<CloudCredentials>(credential)??throw new FormatException();
        if(string.IsNullOrWhiteSpace(c.AccessKey)||string.IsNullOrWhiteSpace(c.SecretKey)||c.AccessKey.Any(char.IsControl)||c.SecretKey.Any(char.IsControl))throw new FormatException("Invalid cloud credential.");
        var date=now.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'",CultureInfo.InvariantCulture);
        if(provider=="qwen"){
            var parameters=new SortedDictionary<string,string>(StringComparer.Ordinal){["Action"]="QueryAccountBalance",["Version"]="2017-12-14",["Format"]="JSON",["AccessKeyId"]=c.AccessKey,["SignatureMethod"]="HMAC-SHA1",["SignatureVersion"]="1.0",["SignatureNonce"]=nonce,["Timestamp"]=date};
            var query=string.Join("&",parameters.Select(p=>Uri.EscapeDataString(p.Key)+"="+Uri.EscapeDataString(p.Value)));
            var canonical="GET&%2F&"+Uri.EscapeDataString(query);
            var signature=Convert.ToBase64String(HMACSHA1.HashData(Encoding.UTF8.GetBytes(c.SecretKey+"&"),Encoding.UTF8.GetBytes(canonical)));
            return new(HttpMethod.Get,"https://business.aliyuncs.com/?"+query+"&Signature="+Uri.EscapeDataString(signature));
        }
        if(provider!="baidu")throw new ArgumentException("Unsupported quota source.");
        var prefix=$"bce-auth-v1/{c.AccessKey}/{date}/1800";
        var signed="POST\n/v1/finance/cash/balance\n\nhost:billing.baidubce.com\nx-bce-date:"+Uri.EscapeDataString(date);
        var result=new HttpRequestMessage(HttpMethod.Post,"https://billing.baidubce.com/v1/finance/cash/balance"){Content=new StringContent("{}",Encoding.UTF8,"application/json")};
        result.Headers.Host="billing.baidubce.com";result.Headers.Add("x-bce-date",date);
        result.Headers.TryAddWithoutValidation("Authorization",prefix+"/host;x-bce-date/"+AdditionalQuotaApi.Hmac(AdditionalQuotaApi.Hmac(c.SecretKey,prefix),signed));return result;
    }
    static double Number(JsonElement value){if(value.ValueKind is not (JsonValueKind.Number or JsonValueKind.String)||!double.TryParse(value.ToString(),NumberStyles.Float|NumberStyles.AllowThousands,CultureInfo.InvariantCulture,out var n)||!double.IsFinite(n))throw new JsonException("Invalid quota number.");return n;}
    static DomesticProviderQuotaSnapshot Balance(string provider,string label,double value,string currency)=>new(provider=="qwen"?"alibaba":provider,label,null,null,null,null,value,null,currency,DateTimeOffset.UtcNow,false);
    internal static DomesticProviderQuotaSnapshot Parse(string provider,JsonElement root)
    {
        if(provider=="kimi"){
            if(root.GetProperty("code").GetInt32()!=0||root.GetProperty("status").ValueKind!=JsonValueKind.True)throw new JsonException("Kimi did not return a successful balance.");
            return Balance(provider,"Moonshot API balance",Number(root.GetProperty("data").GetProperty("available_balance")),"CNY");
        }
        if(provider=="qwen"){
            if(root.GetProperty("Success").ValueKind!=JsonValueKind.True||root.GetProperty("Code").ToString()!="200")throw new JsonException("Aliyun did not return a successful balance.");
            var data=root.GetProperty("Data");var currency=data.GetProperty("Currency").GetString();if(currency is not ("CNY" or "USD" or "JPY"))throw new JsonException("Unknown currency.");
            return Balance(provider,"Aliyun cloud cash balance",Number(data.GetProperty("AvailableCashAmount")),currency);
        }
        if(provider=="baidu"){
            if(root.TryGetProperty("code",out _)||root.TryGetProperty("error",out _))throw new JsonException("Baidu error response.");
            return Balance(provider,"Baidu cloud cash balance",Number(root.GetProperty("cashBalance")),"CNY");
        }
        if(provider!="zhipu")throw new ArgumentException("Unsupported quota source.");
        if(!root.TryGetProperty("success",out var success)||success.ValueKind!=JsonValueKind.True||root.TryGetProperty("code",out var code)&&code.ToString()!="200")throw new JsonException("GLM did not return successful limits.");
        var limits=root.GetProperty("data").GetProperty("limits");if(limits.ValueKind!=JsonValueKind.Array)throw new JsonException("Missing GLM limits.");
        double? primary=null,weekly=null;DateTimeOffset? primaryReset=null,weeklyReset=null;
        long? primarySeconds=null,weeklySeconds=null;
        foreach(var limit in limits.EnumerateArray()){
            if(limit.GetProperty("type").GetString()!="TOKENS_LIMIT")continue; // MCP count is a different budget.
            var pct=Number(limit.GetProperty("percentage"));if(pct is <0 or >100)throw new JsonException("Invalid percent.");
            DateTimeOffset? reset=null;if(limit.TryGetProperty("nextResetTime",out var time)&&time.ValueKind!=JsonValueKind.Null){var epoch=Number(time);reset=DateTimeOffset.FromUnixTimeMilliseconds(checked((long)epoch));}
            var unit=limit.TryGetProperty("unit",out var unitValue)?unitValue.GetInt32():3;
            var number=limit.TryGetProperty("number",out var numberValue)?numberValue.GetInt32():5;
            if(unit==3&&number is >0 and <=24){if(primary is not null)throw new JsonException("Ambiguous hourly limit.");primary=pct;primaryReset=reset;primarySeconds=number*3600;}
            else if(unit==6&&number==1){if(weekly is not null)throw new JsonException("Ambiguous weekly limit.");weekly=pct;weeklyReset=reset;weeklySeconds=604800;}
        }
        if(primary is null&&weekly is null)throw new JsonException("No supported Coding Plan window.");
        return new("zhipu","GLM Coding Plan",primary,primaryReset,weekly,weeklyReset,null,null,null,DateTimeOffset.UtcNow,false)
            {PrimaryWindowSeconds=primarySeconds,WeeklyWindowSeconds=weeklySeconds};
    }
}
