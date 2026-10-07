namespace AIBotBridge;

internal static class Tab5FirmwareProbe
{
    internal static double? Seconds(string result,string sha,int bytes) {
        if(bytes is <1024 or >0x6e0000||sha.Length!=64||sha.Any(c=>!char.IsAsciiHexDigit(c)))return null;
        var rows=result.Split(';',StringSplitOptions.RemoveEmptyEntries);
        if(rows.Length!=3||rows[0]!="complete"||rows[2]!=$"image,{sha},{bytes}")return null;
        var f=rows[1].Split(',');
        if(f.Length!=15||f[0]!="BLE"||f[1]!="down"||f[2]!="1"||f[3]!=bytes.ToString(System.Globalization.CultureInfo.InvariantCulture)||f[5]!="0"||
            !long.TryParse(f[4],out long ms)||ms is <=0 or >=110000||f.Skip(6).Any(v=>!ulong.TryParse(v,out _)))return null;
        return ms/1000.0*1.2+25;
    }
    internal static string Format(string result,string sha,int bytes) {
        if(Seconds(result,sha,bytes) is not {} seconds)return "完整固件预检未通过：需所选镜像完整接收、解压和 SHA-256 一致；暂不安排蓝牙整包升级。";
        return $"完整固件接收及 SHA-256 校验通过。整包升级预估 {seconds:F0} 秒（实测接收时间加 20% 余量及 25 秒写入/校验/重启预算）。"+
            (seconds<=120?"符合 2 分钟预检目标，仍需完整 OTA 验收。":"超过 2 分钟目标，暂不安排蓝牙整包升级。")+"预检未写 Flash，此值不是实测升级耗时。";
    }
}
