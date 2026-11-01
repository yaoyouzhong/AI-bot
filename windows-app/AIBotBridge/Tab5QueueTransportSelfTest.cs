using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace AIBotBridge;

internal static class Tab5QueueTransportSelfTest
{
    // A private stdio fixture, never Codex or a real account. Reject every RPC
    // except initialization and queue reads, including all message mutations.
    internal static async Task ServeAsync() {
        int reads=0;
        while(await Console.In.ReadLineAsync() is {} line) {
            using var document=JsonDocument.Parse(line);var root=document.RootElement;
            string? method=root.GetProperty("method").GetString();
            if(method=="initialized")continue;
            int id=root.GetProperty("id").GetInt32();object result;
            if(method=="initialize") {await Task.Delay(150);result=new {};}
            else if(method=="thread/queue/list") {
                int count=++reads;
                result=new {data=new[]{new {id="queue-"+count,clientUserMessageId="client-"+count,input=new[]{new {type="text",text="synthetic"}}}},nextCursor=(string?)null};
            } else throw new InvalidOperationException("Mutation or unsupported RPC in read-only transport");
            await Console.Out.WriteLineAsync(JsonSerializer.Serialize(new{id,result}));await Console.Out.FlushAsync();
        }
    }
    internal static async Task RunAsync() {
        int starts=0;string? scope="scope-a";
        ProcessStartInfo Start() {
            starts++;
            var start=new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"dotnet","dotnet.exe")) {
                UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,
                StandardInputEncoding=new UTF8Encoding(false),StandardOutputEncoding=Encoding.UTF8
            };
            start.ArgumentList.Add(typeof(Program).Assembly.Location);start.ArgumentList.Add("--self-test-queue-transport-server");return start;
        }
        void Check(bool ok,string name){if(!ok)throw new InvalidOperationException("QUEUE_TRANSPORT: "+name);}
        using var reader=new Tab5CodexDeliveryReceipt.Tab5QueueReceiptReader(()=>scope,Start);
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(15));var token=timeout.Token;
        long began=Environment.TickCount64;await reader.PrepareAsync(token);long prepared=Environment.TickCount64-began;
        began=Environment.TickCount64;var first=await reader.ReadAsync("synthetic-task","synthetic",token);long reused=Environment.TickCount64-began;
        Check(starts==1&&first?.Known.Contains(Tab5CodexDeliveryReceipt.Digest("queue-1"))==true&&first.Matching.Count==1,"prepare performs no queue read and sends no text");
        var second=await reader.ReadAsync("synthetic-task","synthetic",token);
        Check(starts==1&&second is not null&&!second.Known.Overlaps(first!.Known),"each request reads fresh queue on same transport");
        scope="scope-b";var changed=await reader.ReadAsync("synthetic-task","synthetic",token);
        Check(starts==2&&changed is not null,"scope change restarts reader");
        scope=null;Check(await reader.ReadAsync("synthetic-task","synthetic",token) is null&&starts==2,"missing identity fails closed");
        scope="scope-c";
        using(var cancel=new CancellationTokenSource(50))await reader.PrepareAsync(cancel.Token);
        Check(starts==3,"cold initialization cancellation exercised");
        Check(await reader.ReadAsync("synthetic-task","synthetic",token) is not null&&starts==4,"cancelled preparation recovers with fresh child");
        int identityReads=0;
        using var shifting=new Tab5CodexDeliveryReceipt.Tab5QueueReceiptReader(()=>++identityReads==1?"before":"after",Start);
        Check(await shifting.ReadAsync("synthetic-task","synthetic",token) is null,"identity change during RPC rejects result");
        Console.WriteLine($"TAB5_QUEUE_TRANSPORT_OK guards=7 prepareMs={prepared} reusedReadMs={reused} (synthetic stdio)");
    }
}
