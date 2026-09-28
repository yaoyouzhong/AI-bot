using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AIBotBridge;

// A package is prepared only after the user explicitly chooses an image in the
// local bridge window. The immutable bytes are served only to the paired TAB5.
internal sealed record Tab5OtaPackage(byte[] Image,string Sha256,string Version,string OfferId,string Notes="")
{
    internal const int MaximumSize=0x6e0000;
    internal object Offer=>new {sha256=Sha256,size=Image.Length,version=Version,offerId=OfferId,notes=Notes};
    private sealed record ReleaseNotes(string Version,string Sha256,string Notes);
    internal static Tab5OtaPackage Load(string path) {
        var info=new FileInfo(path);
        if(!info.Exists||info.Length is <1024 or >MaximumSize)throw new ArgumentException("请选择不超过 7,208,960 字节 的 TAB5 固件文件");
        var bytes=File.ReadAllBytes(path);
        if(bytes.Length!=info.Length||bytes[0]!=0xE9||BitConverter.ToUInt32(bytes,32)!=0xABCD5432)
            throw new ArgumentException("文件不是有效的 ESP 应用镜像");
        string ReadAscii(int offset,int length) {
            var slice=bytes.AsSpan(offset,length);
            int end=slice.IndexOf((byte)0);if(end<0)throw new ArgumentException("固件标识缺少结束符");
            var text=Encoding.ASCII.GetString(slice[..end]);
            if(text.Length==0||text.Any(c=>c<0x20||c>0x7e))throw new ArgumentException("固件标识无效");
            return text;
        }
        var version=ReadAscii(48,32);
        if(ReadAscii(80,32)!="aibot_tab5")throw new ArgumentException("这不是本项目的 TAB5 固件");
        string sha=Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(),notes="";
        string notesPath=path+".notes.json";
        if(File.Exists(notesPath)) {
            if(new FileInfo(notesPath).Length>8192)throw new ArgumentException("固件更新说明文件过大");
            ReleaseNotes? release;
            try {release=JsonSerializer.Deserialize<ReleaseNotes>(File.ReadAllText(notesPath),new JsonSerializerOptions{PropertyNameCaseInsensitive=true});}
            catch(JsonException){throw new ArgumentException("固件更新说明格式无效");}
            if(release is null||release.Version!=version||!string.Equals(release.Sha256,sha,StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("更新说明与固件版本或校验值不匹配");
            notes=(release.Notes??"").Replace("\r\n","\n").Trim();
            if(Encoding.UTF8.GetByteCount(notes)>1024||notes.Any(c=>char.IsControl(c)&&c!='\n'))
                throw new ArgumentException("更新说明须为 1024 字节以内的纯文本");
        }
        return new(bytes,sha,version,Guid.NewGuid().ToString("N"),notes);
    }
}
