using System.Security.Principal;

namespace AIBotBridge;

internal static class BridgeLifetime
{
    private static string SignalName => @"Local\AIBotBridge.Exit." + WindowsIdentity.GetCurrent().User!.Value;
    internal static EventWaitHandle Listen() => new(false,EventResetMode.AutoReset,SignalName);
    internal static void RequestExit()
    {
        if(!EventWaitHandle.TryOpenExisting(SignalName,out var signal))
        {Console.WriteLine("BRIDGE_NOT_RUNNING");return;}
        using(signal)signal.Set();
        Console.WriteLine("BRIDGE_EXIT_REQUESTED");
    }
}
