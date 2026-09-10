using System.Globalization;
using System.Text.RegularExpressions;

namespace AIBotBridge;

// Explicit local import only. Brand pixels are never embedded in the public package.
internal static class LocalPageLogos
{
    private static string DirectoryPath => Path.Combine(AIBotBridge.AppPaths.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"AI-bot","page-logos");
    internal static byte[] ReadHeader(string path,string role)
    {
        if(role is not ("claude" or "codex"))throw new ArgumentException("Unknown role");
        if(new FileInfo(path).Length>100000)throw new InvalidDataException("Logo file too large");
        return ParseHeader(File.ReadAllText(path),role);
    }
    internal static byte[] ParseHeader(string source,string role)
    {
        if(role is not ("claude" or "codex")||source.Length>100000)throw new InvalidDataException("Invalid logo input");
        foreach(var dimension in new[]{"W","H"})
            if(!Regex.IsMatch(source,@"#define\s+"+role.ToUpperInvariant()+"_LOGO_"+dimension+@"\s+40\b"))throw new InvalidDataException("Expected 40x40 logo");
        var match=Regex.Match(source,@"\b"+role+@"_logo_0\[1600\]\s+PROGMEM\s*=\s*\{([^}]+)\}");
        var words=match.Groups[1].Value.Split(',',StringSplitOptions.TrimEntries|StringSplitOptions.RemoveEmptyEntries);
        if(!match.Success||words.Length!=1600)throw new InvalidDataException("Incomplete logo pixels");
        var bytes=new byte[3200];
        for(int i=0;i<words.Length;i++) {
            var word=words[i];ushort value=word.StartsWith("0x",StringComparison.OrdinalIgnoreCase)?ushort.Parse(word[2..],NumberStyles.HexNumber,CultureInfo.InvariantCulture):ushort.Parse(word,CultureInfo.InvariantCulture);
            bytes[i*2]=(byte)(value>>8);bytes[i*2+1]=(byte)value;
        }
        return bytes;
    }
    internal static void Import(string root)
    {
        var values=new[]{"claude","codex"}.Select(role=>(Role:role,Bytes:ReadHeader(Path.Combine(root,"firmware","include","img",role+"_logo.h"),role))).ToArray();
        Directory.CreateDirectory(DirectoryPath);
        foreach(var item in values) {
            var path=Path.Combine(DirectoryPath,item.Role+".rgb565");
            File.WriteAllBytes(path+".tmp",item.Bytes);File.Move(path+".tmp",path,true);
            var original=Path.Combine(root,"windows-app","AIClockBridge","Assets",item.Role+"-logo.png");
            if(File.Exists(original)) {
                using var image=new Bitmap(original);
                if(image.Width>2048||image.Height>2048)throw new InvalidDataException("Logo exceeds local import size limit.");
                var mirrorPath=Path.Combine(DirectoryPath,item.Role+"-mirror.png");
                if(File.Exists(mirrorPath))File.Copy(mirrorPath,mirrorPath+".previous",true);
                image.Save(mirrorPath+".tmp",System.Drawing.Imaging.ImageFormat.Png);
                File.Move(mirrorPath+".tmp",mirrorPath,true);
            }
            Console.WriteLine($"LOCAL_LOGO_IMPORTED {item.Role} 40x40");
        }
    }
    internal static IReadOnlyList<ResourcePayload> Resources()
    {
        var result=new List<ResourcePayload>();
        foreach(var role in new[]{"claude","codex"})try {
            var path=Path.Combine(DirectoryPath,role+".rgb565");
            if(!File.Exists(path)||new FileInfo(path).Length!=3200)continue;
            var bytes=File.ReadAllBytes(path);result.Add(new(role=="claude"?BinaryResourceKind.ClaudeLogo:BinaryResourceKind.CodexLogo,unchecked((int)BinaryResourceProtocol.Crc32(bytes)),bytes));
        }catch(Exception ex)when(ex is IOException or UnauthorizedAccessException) { }
        return result;
    }
    internal static bool Draw(Graphics graphics,bool claude)
    {
        var mirrorPath=Path.Combine(DirectoryPath,(claude?"claude":"codex")+"-mirror.png");
        if(File.Exists(mirrorPath))try {
            using var original=new Bitmap(mirrorPath);graphics.DrawImage(original,new Rectangle(14,18,40,40));return true;
        }catch(Exception ex)when(ex is IOException or ArgumentException or System.Runtime.InteropServices.ExternalException) { }
        var resource=Resources().FirstOrDefault(r=>r.Kind==(claude?BinaryResourceKind.ClaudeLogo:BinaryResourceKind.CodexLogo));
        if(resource is null)return false;
        using var bitmap=new PetAnimation([120],[resource.Data],40,40).BitmapAt(0);graphics.DrawImageUnscaled(bitmap,14,18);return true;
    }
}
