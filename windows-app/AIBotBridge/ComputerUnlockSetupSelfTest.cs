namespace AIBotBridge;

internal static class ComputerUnlockSetupSelfTest
{
    private sealed class Component(WifiKeyboardAccessStore store):ITightVncController
    {
        internal TightVncState State=new(TightVncKind.Managed,true,true);
        internal int Changes;internal string Action="";
        public Task<TightVncState> InspectAsync(CancellationToken token)=>Task.FromResult(State);
        public Task<TightVncResult> ChangeAsync(string action,string instance,CancellationToken token){Changes++;Action=action;store.Save(true,"Abc12345"u8);State=new(TightVncKind.Managed,true,true);return Task.FromResult(new TightVncResult(true,"ready"));}
    }
    internal static ComputerUnlockSetup Ready(string directory)
    {
        Directory.CreateDirectory(directory);var store=new WifiKeyboardAccessStore(Path.Combine(directory,"synthetic-setup-access.dat"));store.Save(true,"Abc12345"u8);
        return new(store,new Component(store),()=>true,()=>Task.FromResult(true),_=>Task.CompletedTask);
    }
    internal static async Task RunAsync(string directory)
    {
        Directory.CreateDirectory(directory);var store=new WifiKeyboardAccessStore(Path.Combine(directory,"automatic-access.dat"));store.Delete();
        var component=new Component(store){State=new(TightVncKind.Missing)};bool helper=false;int installs=0,checks=0;
        var setup=new ComputerUnlockSetup(store,component,()=>helper,()=>{installs++;helper=true;return Task.FromResult(true);},_=>{checks++;return Task.CompletedTask;});
        var result=await setup.EnsureAsync(_=>{},CancellationToken.None);
        if(!result.Ready||installs!=1||component.Changes!=1||component.Action!="install"||checks!=1||!store.Enabled)throw new InvalidOperationException("Default installation did not complete both prerequisites");
        await setup.EnsureAsync(_=>{},CancellationToken.None);
        if(installs!=1||component.Changes!=1)throw new InvalidOperationException("Ready setup reinstalled components");
        component.State=new(TightVncKind.Existing,Compatible:false,Detail:"fixture conflict");
        result=await setup.EnsureAsync(_=>{},CancellationToken.None);
        if(result.Ready||component.Changes!=1)throw new InvalidOperationException("Conflicting existing installation was overwritten or marked ready");
        store.Save(false,"Abc12345"u8);byte[] before=store.EncryptedSnapshot();component.State=new(TightVncKind.Managed,true,true);
        var failing=new ComputerUnlockSetup(store,component,()=>true,()=>Task.FromResult(true),_=>throw new IOException("fixture unavailable"));
        result=await failing.EnsureAsync(_=>{},CancellationToken.None);
        if(result.Ready||store.Enabled||!store.EncryptedSnapshot().AsSpan().SequenceEqual(before))throw new InvalidOperationException("Failed connectivity changed saved state or reported completion");
        Console.WriteLine("COMPUTER_UNLOCK_SETUP_SELF_TEST_OK (default setup, no redundant install, conflict and connectivity rollback; synthetic only)");
    }
}
