namespace AIBotBridge;

internal static class Tab5BleOtaEstimate
{
    internal const int TargetSeconds=120;
    internal static double? Seconds(string result,int firmwareBytes) {
        if(firmwareBytes is <1024 or >0x6e0000)return null;
        var rows=result.Split(';',StringSplitOptions.RemoveEmptyEntries);
        if(rows.Length!=4||rows[0]!="complete")return null;
        var repeats=new HashSet<int>();double slowest=double.MaxValue;
        foreach(string row in rows.Skip(1)) {
            var f=row.Split(',');
            if(f.Length!=15||f[0]!="BLE"||f[1]!="down"||!int.TryParse(f[2],out int repeat)||repeat is <1 or >3||!repeats.Add(repeat)||
               f[3]!="262144"||!long.TryParse(f[4],out long ms)||ms is <=0 or >25000||f[5]!="0")return null;
            slowest=Math.Min(slowest,262144000.0/ms);
        }
        // Conservative screening, not a measured OTA duration: use the slowest
        // complete round, 20% transfer margin and 25s for Flash/verify/reboot.
        return firmwareBytes/slowest*1.2+25;
    }
    internal static string Format(string result,int firmwareBytes) {
        var rows=result.Split(';',StringSplitOptions.RemoveEmptyEntries);
        if(rows.Length>1&&rows[0]=="failed") {
            var data=rows.Skip(1).Select(row=>row.Split(',')).ToArray();
            if(data.All(f=>f.Length==15&&f[0]=="BLE"&&f[1]=="down")&&data[^1][5]=="-2") {
                int completed=data.Count(f=>f[3]=="262144"&&f[5]=="0");
                return $"蓝牙预检达到 20 秒传输预算，已完成 {completed}/3 轮；速度未满足本次预检条件，暂不安排整包升级。固件未被擦写。";
            }
        }
        if(Seconds(result,firmwareBytes) is not {} seconds)return "预检无有效结论：需仅蓝牙下载三轮全部通过，并已选择升级固件；不安排整包升级。";
        return $"整包耗时预估 {seconds:F0} 秒（最慢轮次、20% 传输余量及 25 秒写入/校验/重启预算）。"+
            (seconds<=TargetSeconds?"符合 2 分钟预检目标，仍需完整 OTA 验收；不会自动刷机。":"超过 2 分钟目标，暂不安排蓝牙整包升级。")+
            "此值是短时估算，不是实测升级耗时。";
    }
}
