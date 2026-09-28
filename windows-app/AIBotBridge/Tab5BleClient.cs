using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Text.Json;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Storage.Streams;

namespace AIBotBridge;

// Application encryption authenticates the USB-paired device without exposing its key over BLE.
internal sealed class Tab5BleClient(Tab5PairingStore store,Func<byte[]?> capture,Action<string> status,Action<string> assets,Action<string>? diagnostic=null,
    Func<string,string,byte[],CancellationToken,Task<(int Status,byte[]? Packet)>>? voiceHandler=null)
{
    internal async Task RunAsync(CancellationToken token)
    {
        while(!token.IsCancellationRequested) {
            if(store.Current is not { } pairing) {await Task.Delay(2000,token);continue;}
            string stage="检查蓝牙适配器";
            try {
                var adapter=await BluetoothAdapter.GetDefaultAsync().AsTask(token);
                if(adapter is null) {status("电脑未检测到蓝牙适配器");await Task.Delay(5000,token);continue;}
                var radio=await adapter.GetRadioAsync().AsTask(token);
                if(radio.State!=Windows.Devices.Radios.RadioState.On) {status("请在 Windows 设置中打开蓝牙");await Task.Delay(5000,token);continue;}
                stage="扫描";
                status("正在寻找已配对 TAB5");
                var candidates=new ConcurrentDictionary<ulong,BluetoothAddressType>();
                var watcher=new BluetoothLEAdvertisementWatcher {ScanningMode=BluetoothLEScanningMode.Active};
                watcher.AdvertisementFilter.Advertisement.ServiceUuids.Add(Tab5Protocol.Service);
                watcher.Received+=(_,e)=>candidates.TryAdd(e.BluetoothAddress,e.BluetoothAddressType);
                try {watcher.Start();await Task.Delay(4000,token);} finally {watcher.Stop();}
                if(candidates.IsEmpty) status("未发现已配对 TAB5，自动重试");
                foreach(var address in candidates.Keys) {
                    stage="连接设备";
                    using var device=await BluetoothLEDevice.FromBluetoothAddressAsync(address,candidates[address]).AsTask(token);
                    if(device is null) continue;
                    using var timeout=CancellationTokenSource.CreateLinkedTokenSource(token); timeout.CancelAfter(TimeSpan.FromSeconds(12));
                    using var session=await GattSession.FromDeviceIdAsync(device.BluetoothDeviceId).AsTask(timeout.Token);
                    session.MaintainConnection=true;
                    stage="发现 GATT 服务";
                    var services=await device.GetGattServicesAsync(BluetoothCacheMode.Uncached).AsTask(timeout.Token);
                    stage="读取设备服务";
                    if(services.Status!=GattCommunicationStatus.Success) continue;
                    try {
                        foreach(var service in services.Services.Where(s=>s.Uuid==Tab5Protocol.Service)) {
                            var infos=await service.GetCharacteristicsForUuidAsync(Tab5Protocol.Info,BluetoothCacheMode.Uncached).AsTask(timeout.Token);
                            var inputs=await service.GetCharacteristicsForUuidAsync(Tab5Protocol.Input,BluetoothCacheMode.Uncached).AsTask(timeout.Token);
                            if(infos.Status!=GattCommunicationStatus.Success || inputs.Status!=GattCommunicationStatus.Success || infos.Characteristics.Count!=1 || inputs.Characteristics.Count!=1) continue;
                            var info=infos.Characteristics[0];var input=inputs.Characteristics[0];
                            using var first=await ReadInfoAsync(info,timeout.Token);
                            stage="验证设备";
                            if(!Authenticate(first.RootElement,pairing,out var nonce)) continue;
                            ReadAssets(first.RootElement,pairing,nonce);
                            Tab5BleVoice? voice=null;
                            var voices=await service.GetCharacteristicsForUuidAsync(new Guid("7af50004-7f23-4a91-bc65-667a19320101"),BluetoothCacheMode.Uncached).AsTask(timeout.Token);
                            if(voiceHandler is not null&&voices.Status==GattCommunicationStatus.Success&&voices.Characteristics.Count==1) {
                                var endpoint=voices.Characteristics[0];
                                voice=new Tab5BleVoice(async ct=> {
                                    var value=await endpoint.ReadValueAsync(BluetoothCacheMode.Uncached).AsTask(ct);
                                    if(value.Status!=GattCommunicationStatus.Success||value.Value.Length>500)throw new IOException("BLE voice read failed");
                                    using var reader=DataReader.FromBuffer(value.Value);var bytes=new byte[value.Value.Length];reader.ReadBytes(bytes);return bytes;
                                },async (bytes,ct)=> {
                                    using var writer=new DataWriter();writer.WriteBytes(bytes);
                                    if(await endpoint.WriteValueAsync(writer.DetachBuffer(),GattWriteOption.WriteWithResponse).AsTask(ct)!=GattCommunicationStatus.Success)throw new IOException("BLE voice write failed");
                                },voiceHandler,(int)session.MaxPduSize);
                            }
                            timeout.CancelAfter(Timeout.InfiniteTimeSpan);
                            status("设备已认证，等待数据确认");
                            stage="发送数据";
                            using var voiceStop=CancellationTokenSource.CreateLinkedTokenSource(token);
                            // Separate bounded ATT operations on the SAME GATT session;
                            // status frames cannot hold up a voice request for seconds.
                            var voiceTask=voice is null?Task.CompletedTask:PumpVoiceAsync(voice,voiceStop.Token);
                            try {
                            while(!token.IsCancellationRequested && store.Current?.DeviceId==pairing.DeviceId) {
                                if(voiceTask.IsFaulted)await voiceTask;
                                int pauseMs=2000;
                                if(capture() is { } clear) {
                                    using var doc=JsonDocument.Parse(clear);var seq=doc.RootElement.GetProperty("sequence").GetInt64();
                                    var bytes=Tab5Protocol.WithLength(Tab5Protocol.Encrypt(Convert.FromBase64String(pairing.Key),nonce,clear));
                                    int size=Math.Clamp((int)session.MaxPduSize-3,20,244);
                                    bool fast=input.CharacteristicProperties.HasFlag(GattCharacteristicProperties.WriteWithoutResponse);
                                    var started=System.Diagnostics.Stopwatch.StartNew();
                                    int sent=0;
                                    JsonDocument? confirmed=null;
                                    try {
                                        await Tab5BleTransfer.SendAsync(bytes,size,fast,
                                            async (part,ct)=> {
                                                using var writer=new DataWriter();writer.WriteBytes(part.ToArray());
                                                bool response=Tab5BleTransfer.RequiresResponse(sent,part.Length,bytes.Length,size,fast);
                                                stage=$"发送分片 {sent}/{bytes.Length} 字节，{(response?"等待分片确认":"连续发送")}";
                                                if(await input.WriteValueAsync(writer.DetachBuffer(),response?GattWriteOption.WriteWithResponse:GattWriteOption.WriteWithoutResponse).AsTask(ct)!=GattCommunicationStatus.Success)
                                                    throw new IOException("BLE write failed");
                                                sent+=part.Length;
                                                stage=$"发送数据 {sent}/{bytes.Length} 字节，MTU {session.MaxPduSize}，{started.Elapsed.TotalSeconds:F1} 秒";
                                            },
                                            async ct=> {
                                                stage=$"等待整帧确认，{sent}/{bytes.Length} 字节，MTU {session.MaxPduSize}，已用 {started.Elapsed.TotalSeconds:F1} 秒";
                                                var candidate=await ReadInfoAsync(info,ct);
                                                if(!Authenticate(candidate.RootElement,pairing,out var nextNonce)||nextNonce!=nonce) {
                                                    candidate.Dispose();throw new IOException("BLE acknowledgement authentication failed");
                                                }
                                                if(candidate.RootElement.GetProperty("ack").GetInt64()!=seq){stage=$"整帧确认尚未匹配，{sent}/{bytes.Length} 字节，已用 {started.Elapsed.TotalSeconds:F1} 秒";candidate.Dispose();return false;}
                                                confirmed=candidate;return true;
                                            },token);
                                        ReadAssets(confirmed!.RootElement,pairing,nonce);
                                        diagnostic?.Invoke($"{bytes.Length} bytes, MTU {session.MaxPduSize}, {(fast?"window16":"request")}, {started.Elapsed.TotalSeconds:F1}s, ACK verified");
                                        status("已连接 · 数据已确认");
                                        // A slow complete transfer has already paced this loop.
                                        // Avoid adding two more seconds to the 8-second freshness budget.
                                        pauseMs=Math.Clamp(2000-(int)started.ElapsedMilliseconds,100,2000);
                                    } finally {confirmed?.Dispose();}
                                }
                                await Task.Delay(pauseMs,token);
                            }
                            } finally {voiceStop.Cancel();try{await voiceTask;}catch(OperationCanceledException) when(voiceStop.IsCancellationRequested){}}
                            session.MaintainConnection=false;
                        }
                    } finally {foreach(var service in services.Services) service.Dispose();}
                }
            } catch(OperationCanceledException) when(token.IsCancellationRequested) {return;}
            catch(Exception ex) when(ex is COMException or IOException or InvalidOperationException or UnauthorizedAccessException or OperationCanceledException or JsonException or ArgumentException or KeyNotFoundException or NotSupportedException) {
                diagnostic?.Invoke(stage+"; "+ex.GetType().Name+" / 0x"+ex.HResult.ToString("X8"));
                status(ex is OperationCanceledException?"蓝牙通信超时，正在重连":"蓝牙连接中断，正在重连");
            }
            await Task.Delay(3000,token);
        }
    }
    private static async Task PumpVoiceAsync(Tab5BleVoice voice,CancellationToken token) {
        while(!token.IsCancellationRequested){await voice.PumpAsync(token);await Task.Delay(30,token);}
    }
    private void ReadAssets(JsonElement info,Tab5Pairing pairing,string nonce) {
        if(info.TryGetProperty("assetIds",out var ids)&&ids.ValueKind==JsonValueKind.String&&
            info.TryGetProperty("assetProof",out var proof)&&proof.ValueKind==JsonValueKind.String&&Tab5Assets.ValidIds(ids.GetString())&&
            Tab5Protocol.Verify(Convert.FromBase64String(pairing.Key),"ASSETS|"+nonce+"|"+ids.GetString(),proof.GetString()))assets(ids.GetString()!);
    }
    private static async Task<JsonDocument> ReadInfoAsync(GattCharacteristic info,CancellationToken token) {
        var result=await info.ReadValueAsync(BluetoothCacheMode.Uncached).AsTask(token);
        if(result.Status!=GattCommunicationStatus.Success || result.Value.Length>2048) throw new IOException("BLE identity read failed");
        using var reader=DataReader.FromBuffer(result.Value);var bytes=new byte[result.Value.Length];reader.ReadBytes(bytes);
        return JsonDocument.Parse(bytes);
    }
    private static bool Authenticate(JsonElement info,Tab5Pairing pairing,out string nonce) {
        nonce="";
        if(!info.TryGetProperty("deviceId",out var id)||id.GetString()!=pairing.DeviceId || !info.TryGetProperty("nonce",out var n)||!Tab5Protocol.ValidNonce(n.GetString()) ||
           !info.TryGetProperty("ack",out var a)||!a.TryGetInt64(out var ack)|| !info.TryGetProperty("proof",out var p)) return false;
        nonce=n.GetString()!;
        return Tab5Protocol.Verify(Convert.FromBase64String(pairing.Key),nonce+"|"+ack.ToString(System.Globalization.CultureInfo.InvariantCulture),p.GetString());
    }
}
