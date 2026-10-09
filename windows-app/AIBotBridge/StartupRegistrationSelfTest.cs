using System.Runtime.InteropServices;
namespace AIBotBridge;
internal static class StartupRegistrationSelfTest
{
    internal static void Run()
    {
        foreach(Exception exception in new Exception[]{new FileNotFoundException(),new COMException("missing",unchecked((int)0x80070002))})
            if(StartupRegistration.FindTask(new Folder(exception)) is not null)throw new Exception("Missing startup task was not treated as absent");
        bool denied=false;try{StartupRegistration.FindTask(new Folder(new COMException("denied",unchecked((int)0x80070005))));}catch(COMException){denied=true;}
        if(!denied)throw new Exception("Startup permission error was silently ignored");
        // Read the same Windows scheduler state used by the real Device Center. Never register/delete a task or edit Run.
        bool enabled=StartupRegistration.IsEnabled;
        var store=new DeviceRegistryStore(Path.Combine(AppPaths.GetFolderPath(Environment.SpecialFolder.ApplicationData),"startup-ui-devices.json"));store.Migrate(null,false,false);
        using var center=new DeviceCenterForm(store,_=>new(false,"离线","",""),(_,_)=>{},(_,_)=>Task.CompletedTask,()=>{},_=>{},key=>key=="startup"&&StartupRegistration.IsEnabled);
        center.StartPosition=FormStartPosition.Manual;center.Location=new(-30000,-30000);center.ShowInTaskbar=false;
        center.Show();center.ShowPage("bridge-settings");Application.DoEvents();center.Reload();center.ShowPage("devices");Application.DoEvents();center.Close();
        Console.WriteLine($"STARTUP_READ_OK absent task via FileNotFoundException/COMException, permission errors preserved; actual enabled={enabled}; Device Center opens/reloads; no startup settings changed");
    }
    internal sealed class Folder(Exception exception){public object GetTask(string name)=>throw exception;}
}
