using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AIBotBridge;

internal static class Tab5BleVoiceSelfTest
{
    internal static async Task<byte[]> ExchangeAsync(byte[] request,Func<string,string,byte[],CancellationToken,Task<(int Status,byte[]? Packet)>> handle)
    {
        int at=0;var reply=new List<byte>();
        var rpc=new Tab5BleVoice(ct=> {
            int n=Math.Min(480,request.Length-at);var p=new byte[8+n];
            BinaryPrimitives.WriteUInt32LittleEndian(p,1);BinaryPrimitives.WriteUInt16LittleEndian(p.AsSpan(4),(ushort)at);
            BinaryPrimitives.WriteUInt16LittleEndian(p.AsSpan(6),(ushort)request.Length);request.AsSpan(at,n).CopyTo(p.AsSpan(8));return Task.FromResult(p);
        },(p,ct)=> {
            if(p[0]==1)at=BinaryPrimitives.ReadUInt16LittleEndian(p.AsSpan(5));
            else {if(BinaryPrimitives.ReadUInt16LittleEndian(p.AsSpan(5))!=reply.Count)throw new Exception("BLE reply offset");reply.AddRange(p[9..]);}
            return Task.CompletedTask;
        },handle,247);
        await rpc.PumpAsync(CancellationToken.None);return reply.ToArray();
    }
    private static async Task VerifyBulkLifetimeAsync() {
        foreach(bool continuation in new[]{false,true})foreach(int fault in new[]{0,1,2}) {
            byte[] request=new byte[continuation?512:13000];int at=0;
            var handling=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var handleRelease=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var responding=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var responseRelease=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using var cancel=new CancellationTokenSource();
            var pump=new Tab5BleVoice(ct=> {
                ct.ThrowIfCancellationRequested();int n=Math.Min(480,request.Length-at);var p=new byte[8+n];
                BinaryPrimitives.WriteUInt32LittleEndian(p,42);BinaryPrimitives.WriteUInt16LittleEndian(p.AsSpan(4),(ushort)at);
                BinaryPrimitives.WriteUInt16LittleEndian(p.AsSpan(6),(ushort)request.Length);request.AsSpan(at,n).CopyTo(p.AsSpan(8));return Task.FromResult(p);
            },async (p,ct)=> {
                if(p[0]==1){at=BinaryPrimitives.ReadUInt16LittleEndian(p.AsSpan(5));return;}
                responding.SetResult();await responseRelease.Task.WaitAsync(ct);
                if(fault==2)throw new IOException("injected final response failure");
            },async (_,_,body,ct)=> {
                if(body.Length!=request.Length-96)throw new Exception("Bulk bytes changed");
                handling.SetResult();await handleRelease.Task.WaitAsync(ct);
                return (200,new byte[28]);
            },247,maximumRequest:13000,bulkPriority:()=>continuation);
            var running=pump.PumpAsync(cancel.Token);
            await handling.Task.WaitAsync(TimeSpan.FromSeconds(2));
            if(!pump.BulkActive)throw new Exception("Photo priority ended before attachment handling completed");
            if(fault==1) {
                cancel.Cancel();
                try{await running;throw new Exception("Bulk cancellation ignored");}catch(OperationCanceledException){}
            }else {
                handleRelease.SetResult();await responding.Task.WaitAsync(TimeSpan.FromSeconds(2));
                if(!pump.BulkActive)throw new Exception("Photo priority ended before final reply acknowledgement");
                responseRelease.SetResult();
                try{await running;if(fault==2)throw new Exception("Response failure ignored");}
                catch(IOException) when(fault==2){}
            }
            if(pump.BulkActive||pump.CompletedCount!=(fault==0?1u:0u))throw new Exception("Bulk priority leaked or failed response counted as completed");
        }
        Console.WriteLine("TAB5_BULK_PRIORITY_OK attachment handler/final response retain priority; success/cancel/failure release it");
    }
    internal static async Task RunAsync(string? encodedFixture=null)
    {
        await VerifyBulkLifetimeAsync();
        await Tab5BleMailboxWindowSelfTest.RunAsync();
        foreach(int mtu in new[]{23,247,517}) {
            var key=RandomNumberGenerator.GetBytes(32);string nonce=Guid.NewGuid().ToString("N");
            var clear=JsonSerializer.SerializeToUtf8Bytes(new {op="audio",seq=0,pcm=new string('A',2144)});
            var encrypted=Tab5Protocol.Encrypt(key,nonce,clear);
            string proof=Tab5Protocol.Proof(key,"POST|/tab5/v1/voice|001122334455|"+nonce+"|"+Convert.ToHexString(SHA256.HashData(encrypted)).ToLowerInvariant());
            var request=Encoding.ASCII.GetBytes(nonce+proof).Concat(encrypted).ToArray();
            int at=0,calls=0;uint id=7;var response=new List<byte>();
            var rpc=new Tab5BleVoice(ct=> {
                ct.ThrowIfCancellationRequested();int n=Math.Min(480,request.Length-at);var part=new byte[8+n];
                BinaryPrimitives.WriteUInt32LittleEndian(part,id);BinaryPrimitives.WriteUInt16LittleEndian(part.AsSpan(4),(ushort)at);
                BinaryPrimitives.WriteUInt16LittleEndian(part.AsSpan(6),(ushort)request.Length);request.AsSpan(at,n).CopyTo(part.AsSpan(8));return Task.FromResult(part);
            },(bytes,ct)=> {
                ct.ThrowIfCancellationRequested();if(bytes.Length>mtu-3||BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(1))!=id)throw new Exception("Voice MTU/id mismatch");
                if(bytes[0]==1)at=BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(5));
                else {
                    if(BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(5))!=response.Count)throw new Exception("Response out of order");
                    response.AddRange(bytes[9..]);
                }
                return Task.CompletedTask;
            },(n,p,body,ct)=> {
                calls++;
                if(n!=nonce||p!=proof||!Tab5Protocol.Decrypt(key,n,body).SequenceEqual(clear))throw new Exception("Authenticated voice request changed");
                return Task.FromResult<(int,byte[]?)>((200,Tab5Protocol.Encrypt(key,n,Encoding.UTF8.GetBytes("{\"state\":\"review\",\"text\":\"蓝牙语音草稿\"}"))));
            },mtu);
            await rpc.PumpAsync(CancellationToken.None);await rpc.PumpAsync(CancellationToken.None);
            if(calls!=1||!Encoding.UTF8.GetString(Tab5Protocol.Decrypt(key,nonce,response.ToArray())).Contains("蓝牙语音草稿"))throw new Exception("Voice result/replay mismatch");
        }
        var bad=new Tab5BleVoice(_=>Task.FromResult(new byte[]{1,0,0,0,4,0,124,0,1}),(_,_)=>Task.CompletedTask,(_,_,_,_)=>throw new Exception("Malformed request reached host"),247);
        try{await bad.PumpAsync(CancellationToken.None);throw new Exception("Malformed fragment accepted");}catch(IOException){}
        using var cancel=new CancellationTokenSource();cancel.Cancel();
        var cancelled=new Tab5BleVoice(ct=>Task.FromCanceled<byte[]>(ct),(_,_)=>Task.CompletedTask,(_,_,_,_)=>throw new Exception("Cancelled request reached host"),247);
        try{await cancelled.PumpAsync(cancel.Token);throw new Exception("Cancellation ignored");}catch(OperationCanceledException){}
        byte[] zero=[4,0,0,0,0,0,0,0];
        if(!Tab5VoiceAdpcm.Decode(zero).SequenceEqual(new byte[8]))throw new Exception("ADPCM silence mismatch");
        if(!Tab5VoiceAdpcm.Decode8k(zero).SequenceEqual(new byte[16]))throw new Exception("8 kHz ADPCM duration mismatch");
        foreach(var invalid in new[]{Array.Empty<byte>(),new byte[]{0,0,0,0,0,0},new byte[]{1,0,0,0,89,0},zero[..7]}) {
            try{Tab5VoiceAdpcm.Decode(invalid);throw new Exception("Malformed ADPCM accepted");}catch(ArgumentException){}
        }
        if(encodedFixture is not null) {
            var pcm=Tab5VoiceAdpcm.Decode(File.ReadAllBytes(encodedFixture));double signal=0,error=0;
            if(pcm.Length!=6400)throw new Exception("Cross-language ADPCM sample count mismatch");
            for(int i=160;i<3200;i++) {
                int original=(short)(10000*Math.Sin(2*Math.PI*440*i/16000));
                int decoded=BinaryPrimitives.ReadInt16LittleEndian(pcm.AsSpan(i*2));
                signal+=(double)original*original;error+=(double)(original-decoded)*(original-decoded);
            }
            double snr=10*Math.Log10(signal/error);if(snr<20)throw new Exception("ADPCM reconstruction quality failed");
            Console.WriteLine($"TAB5_ADPCM_CROSS_LANGUAGE_PASS SNR={snr:F1}dB input=6400 encoded=1606");
        }
        Console.WriteLine("TAB5_BLE_VOICE_PASS fragmented encrypted RPC, MTU 23/247/517, single execution, invalid ordering, cancellation, bounded ADPCM");
    }
}
