using System.Text.Json;

namespace AIBotBridge;
internal static class UserPreferenceFile
{
    internal static string PathFor(string name)=>Path.Combine(AppPaths.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"AI-bot",name);
    internal static T? Read<T>(string name) {
        string path=PathFor(name);if(!File.Exists(path))return default;
        if(new FileInfo(path).Length>262144)throw new InvalidDataException("配置文件过大："+name);
        return JsonSerializer.Deserialize<T>(File.ReadAllText(path))??throw new InvalidDataException("配置文件为空："+name);
    }
    internal static void Write<T>(string name,T value) {
        string path=PathFor(name);Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
        try{File.WriteAllText(temp,JsonSerializer.Serialize(value,new JsonSerializerOptions{WriteIndented=true}));File.Move(temp,path,true);}
        finally{if(File.Exists(temp))File.Delete(temp);}
    }
}
