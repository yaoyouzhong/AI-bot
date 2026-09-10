using System.Globalization;

namespace AIBotBridge;

internal static class DomesticDisplayText
{
    internal static string Percent(double? value)=>value is double number?((int)Math.Clamp(number,0,100)).ToString(CultureInfo.InvariantCulture):"--";
    internal static string Membership(string provider,string? value,bool balance)
    {
        if((provider=="deepseek"||provider=="zhipu")&&balance)return "API PAYG";
        if(string.IsNullOrWhiteSpace(value))return "";
        if(provider=="minimax")return value.ToUpperInvariant();
        if(provider!="alibaba")return value;
        string[][] names=[["CODING PLAN","coding"],["TEAM","团队","team"],["ENTERPRISE","企业","enterprise"],
            ["PERSONAL","个人","personal","individual"],["PRO","专业","professional"],["STANDARD","标准","standard"],["BASIC","基础","basic"]];
        return names.FirstOrDefault(row=>row.Skip(1).Any(key=>value.Contains(key,StringComparison.OrdinalIgnoreCase)))?[0]??"TOKEN PLAN";
    }
}
