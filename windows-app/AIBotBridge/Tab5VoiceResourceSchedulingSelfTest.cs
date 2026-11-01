using System.Buffers.Binary;

namespace AIBotBridge;

internal sealed partial class Tab5Service
{
    internal static async Task VoiceResourceSchedulingSelfTestAsync()
    {
        long now=1000;var priority=new Tab5VoiceTransportPriority(()=>now);
        Tab5VoiceReply Reply(string id,string state)=>new(id,"task","dji",state,"","preview");
        void Check(bool value,string label){if(!value)throw new InvalidOperationException(label);}
        Check(!priority.Active,"idle must not reserve resources");
        priority.Observe("start",Reply("one","recording"),"USB","start");
        Check(priority.Active,"recording reserves terminal delivery");
        priority.Observe("poll",Reply("one","recognizing"),"USB","recognizing");
        now+=2000;Check(priority.Active,"recognition completion gap remains reserved");
        priority.Observe("poll",Reply("old","review"),"USB","stale");priority.Acknowledge("stale");
        Check(priority.Active,"old take cannot release reservation");
        priority.Observe("poll",Reply("one","review"),"USB","final");priority.Acknowledge("recognizing");
        Check(priority.Active,"only final matching nonce releases reservation");
        priority.Acknowledge("final");Check(!priority.Active,"final ACK releases");
        priority.Observe("start",Reply("two","recording"),"USB","start2");
        priority.Observe("cancel",Reply("two","cancelled"),"USB","cancel2");
        priority.Acknowledge("final");Check(priority.Active,"old ACK cannot release new take");
        now+=30000;Check(!priority.Active,"lost terminal ACK has bounded reservation");
        foreach(string transport in new[]{"Wi-Fi","BLE"}) {
            priority.Observe("start",Reply(transport,"recording"),transport,"start");
            priority.Observe("poll",Reply(transport,"error"),transport,"error");
            Check(!priority.Active,"non-USB terminal reply releases reservation");
        }
        foreach(bool fail in new[]{false,true}) {
            using var service=new Tab5Service();
            var responseReady=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            byte[] request=new byte[132];BinaryPrimitives.WriteUInt32LittleEndian(request,1);
            BinaryPrimitives.WriteUInt16LittleEndian(request.AsSpan(6),124);
            string nonce=new('a',32);System.Text.Encoding.ASCII.GetBytes(nonce).CopyTo(request,8);
            service._voiceTransportPriority.Observe("start",Reply("wire","recording"),"USB","start");
            service._voiceTransportPriority.Observe("poll",Reply("wire","review"),"USB",nonce);
            int writes=0;
            var pump=new Tab5BleVoice(_=>Task.FromResult(request),async (_,ct)=> {
                responseReady.TrySetResult();await release.Task.WaitAsync(ct);
                if(fail&&writes==1)throw new IOException("fixture response disconnect after first fragment");writes++;
                Check(service._voiceTransportPriority.Active,"terminal priority must survive every partial ACK");
            },(_,_,_,_)=>Task.FromResult<(int,byte[]?)>((200,new byte[5000])),247,responseChunk:2048,
                responseAccepted:service._voiceTransportPriority.Acknowledge);
            service._usbRpcPump=pump;
            var running=pump.PumpAsync(CancellationToken.None);
            await responseReady.Task.WaitAsync(TimeSpan.FromSeconds(2));
            bool protectedReply=service.BackgroundAssetTransferPaused;
            release.TrySetResult();
            try{await running;}catch(IOException) when(fail){}
            if(!protectedReply)throw new InvalidOperationException("Terminal reply fragments compete with USB background resources");
            if(service.BackgroundAssetTransferPaused!=fail||writes!=(fail?1:3))throw new InvalidOperationException("Reply scheduling released before all ACKs or lost fragments");
        }
        Console.WriteLine("TAB5_VOICE_RESOURCE_PRIORITY_OK final-poll gap; matching take/nonce; all fragment ACKs; failed delivery retained; bounded expiry; status remains enabled");
    }
}
