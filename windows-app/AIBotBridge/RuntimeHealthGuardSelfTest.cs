using System.Text.Json;

namespace AIBotBridge;

internal static class RuntimeHealthGuardSelfTest
{
    internal static async Task ParentAsync(string directory,string mode) {
        if(mode is not ("normal" or "crash" or "stalled"))throw new ArgumentException("Invalid fixture mode");
        Directory.CreateDirectory(directory);
        using var guard=RuntimeHealthGuard.Start(Path.Combine(directory,"guard-result.json"));
        File.WriteAllText(Path.Combine(directory,"parent.json"),JsonSerializer.Serialize(new{parent=Environment.ProcessId,child=guard.ChildId}));
        var now=DateTimeOffset.UtcNow;
        for(int i=0;i<(mode=="stalled"?35:5);i++) {
            guard.Pulse(new("guard-fixture",0,mode=="stalled"?now:DateTimeOffset.UtcNow,[],[]),true,true,0);
            await Task.Delay(2000);
        }
        if(mode=="crash")Environment.Exit(17); // Only this explicitly-created fixture parent exits.
        guard.BeginExit();
        for(int i=0;i<10&&!guard.ChildExited;i++)await Task.Delay(500);
        if(!guard.ChildExited)throw new InvalidOperationException("Fixture guard did not stop after intentional exit");
        Console.WriteLine("RUNTIME_GUARD_PARENT_OK "+mode);
    }
}
