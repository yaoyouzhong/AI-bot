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
    Func<string,string,byte[],CancellationToken,Task<(int Status,byte[]? Packet)>>? voiceHandler=null,
    Func<string,string,byte[],CancellationToken,Task<(int Status,byte[]? Packet)>>? rpcHandler=null,
    Func<int,bool,byte[]?>? telemetryCapture=null,Action<string>? rpcDiagnostic=null,Action? resetTelemetry=null,Func<bool>? imageUploadActive=null,Action<string>? voiceDiagnostic=null,Action<long>? telemetryAcknowledged=null,Action<string>? firmwareObserved=null)
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
                            if(!Authenticate(first.RootElement,pairing,out var nonce)) {
                                if(first.RootElement.TryGetProperty("deviceId",out var rejectedId)&&rejectedId.GetString()==pairing.DeviceId) {
                                    diagnostic?.Invoke("FAILED 配对认证失败；设备身份匹配，请通过 USB 重新验证配对");
                                    status("配对认证失败，请通过 USB 重新验证配对");
                                }
                                continue;
                            }
                            ReadAssets(first.RootElement,pairing,nonce);
                            int telemetry=first.RootElement.TryGetProperty("telemetryVersion",out var tv)&&tv.TryGetInt32(out int version)&&version is >=1 and <=3?version:0;
                            int credits=first.RootElement.TryGetProperty("mailboxWindow",out var wc)&&wc.TryGetInt32(out int offered)?Math.Clamp(offered,1,8):4;
                            int rpcCredits=first.RootElement.TryGetProperty("rpcMailboxWindow",out var rw)&&rw.TryGetInt32(out int rpcOffered)?Math.Clamp(rpcOffered,1,8):credits; // Larger grants failed hardware acceptance; retain the proven eight-packet bound.
                            int rpcNotifyCredits=first.RootElement.TryGetProperty("rpcNotifyWindow",out var nw)&&nw.TryGetInt32(out int notifyOffered)?Math.Clamp(notifyOffered,1,32):rpcCredits;
                            using var transferGate=new SemaphoreSlim(1,1);
                            Tab5BleVoice? voice=null;
                            var voices=await service.GetCharacteristicsForUuidAsync(new Guid("7af50004-7f23-4a91-bc65-667a19320101"),BluetoothCacheMode.Uncached).AsTask(timeout.Token);
                            using var voiceMailbox=voiceHandler is not null&&voices.Status==GattCommunicationStatus.Success&&voices.Characteristics.Count==1
                                ?await Tab5BleGattMailbox.CreateAsync(voices.Characteristics[0],(int)session.MaxPduSize,timeout.Token,transferGate,credits):null;
                            if(voiceMailbox is not null) {
                                var window=voiceMailbox.Window;
                                voice=new Tab5BleVoice(window.ReadAsync,window.WriteAsync,voiceHandler!,
                                    (int)session.MaxPduSize,responseChunk:window.ResponseChunk,transportMode:window.Mode);
                            }
                            Tab5BleVoice? rpc=null;
                            var endpoints=await service.GetCharacteristicsForUuidAsync(new Guid("7af50005-7f23-4a91-bc65-667a19320101"),BluetoothCacheMode.Uncached).AsTask(timeout.Token);
                            using var rpcMailbox=rpcHandler is not null&&endpoints.Status==GattCommunicationStatus.Success&&endpoints.Characteristics.Count==1
                                ?await Tab5BleGattMailbox.CreateAsync(endpoints.Characteristics[0],(int)session.MaxPduSize,timeout.Token,transferGate,rpcCredits,rpcNotifyCredits):null;
                            if(rpcMailbox is not null) {
                                var window=rpcMailbox.Window;
                                rpc=new Tab5BleVoice(window.ReadAsync,window.WriteAsync,rpcHandler!,
                                    (int)session.MaxPduSize,Tab5RpcBinary.MailboxMaximum,75,responseChunk:window.ResponseChunk,transportMode:window.Mode,maximumRequest:Tab5RpcBinary.MailboxMaximum,bulkPriority:imageUploadActive);
                            }
                            timeout.CancelAfter(Timeout.InfiniteTimeSpan);
                            resetTelemetry?.Invoke();
                            status("设备已认证，等待数据确认");
                            stage="发送数据";
                            using var voiceStop=CancellationTokenSource.CreateLinkedTokenSource(token);
                            var rpcTask=rpc is null?Task.CompletedTask:PumpVoiceAsync(rpc,"RPC",voiceStop.Token,rpcDiagnostic,()=>rpcMailbox!.QueuedMilliseconds,()=>rpcMailbox!.ReceiveDiagnostic);
                            var voiceTask=voice is null?Task.CompletedTask:PumpVoiceAsync(voice,"voice",voiceStop.Token,voiceDiagnostic,()=>voiceMailbox!.QueuedMilliseconds,bulkActive:()=>rpc?.BulkActive==true||imageUploadActive?.Invoke()==true);
                            long telemetryAt=Environment.TickCount64;
                            try {
                            while(!token.IsCancellationRequested && store.Current?.DeviceId==pairing.DeviceId) {
                                if(voiceTask.IsFaulted)await voiceTask;
                                if(rpcTask.IsFaulted)await rpcTask;
                                // A large inbound RPC already proves a live paired link. Avoid
                                // competing ATT status reads/writes through handling and response.
                                // Keep voice polling independent so a recording can still start.
                                if(DeferTelemetry(Environment.TickCount64,telemetryAt,rpc?.BulkActive==true,imageUploadActive?.Invoke()==true)){await Task.Delay(50,token);continue;}
                                telemetryAt=Environment.TickCount64;
                                bool bulkActive=imageUploadActive?.Invoke()==true||voice is not null&&voice.LastActive>0&&Environment.TickCount64-voice.LastActive<1500;
                                int cadence=telemetry>0?(bulkActive?1000:250):2000;
                                int pauseMs=cadence;
                                    await Tab5BleTransfer.SerializeAsync(transferGate,async ()=> {
                                    if((telemetryCapture is null?capture():telemetryCapture(telemetry,rpc is not null&&rpc.LastActive>0&&Environment.TickCount64-rpc.LastActive<3000)) is not {} clear)return false;
                                    using var doc=JsonDocument.Parse(clear);var seq=doc.RootElement.GetProperty("sequence").GetInt64();
                                    var bytes=Tab5Protocol.WithLength(Tab5Protocol.Encrypt(Convert.FromBase64String(pairing.Key),nonce,clear));
                                    int size=Math.Clamp((int)session.MaxPduSize-3,20,488);
                                    bool fast=input.CharacteristicProperties.HasFlag(GattCharacteristicProperties.WriteWithoutResponse);
                                    int window=imageUploadActive?.Invoke()==true?4:(size>244?8:16);
                                    var started=System.Diagnostics.Stopwatch.StartNew();
                                    int sent=0;
                                    JsonDocument? confirmed=null;
                                    try {
                                        await Tab5BleTransfer.SendAsync(bytes,size,fast,
                                            async (part,ct)=> {
                                                using var writer=new DataWriter();writer.WriteBytes(part.ToArray());
                                                bool response=Tab5BleTransfer.RequiresResponse(sent,part.Length,bytes.Length,size,fast,window);
                                                stage=$"发送分片 {sent}/{bytes.Length} 字节，{(response?"等待分片确认":"连续发送")}，窗口 {window}，已用 {started.ElapsedMilliseconds}ms";
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
                                            },token,window);
                                        ReadAssets(confirmed!.RootElement,pairing,nonce);
                                        telemetryAcknowledged?.Invoke(seq);
                                        string link=confirmed.RootElement.TryGetProperty("link",out var ld)&&ld.ValueKind==JsonValueKind.Array&&ld.GetArrayLength()==10?"; link="+ld.GetRawText():"";
                                        if(confirmed.RootElement.TryGetProperty("mailboxStats",out var ms)&&ms.ValueKind==JsonValueKind.Array&&ms.GetArrayLength()==4)link+="; mailboxStats="+ms.GetRawText();
                                        if(confirmed.RootElement.TryGetProperty("rpcPacing",out var pacing)&&pacing.ValueKind==JsonValueKind.Array&&pacing.GetArrayLength()==3)link+="; rpcPacing="+pacing.GetRawText();
                                        diagnostic?.Invoke($"{bytes.Length} bytes, MTU {session.MaxPduSize}, {(fast?$"window{window}":"request")}, {started.Elapsed.TotalSeconds:F1}s, ACK verified{link}");
                                        status("已连接 · 数据已确认");
                                        // A slow complete transfer has already paced this loop.
                                        // Avoid adding two more seconds to the 8-second freshness budget.
                                        pauseMs=Math.Clamp(cadence-(int)started.ElapsedMilliseconds,20,cadence);
                                    } finally {confirmed?.Dispose();}
                                    return true;
                                    },token);
                                await Task.Delay(pauseMs,token);
                            }
                            } finally {voiceStop.Cancel();try{await Task.WhenAll(voiceTask,rpcTask);}catch(OperationCanceledException) when(voiceStop.IsCancellationRequested){}}
                            session.MaintainConnection=false;
                        }
                    } finally {foreach(var service in services.Services) service.Dispose();}
                }
            } catch(OperationCanceledException) when(token.IsCancellationRequested) {return;}
            catch(Exception ex) when(ex is COMException or IOException or InvalidOperationException or UnauthorizedAccessException or OperationCanceledException or JsonException or ArgumentException or KeyNotFoundException or NotSupportedException) {
                string source=ex.Message.StartsWith("RPC ")||ex.Message.StartsWith("voice ")?ex.Message:stage;
                diagnostic?.Invoke("FAILED "+source+"; "+ex.GetType().Name+" / 0x"+ex.HResult.ToString("X8"));
                status(ex is OperationCanceledException?"蓝牙通信超时，正在重连":"蓝牙连接中断，正在重连");
            }
            await Task.Delay(3000,token);
        }
    }
    internal static bool DeferTelemetry(long now,long lastTelemetry,bool receiving,bool uploadActive)=>(receiving||uploadActive)&&now-lastTelemetry<6000;
    internal static int VoicePollDelay(long now,long lastActive,bool bulkActive)=>now-lastActive<1000?15:bulkActive?500:250;
    private static async Task PumpVoiceAsync(Tab5BleVoice voice,string name,CancellationToken token,Action<string>? diagnostic=null,Func<long>? queuedTime=null,Func<string>? receiveDiagnostic=null,Func<bool>? bulkActive=null) {
        // Idle voice + RPC mailboxes share the same radio with status frames.
        // Polling both every 30 ms starves the status write queue on Windows.
        try {
            while(!token.IsCancellationRequested) {
                uint before=voice.CompletedCount;
                long queued=queuedTime?.Invoke()??0;
                bool active=await voice.PumpAsync(token);
                if(active)diagnostic?.Invoke($"queue={(queuedTime?.Invoke()??0)-queued}ms; {voice.Timing}"+(receiveDiagnostic is null?"":"; "+receiveDiagnostic()));
                // GATT adapters own the gate for one read/grant/write window;
                // notification waits and desktop handlers leave other flows free.
                if(voice.CompletedCount!=before)continue;
                await Task.Delay(VoicePollDelay(Environment.TickCount64,voice.LastActive,bulkActive?.Invoke()==true),token);
            }
        }catch(Exception ex) when(!token.IsCancellationRequested&&(ex is IOException or COMException or OperationCanceledException)) {
            throw new IOException(name+" "+voice.Progress,ex);
        }
    }
    private void ReadAssets(JsonElement info,Tab5Pairing pairing,string nonce) {
        if(info.TryGetProperty("firmware",out var fw)&&fw.ValueKind==JsonValueKind.String&&
            info.TryGetProperty("firmwareProof",out var fp)&&fp.ValueKind==JsonValueKind.String&&
            Tab5Protocol.Verify(Convert.FromBase64String(pairing.Key),"FIRMWARE|"+nonce+"|"+fw.GetString(),fp.GetString()))firmwareObserved?.Invoke(fw.GetString()!);
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
