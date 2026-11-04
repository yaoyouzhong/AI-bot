using System.Text.Json;

namespace AIBotBridge;

// Annual mode selects an ordered index into the shared full collection.
internal static class GalleryPreferences
{
    private sealed record State(bool Full=false,bool High=true);
    private static State? _state;
    private static string DirectoryPath=>Path.Combine(AppPaths.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"AI-bot");
    private static string FilePath=>Path.Combine(DirectoryPath,"gallery-preferences.json");
    private static State Current {
        get {
            if(_state is not null)return _state;
            try{_state=JsonSerializer.Deserialize<State>(File.ReadAllBytes(FilePath),JsonDefaults.Options)??new();}
            catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or JsonException){_state=new();}
            return _state;
        }
    }
    internal static bool Full {get{lock(GalleryPack.Gate)return Current.Full;}}
    internal static bool High {get{lock(GalleryPack.Gate)return Current.High;}}
    internal static void Set(bool full,bool high) {
        lock(GalleryPack.Gate) {
            if(Current.Full==full&&Current.High==high)return;
            Directory.CreateDirectory(DirectoryPath);
            string temp=Path.Combine(DirectoryPath,".gallery-preferences-"+Guid.NewGuid().ToString("N")+".tmp");
            try {
                File.WriteAllBytes(temp,JsonSerializer.SerializeToUtf8Bytes(new State(full,high),JsonDefaults.Options));
                File.Move(temp,FilePath,true);
                _state=new State(full,high);
                GalleryPack.NotifyInstalled();
            } finally {if(File.Exists(temp))File.Delete(temp);}
        }
    }
}
