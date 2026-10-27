using System.Buffers.Binary;

namespace AIBotBridge;

internal sealed partial class Tab5Service
{
    internal static async Task VoiceResourceSchedulingSelfTestAsync()
    {
        foreach(bool fail in new[]{false,true}) {
            using var service=new Tab5Service();
            var responseReady=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            byte[] request=new byte[132];BinaryPrimitives.WriteUInt32LittleEndian(request,1);
            BinaryPrimitives.WriteUInt16LittleEndian(request.AsSpan(6),124);
            int writes=0;
            var pump=new Tab5BleVoice(_=>Task.FromResult(request),async (_,ct)=> {
                responseReady.TrySetResult();await release.Task.WaitAsync(ct);
                if(fail)throw new IOException("fixture response disconnect");writes++;
            },(_,_,_,_)=>Task.FromResult<(int,byte[]?)>((200,new byte[5000])),247,responseChunk:2048);
            service._usbRpcPump=pump;
            var running=pump.PumpAsync(CancellationToken.None);
            await responseReady.Task.WaitAsync(TimeSpan.FromSeconds(2));
            bool protectedReply=service.BackgroundAssetTransferPaused;
            release.TrySetResult();
            try{await running;}catch(IOException) when(fail){}
            if(!protectedReply)throw new InvalidOperationException("Terminal reply fragments compete with USB background resources");
            if(service.BackgroundAssetTransferPaused||writes!=(fail?0:3))throw new InvalidOperationException("Reply scheduling lease leaked or lost fragments");
        }
        Console.WriteLine("TAB5_VOICE_RESOURCE_PRIORITY_OK terminal reply retained through all writes; disconnect releases; status remains enabled");
    }
}
