using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Buffers.Binary;

namespace AIBotBridge;

// The same canonical fingerprint is used before input and on the native user
// record. No prompt, image bytes or local paths are kept in receipt metadata.
internal static class ClaudeNativeContent
{
    internal static string Digest(string text,IReadOnlyList<string>? images=null) {
        string body=images is {Count:>0}?"claude-native-images-v1\n"+Hash(Encoding.UTF8.GetBytes(text))+"\n"+string.Join("\n",images):text;
        return Hash(new UTF8Encoding(false,true).GetBytes(body));
    }
    internal static string Hash(byte[] bytes)=>Convert.ToHexString(SHA256.HashData(bytes));
    internal static string FileDigest(string path) {
        var info=new FileInfo(path);
        if(!info.Exists||info.Length is <16 or >Tab5CodexImages.MaxBytes||(info.Attributes&FileAttributes.ReparsePoint)!=0)throw new IOException("native_image_unavailable");
        return ImageDigest(File.ReadAllBytes(path));
    }
    internal static string ImageDigest(byte[] bytes) {
        if(bytes.Length is <16 or >Tab5CodexImages.MaxBytes)throw new IOException("native_image_unavailable");
        using var stream=new MemoryStream(bytes,false);using var source=Image.FromStream(stream,false,true);
        if(source.Width<1||source.Height<1||source.Width>4096||source.Height>4096||(long)source.Width*source.Height>4_194_304)throw new IOException("native_image_unavailable");
        using var bitmap=new Bitmap(source.Width,source.Height,PixelFormat.Format32bppArgb);
        using(var graphics=Graphics.FromImage(bitmap)){graphics.CompositingMode=System.Drawing.Drawing2D.CompositingMode.SourceCopy;graphics.DrawImageUnscaled(source,0,0);}
        using var hash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);Span<byte> dimensions=stackalloc byte[8];
        BinaryPrimitives.WriteInt32LittleEndian(dimensions,bitmap.Width);BinaryPrimitives.WriteInt32LittleEndian(dimensions[4..],bitmap.Height);hash.AppendData(dimensions);
        var data=bitmap.LockBits(new Rectangle(0,0,bitmap.Width,bitmap.Height),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);
        try{byte[] row=new byte[bitmap.Width*4];for(int y=0;y<bitmap.Height;y++){Marshal.Copy(data.Scan0+y*data.Stride,row,0,row.Length);hash.AppendData(row);}}
        finally{bitmap.UnlockBits(data);}
        return Convert.ToHexString(hash.GetHashAndReset());
    }
    internal static string? MessageDigest(JsonElement content) {
        if(content.ValueKind==JsonValueKind.String)return Digest(content.GetString()??"");
        if(content.ValueKind!=JsonValueKind.Array)return null;
        var text=new StringBuilder();var images=new List<string>();
        foreach(var block in content.EnumerateArray()) {
            if(ClaudeHistory.Text(block,"type")=="text")text.Append(ClaudeHistory.Text(block,"text")??"");
            else if(ClaudeHistory.Text(block,"type")=="image"&&images.Count<Tab5CodexImages.MaxCount&&block.TryGetProperty("source",out var source)&&
                ClaudeHistory.Text(source,"type")=="base64"&&ClaudeHistory.Text(source,"media_type") is "image/jpeg" or "image/png"&&
                ClaudeHistory.Text(source,"data") is {} encoded&&encoded.Length<=4*((Tab5CodexImages.MaxBytes+2)/3)) {
                try{images.Add(ImageDigest(Convert.FromBase64String(encoded)));}catch(Exception e) when(e is FormatException or ArgumentException or IOException or ExternalException){return null;}
            }else return null;
        }
        return Digest(text.ToString(),images);
    }
}
