using System.Drawing.Imaging;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AIBotBridge;

// Only paired-device uploads become attachments. The device never supplies a PC path.
internal sealed class Tab5CodexImages(string? directory=null)
{
    internal const int MaxBytes=2*1024*1024, MaxPacket=3*1024*1024, MaxCount=3;
    private sealed record Stored(string Device,string Task,string Id,string Sha,string Extension,byte[] Bytes);
    private readonly string _directory=directory??Path.Combine(AppPaths.GetFolderPath(Environment.SpecialFolder.ApplicationData),"AI-bot","tab5-images");
    private readonly object _gate=new();
    private static readonly byte[] Entropy=Encoding.UTF8.GetBytes("TAB5 image attachments v1");
    private string FileName(string id) => Path.Combine(_directory,Guid.Parse(id).ToString()+".dat");
    internal object Upload(string device,string task,string id,byte[] bytes)
    {
        if(!Guid.TryParse(task,out _)||!Guid.TryParse(id,out _)||bytes.Length is <16 or >MaxBytes)throw new ArgumentException("invalid_image");
        string extension;
        using(var stream=new MemoryStream(bytes,false)) using(var image=Image.FromStream(stream,false,true)) {
            if(image.Width<1||image.Height<1||image.Width>4096||image.Height>4096||(long)image.Width*image.Height>4_194_304)
                throw new ArgumentException("image_dimensions_exceeded");
            extension=image.RawFormat.Guid==ImageFormat.Jpeg.Guid?".jpg":image.RawFormat.Guid==ImageFormat.Png.Guid?".png":"";
            if(extension.Length==0)throw new ArgumentException("unsupported_image");
            // Force a real decode; headers alone do not establish a readable attachment.
            using var decoded=new Bitmap(image);
        }
        id=Guid.Parse(id).ToString();string sha=Convert.ToHexString(SHA256.HashData(bytes));
        lock(_gate) {
            Directory.CreateDirectory(_directory);
            string path=FileName(id);
            if(File.Exists(path)) {
                var existing=Read(id);
                if(existing.Device!=device||existing.Task!=task||existing.Sha!=sha)throw new ArgumentException("image_id_conflict");
            } else {
                if(Directory.EnumerateFiles(_directory,"*.dat").Take(513).Count()>=512)throw new IOException("image_storage_full");
                byte[] clear=JsonSerializer.SerializeToUtf8Bytes(new Stored(device,task,id,sha,extension,bytes));
                try {
                    var encrypted=ProtectedData.Protect(clear,Entropy,DataProtectionScope.CurrentUser);
                    File.WriteAllBytes(path+".tmp",encrypted);File.Move(path+".tmp",path,false);
                }finally{CryptographicOperations.ZeroMemory(clear);}
            }
        }
        return new {status="uploaded",imageId=id,sha256=sha.ToLowerInvariant()};
    }
    private Stored Read(string id) {
        byte[] clear=ProtectedData.Unprotect(File.ReadAllBytes(FileName(id)),Entropy,DataProtectionScope.CurrentUser);
        try{return JsonSerializer.Deserialize<Stored>(clear)??throw new IOException("image_unavailable");}
        finally{CryptographicOperations.ZeroMemory(clear);}
    }
    internal string[] Resolve(string device,string task,string[] ids,bool materialize)
    {
        if(ids.Length>MaxCount||ids.Distinct().Count()!=ids.Length||ids.Any(id=>!Guid.TryParse(id,out _)))throw new ArgumentException("invalid_images");
        lock(_gate) return ids.Select(id=>{
            var image=Read(id);
            if(image.Device!=device||image.Task!=task)throw new ArgumentException("image_task_mismatch");
            if(image.Sha!=Convert.ToHexString(SHA256.HashData(image.Bytes)))throw new IOException("image_unavailable");
            // Keep the immutable local asset for deferred desktop reads and conversation history.
            string path=Path.Combine(_directory,image.Id+image.Extension);
            if(materialize&&!File.Exists(path)) {File.WriteAllBytes(path+".tmp",image.Bytes);File.Move(path+".tmp",path,false);}
            return path;
        }).ToArray();
    }
}
