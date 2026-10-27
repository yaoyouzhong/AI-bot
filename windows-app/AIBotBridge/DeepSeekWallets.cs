using System.Globalization;
using System.Text.Json;

namespace AIBotBridge;

// Wallet rows are per-currency aggregates, not transactions. Repeated wrappers
// must agree; summing them would count the same money twice.
internal static class DeepSeekWallets
{
    internal sealed record Balance(double Total,double? Granted,double? ToppedUp,string Currency);
    internal static Balance? Parse(JsonElement root)
    {
        var groups=new Dictionary<string,Dictionary<string,decimal>>();
        var direct=new Dictionary<string,decimal>();bool valid=true;
        bool Amount(JsonElement row,string key,out decimal amount) {
            amount=0;return row.TryGetProperty(key,out var v)&&v.ValueKind is JsonValueKind.String or JsonValueKind.Number&&
                decimal.TryParse(v.ToString(),NumberStyles.Float,CultureInfo.InvariantCulture,out amount)&&amount is > -1000000000000000m and <1000000000000000m;
        }
        void Add(Dictionary<string,decimal> target,string currency,decimal amount) {
            if(target.TryGetValue(currency,out var old)&&old!=amount)valid=false;else target[currency]=amount;
        }
        string? Currency(JsonElement row)=>row.TryGetProperty("currency",out var v)&&v.ValueKind==JsonValueKind.String&&v.GetString() is "CNY" or "USD"?v.GetString():null;
        void Visit(JsonElement node,int depth) {
            if(depth>32){valid=false;return;}
            if(node.ValueKind==JsonValueKind.Array){foreach(var child in node.EnumerateArray())Visit(child,depth+1);return;}
            if(node.ValueKind!=JsonValueKind.Object)return;
            foreach(var key in new[]{"normal_wallets","bonus_wallets","balance_infos"}) {
                if(!node.TryGetProperty(key,out var rows))continue;
                if(rows.ValueKind!=JsonValueKind.Array){valid=false;continue;}
                if(!groups.TryGetValue(key,out var amounts))groups[key]=amounts=new();
                foreach(var row in rows.EnumerateArray()) {
                    if(row.ValueKind!=JsonValueKind.Object||Currency(row) is not {} currency){valid=false;continue;}
                    string field=key=="balance_infos"?"total_balance":row.TryGetProperty("available_balance",out _)?"available_balance":"balance";
                    if(!Amount(row,field,out var amount)){valid=false;continue;}
                    Add(amounts,currency,amount);
                }
            }
            if(Currency(node) is {} code&&Amount(node,"total_balance",out var total))Add(direct,code,total);
            foreach(var property in node.EnumerateObject())
                if(property.Name is not ("normal_wallets" or "bonus_wallets" or "balance_infos"))Visit(property.Value,depth+1);
        }
        Visit(root,0);if(!valid)return null;
        var totals=groups.GetValueOrDefault("balance_infos")??direct;
        var normal=groups.GetValueOrDefault("normal_wallets");var bonus=groups.GetValueOrDefault("bonus_wallets");
        if(normal is not null&&bonus is not null)
            foreach(var code in normal.Keys.Union(bonus.Keys))
                if(!totals.ContainsKey(code))totals[code]=normal.GetValueOrDefault(code)+bonus.GetValueOrDefault(code);
        string currency=totals.ContainsKey("CNY")?"CNY":"USD";
        if(!totals.TryGetValue(currency,out var balance))return null;
        return new((double)balance,bonus is null?null:(double)bonus.GetValueOrDefault(currency),normal is null?null:(double)normal.GetValueOrDefault(currency),currency);
    }
}
