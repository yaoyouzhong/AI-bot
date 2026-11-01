using System.Text;

namespace AIBotBridge;

// Only fixed protocol states and numeric timings may enter this sink. No user
// text, paths, task/request identities or credentials. One current + one prior file.
internal static class Tab5LocalDiagnostics
{
    private static readonly object Gate=new();
    internal const int MaximumBytes=1024*1024;
    internal static void Write(string metadata,string? destination=null) {
        if(metadata.Length>2048||metadata.Contains('\r')||metadata.Contains('\n'))return;
        try {
            string path=destination??Environment.GetEnvironmentVariable("AIBOT_TAB5_DIAG_LOG")??
                Path.Combine(AppPaths.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"AI-bot","diagnostics","tab5.log");
            lock(Gate) {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
                if(File.Exists(path)&&new FileInfo(path).Length>=MaximumBytes)File.Move(path,path+".previous",true);
                File.AppendAllText(path,$"{DateTimeOffset.Now:O} {metadata}{Environment.NewLine}",Encoding.UTF8);
            }
        }catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException) { }
    }
}
