using System.Security.Cryptography;

namespace AIBotBridge;

internal sealed partial class Tab5Service
{
    internal Tab5ScreenPreview ScreenPreview {get;}=new();
    internal Tab5ScreenPreview.Context? ScreenPreviewContext {
        get {
            var pair=_store.Current;if(pair is null)return null;
            long now=Environment.TickCount64;
            bool Recent(long at)=>at>0&&now>=at&&now-at<8000;
            HashSet<string> transports=[];
            if(Recent(Interlocked.Read(ref _usbAckAt)))transports.Add("USB");
            if(WifiBridgeReady(now,Interlocked.Read(ref _wifiRequestAt)))transports.Add("Wi-Fi");
            if(Recent(Interlocked.Read(ref _bleAckAt)))transports.Add("BLE");
            string host;
            try{host=Convert.ToHexString(SHA256.HashData(Convert.FromBase64String(pair.Key))).ToLowerInvariant();}
            catch(FormatException){return null;}
            return new(pair.DeviceId,host,transports,!_stopping&&!BackgroundTransferPaused&&
                Volatile.Read(ref _benchmarkRunning)==0&&Volatile.Read(ref _installBusy)==0&&
                !ImageTransferPending&&!(_voice is Tab5VoiceHost voice&&voice.Busy),Tab5ScreenPreview.SupportsHD(_firmware.LastVersion(pair.DeviceId)),Tab5ScreenPreview.SupportsResponsiveCapture(_firmware.LastVersion(pair.DeviceId)));
        }
    }
}
