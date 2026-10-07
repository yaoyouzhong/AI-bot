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
        await Tab5BleSchedulingSelfTest.RunAsync();
        await VerifyDownloadPriorityAsync();
        VerifyOtaPreflight();
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
        for(int count=1;count<=3200;count++) {
            var block=new byte[6+count/2];BinaryPrimitives.WriteUInt16LittleEndian(block,(ushort)count);
            if(!Tab5VoiceAdpcm.Decode(block).SequenceEqual(new byte[count*2]))throw new Exception("16 kHz tail length/silence mismatch");
        }
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
    private static async Task VerifyDownloadPriorityAsync() {
        foreach(int fault in new[]{0,1,2}) {
            var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var writing=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var packet=new byte[132];packet[0]=42;packet[6]=124;
            using var cancel=new CancellationTokenSource();
            var pump=new Tab5BleVoice(_=>Task.FromResult(packet),async (_,ct)=> {
                writing.TrySetResult();await release.Task.WaitAsync(ct);
                if(fault==2)throw new IOException("download write rejected");
            },(_,_,_,_)=>Task.FromResult<(int,byte[]?)>((200,new byte[49152])),517,maximumResponse:65535);
            var running=pump.PumpAsync(cancel.Token);await writing.Task.WaitAsync(TimeSpan.FromSeconds(2));
            if(!pump.BulkActive)throw new Exception("Small OTA request did not prioritize first large response");
            if(fault==1)cancel.Cancel();else release.SetResult();
            try{await running;if(fault!=0)throw new Exception("Download failure ignored");}
            catch(OperationCanceledException) when(fault==1){}
            catch(IOException) when(fault==2){}
            if(pump.BulkActive||pump.CompletedCount!=(fault==0?1u:0u))throw new Exception("Download priority leaked or premature success");
        }
        Console.WriteLine("BLE_FIRST_DOWNLOAD_PRIORITY_OK completion_cancel_failure");
    }
    private sealed class PreferenceLease(Action dispose):IDisposable {public void Dispose()=>dispose();}
    private static void VerifyOtaPreflight() {
        int requested=0,disposed=0;var values=new List<bool>();
        using(var preference=new Tab5BleConnectionPreference(b=>{requested++;values.Add(b);return new PreferenceLease(()=>disposed++);})) {
            preference.Update(false);preference.Update(true);preference.Update(true);
            if(requested!=1||disposed!=0)throw new Exception("Repeated bulk requests churned connection parameters");
            preference.Update(false);preference.Update(false);
            if(requested!=2||disposed!=1||!values.SequenceEqual(new[]{true,false}))throw new Exception("Bulk preference not restored");
        }
        if(disposed!=2)throw new Exception("Connection preference leaked");
        static string Result(int ms)=>"complete;"+string.Join(";",Enumerable.Range(1,3).Select(i=>$"BLE,down,{i},262144,{ms},0,6,0,0,0,0,0,100000,1000000,90000"));
        string good=Result(2000),slow=Result(5000);
        if(Tab5BleOtaEstimate.Seconds(good,6859616) is not {} fast||fast>=120||Tab5BleOtaEstimate.Seconds(slow,6859616) is not {} poor||poor<=120)
            throw new Exception("BLE 120-second readiness threshold wrong");
        foreach(string bad in new[]{good.Replace("complete","cancelled"),good.Replace("BLE","USB"),good.Replace("down","up"),good.Replace("262144","262143"),good.Replace(",2,",",1,"),good.Replace(",2000,0,",",2000,1,"),Result(0),Result(26000),good+";extra"})
            if(Tab5BleOtaEstimate.Seconds(bad,6859616)!=null)throw new Exception("Incomplete/wrong-channel BLE estimate accepted");
        if(Tab5BleOtaEstimate.Seconds(good,0)!=null)throw new Exception("Missing firmware accepted");
        string budgetFailure=good.Replace("complete","failed").Replace("down,3,262144,2000,0","down,3,196608,5889,-2");
        if(Tab5BleOtaEstimate.Seconds(budgetFailure,6859616)!=null||!Tab5BleOtaEstimate.Format(budgetFailure,6859616).Contains("已完成 2/3 轮"))
            throw new Exception("Budget failure reason lost or incomplete run counted as valid");
        string mixed=good.Replace("down,3,262144,2000","down,3,262144,5000");
        if(Tab5BleOtaEstimate.Seconds(mixed,6859616)!=Tab5BleOtaEstimate.Seconds(slow,6859616))throw new Exception("Estimate did not use slowest round");
        Console.WriteLine("BLE_OTA_ESTIMATE_OK strict_three_rounds_slowest_20percent_25seconds_120second_gate scoped_preference");
    }
}
