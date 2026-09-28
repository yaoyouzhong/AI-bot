using System.Text.Json;
using System.Text.Json.Serialization;

namespace AIBotBridge;

[JsonConverter(typeof(JsonStringEnumConverter<HardwareKind>))]
internal enum HardwareKind { Esp8266, Tab5 }
internal sealed record RegisteredDevice(string Id,HardwareKind Kind,string Name,bool Enabled,string? HardwareId,string[] Sources,string[] Providers) {public string? UsbIdentity {get;init;} public DateTimeOffset CreatedAt {get;init;}=DateTimeOffset.UtcNow;}
internal sealed record DeviceRegistry(int Version,bool MigrationComplete,bool LegacyDecisionPending,bool DesktopQuotaHistory,RegisteredDevice[] Devices);

// Non-secret metadata only. Existing pairing and user content stores remain authoritative.
internal sealed class DeviceRegistryStore
{
    private readonly string _path;
    private readonly object _gate=new();
    private DeviceRegistry _value;
    internal static readonly string[] SourceIds=["activity","quotas","weather","stocks","music","system"];
    internal static readonly string[] ProviderIds=["qwen","kimi","minimax","deepseek","zhipu","stepfun","baidu","xiaomi"];
    internal static string Model(HardwareKind kind)=>kind==HardwareKind.Tab5?"TAB5 平板":"ESP8266 小屏";
    internal DeviceRegistryStore(string? path=null) {
        _path=path??Path.Combine(AppPaths.GetFolderPath(Environment.SpecialFolder.ApplicationData),"AI-bot","devices.json");
        _value=new(1,false,false,false,[]);
        if(File.Exists(_path)) {
            if(new FileInfo(_path).Length>65536)throw new InvalidDataException("设备清单过大，请恢复备份。");
            try{_value=JsonSerializer.Deserialize<DeviceRegistry>(File.ReadAllText(_path))??throw new InvalidDataException("设备清单为空。");Validate(_value);}
            catch(JsonException ex){throw new InvalidDataException("设备清单无法读取，原文件已保留。",ex);}
        }
    }
    internal DeviceRegistry Snapshot {get {lock(_gate)return _value with {Devices=_value.Devices.Select(Clone).ToArray()};}}
    private static RegisteredDevice Clone(RegisteredDevice d)=>d with {Sources=d.Sources.ToArray(),Providers=d.Providers.ToArray()};
    internal void Migrate(string? pairedTab5,bool hasLegacyConnection,bool desktopQuotaHistory) {
        lock(_gate) {
            if(_value.MigrationComplete)return;
            if(pairedTab5 is not null&&!Tab5Protocol.ValidId(pairedTab5))throw new InvalidDataException("已有 TAB5 配对无效，未迁移。");
            Save(_value with {MigrationComplete=true,LegacyDecisionPending=hasLegacyConnection,DesktopQuotaHistory=desktopQuotaHistory,
                Devices=pairedTab5 is null?[]:[Create(HardwareKind.Tab5,Model(HardwareKind.Tab5),pairedTab5)]});
        }
    }
    internal static RegisteredDevice Create(HardwareKind kind,string name,string? hardwareId)=>new(Guid.NewGuid().ToString("N"),kind,name.Trim(),true,hardwareId,SourceIds.ToArray(),kind==HardwareKind.Tab5?["deepseek","zhipu"]:[]);
    internal void Add(RegisteredDevice device) {
        lock(_gate) {
            if(_value.Devices.Any(d=>d.Kind==device.Kind))throw new InvalidOperationException("当前每种型号支持一台，请先管理已有设备。");
            Save(_value with {Devices=[.._value.Devices,Clone(device)],LegacyDecisionPending=device.Kind==HardwareKind.Esp8266?false:_value.LegacyDecisionPending});
        }
    }
    internal void Update(RegisteredDevice device) {
        lock(_gate) {
            var old=_value.Devices.SingleOrDefault(d=>d.Id==device.Id)??throw new InvalidOperationException("设备已移除。");
            if(old.Kind!=device.Kind||old.HardwareId!=device.HardwareId)throw new InvalidOperationException("设备身份不可通过设置替换。");
            Save(_value with {Devices=_value.Devices.Select(d=>d.Id==device.Id?Clone(device):d).ToArray()});
        }
    }
    internal void Remove(string id) {lock(_gate)Save(_value with {Devices=_value.Devices.Where(d=>d.Id!=id).ToArray()});}
    internal void DismissLegacy(){lock(_gate)Save(_value with {LegacyDecisionPending=false});}
    internal void SetDesktopQuotaHistory(bool value){lock(_gate)Save(_value with {DesktopQuotaHistory=value});}
    private void Save(DeviceRegistry value) {
        Validate(value);Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_path))!);
        var temporary=_path+".tmp";
        File.WriteAllText(temporary,JsonSerializer.Serialize(value,new JsonSerializerOptions {WriteIndented=true}));
        // Replace preserves a known-readable previous registry. Never rewrite legacy configuration.
        if(File.Exists(_path))File.Replace(temporary,_path,_path+".bak",true);else File.Move(temporary,_path);
        _value=value;
    }
    private static void Validate(DeviceRegistry value) {
        if(value.Version!=1||value.Devices is null||value.Devices.Length>2)throw new InvalidDataException("设备清单版本不兼容或内容无效，原文件已保留。");
        if(value.Devices.Any(d=>d is null)||value.Devices.Select(d=>d.Id).Distinct().Count()!=value.Devices.Length||value.Devices.Select(d=>d.Kind).Distinct().Count()!=value.Devices.Length)throw new InvalidDataException("设备清单包含重复设备。");
        foreach(var d in value.Devices) {
            if(!Guid.TryParseExact(d.Id,"N",out _)||!Enum.IsDefined(d.Kind)||string.IsNullOrWhiteSpace(d.Name)||d.Name.Length>40||d.Name.Any(char.IsControl)||
                d.Sources is null||d.Providers is null||d.Sources.Any(s=>!SourceIds.Contains(s))||d.Providers.Any(p=>!ProviderIds.Contains(p))||
                (d.UsbIdentity is not null&&!FlashDeviceDiscovery.IsUsbIdentity(d.UsbIdentity))||d.Sources.Distinct().Count()!=d.Sources.Length||d.Providers.Distinct().Count()!=d.Providers.Length||
                (d.Kind==HardwareKind.Tab5?!Tab5Protocol.ValidId(d.HardwareId):d.HardwareId is not null))throw new InvalidDataException("设备名称、身份或数据选择无效。");
        }
    }
}
