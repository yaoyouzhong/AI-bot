using System.Text.Json;

namespace AIBotBridge;

internal static class ZhipuSelfTest
{
    internal static void Run()
    {
        const string fixture = """{"code":200,"data":{"balance":99,"availableBalance":"28.50","totalSpendAmount":9.75}}""";
        var quota=DomesticQuotaService.Parse("zhipu",fixture);
        var fractional=DomesticQuotaService.Parse("zhipu","""{"code":200,"data":{"availableBalance":99.9993936,"totalSpendAmount":0.0006064}}""");
        if(fractional.Balance!=99.99||fractional.UsedCost!=0)throw new InvalidOperationException("GLM official two-decimal truncation failed.");
        if(quota.Provider!="zhipu"||quota.Balance!=28.5||quota.UsedCost!=9.75||quota.Currency!="CNY"||quota.WeeklyPercent!=null)
            throw new InvalidOperationException("GLM available balance/currency semantics failed.");
        foreach(var value in new[]{0,-1.25,123456.78}) {
            var parsed=DomesticQuotaService.Parse("zhipu",JsonSerializer.Serialize(new{code=200,data=new{availableBalance=value}}));
            if(parsed.Balance!=value)throw new InvalidOperationException("GLM zero/debt/amount precision failed.");
        }
        foreach(var bad in new[]{"{}","{\"code\":401,\"data\":{\"availableBalance\":9}}",
            "{\"code\":200,\"data\":{\"balance\":99}}","{\"code\":200,\"data\":{\"availableBalance\":\"NaN\"}}",
            "{\"code\":200,\"data\":{\"availableBalance\":null}}","{\"code\":200,\"data\":{\"tokens\":123}}"}) {
            bool rejected=false;try{DomesticQuotaService.Parse("zhipu",bad);}catch(JsonException){rejected=true;}
            if(!rejected)throw new InvalidOperationException("GLM invalid response accepted.");
        }
        const string endpoint="https://bigmodel.cn/api/biz/account/query-customer-account-report";
        const string overview="https://bigmodel.cn/api/biz/account/finance-overview";
        if(!ZhipuBalance.IsEndpoint(overview)||!ZhipuBalance.IsEndpoint(overview+"?t=1"))throw new InvalidOperationException("Current official endpoint rejected.");
        foreach(var (amount,expected) in new[]{(0d,0d),(-1.255,-1.26),(99.9993936,100d),(123456.785,123456.79)}) {
            using var doc=JsonDocument.Parse(JsonSerializer.Serialize(new{code=200,data=new{
                availableTotalBalance=amount.ToString(System.Globalization.CultureInfo.InvariantCulture),
                availableBalance=999, balance=999, frozenTotalBalance=50, totalSpendAmount=99,
                accountList=new[]{new{type=1,balance=999}}}}));
            var current=ZhipuBalance.Parse(doc.RootElement,financeOverview:true);
            if(current.Balance!=expected||current.UsedCost!=null||current.Currency!="CNY"
                ||ZhipuBalance.Normalize(current)?.Balance!=expected)
                throw new InvalidOperationException("Current GLM overview amount/rounding/unknown spend failed.");
        }
        foreach(var bad in new[]{"{}","{\"code\":401,\"data\":{\"availableTotalBalance\":9}}",
            "{\"code\":200,\"data\":{\"availableBalance\":99,\"balance\":99}}",
            "{\"code\":200,\"data\":{\"availableTotalBalance\":null,\"availableBalance\":99}}",
            "{\"code\":200,\"data\":{\"availableTotalBalance\":\"NaN\"}}",
            "{\"code\":200,\"data\":{\"availableTotalBalance\":true}}",
            "{\"code\":200,\"data\":{\"availableTotalBalance\":1e100}}"}) {
            bool rejected=false;try{using var doc=JsonDocument.Parse(bad);ZhipuBalance.Parse(doc.RootElement,true);}catch(JsonException){rejected=true;}
            if(!rejected)throw new InvalidOperationException("Invalid current GLM overview accepted.");
        }
        foreach(var url in new[]{overview.Replace("https:","http:"),overview.Replace("bigmodel.cn","bigmodel.cn.evil.test"),
            overview.Replace("bigmodel.cn","bigmodel.cn:444"),overview+"/other",overview.Replace("finance-overview","query-org-owner-account-report")})
            if(ZhipuBalance.IsEndpoint(url))throw new InvalidOperationException("Untrusted current endpoint accepted.");
        if(!ZhipuBalance.IsEndpoint(endpoint)||!ZhipuBalance.IsEndpoint(endpoint+"?t=1"))throw new InvalidOperationException("Official endpoint rejected.");
        foreach(var url in new[]{endpoint.Replace("https:","http:"),endpoint.Replace("bigmodel.cn","bigmodel.cn.evil.test"),
            endpoint.Replace("bigmodel.cn","evil.test/bigmodel.cn"),endpoint.Replace("customer","org-owner"),"https://bigmodel.cn/api/monitor/usage/quota/limit"})
            if(ZhipuBalance.IsEndpoint(url))throw new InvalidOperationException("Untrusted/unrelated endpoint accepted.");
        var snapshot=new DomesticQuotaSnapshot(null,null,null,null,quota);
        var roundtrip=JsonSerializer.Deserialize<DomesticQuotaSnapshot>(JsonSerializer.Serialize(snapshot,JsonDefaults.Options),JsonDefaults.Options);
        if(roundtrip?.Zhipu!=quota||roundtrip.DeepSeek!=null||!DisplayModes.IsValid("domestic_zhipu")||DisplayModes.DomesticPage("zhipu")!="domestic_zhipu")
            throw new InvalidOperationException("GLM status or mode routing failed.");
        var now=DateTimeOffset.UtcNow;
        var status=new StatusSnapshot(1,"12:00",now.ToUnixTimeSeconds(),28800,now,new("idle",0),new("idle",0),DomesticQuotas:snapshot);
        var output=Path.Combine(Environment.CurrentDirectory,"artifacts","zhipu");Directory.CreateDirectory(output);
        using(var page=MirrorForm.RenderSnapshot(status,"domestic_zhipu"))page.Save(Path.Combine(output,"glm-balance-fixture.png"));
        using(var page=MirrorForm.RenderSnapshot(status with {DomesticQuotas=null},"domestic_zhipu"))page.Save(Path.Combine(output,"glm-awaiting-auth.png"));
        Console.WriteLine("ZHIPU_SELF_TEST_OK parsing, zero/debt, invalid responses, trusted endpoint, JSON, routing; previews are fixtures only");
    }

    internal static async Task RunDeviceAsync()
    {
        using var stop=new CancellationTokenSource(TimeSpan.FromSeconds(30));
        using var runtime=new BridgeRuntime(startRefresh:false);
        var serial=new SerialPublisher(null,BridgeSettings.Load().Get("serial_port"));
        var worker=serial.RunAsync(runtime.Capture,()=>[],stop.Token);
        UsbDeviceInfo? original=null;
        try {
            while(serial.PortName is null)await Task.Delay(200,stop.Token);
            original=serial.ReadDeviceInfo();
            if(!serial.SendDisplayMode("domestic_zhipu"))throw new IOException("GLM mode rejected.");
            await Task.Delay(3000,stop.Token);
            var device=serial.ReadDeviceInfo();
            bool available=runtime.Capture().DomesticQuotas?.Zhipu?.Balance is not null;
            if(!device.UsbActive||device.Mode!="domestic_zhipu"||device.PageData!.Value.GetProperty("zhipu").GetBoolean()!=available)
                throw new IOException("GLM production data/device routing mismatch.");
            if(available) {
                var host=runtime.Capture().DomesticQuotas!.Zhipu!;
                var page=device.PageData!.Value;
                if(Math.Abs(page.GetProperty("zhipu_balance").GetDouble()-host.Balance!.Value)>0.005
                    ||page.GetProperty("zhipu_currency").GetString()!=host.Currency)
                    throw new IOException("GLM device balance/currency differs from production cache.");
                Console.WriteLine("ZHIPU_REAL_BALANCE_MATCH_OK cache_reload/USB/device_amount/currency");
            }
            Console.WriteLine($"ZHIPU_DEVICE_OK port={serial.PortName} mode={device.Mode} real_balance_available={available}");
        } finally {
            if(original is not null)serial.SendDisplayMode(original.Mode);
            stop.Cancel();try{await worker;}catch(OperationCanceledException)when(stop.IsCancellationRequested){}
        }
    }
}
