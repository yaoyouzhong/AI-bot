using System.Security.Cryptography;

namespace AIBotBridge;

internal sealed class WifiKeyboardAccessStore(string? path=null)
{
    private static readonly object Gate=new();
    private readonly string file=path??Path.Combine(AppPaths.GetFolderPath(Environment.SpecialFolder.ApplicationData),"AI-bot","wifi-keyboard-access.dat");
    private static readonly byte[] Entropy="AI-bot local RFB access v1"u8.ToArray();
    internal bool Exists{get{lock(Gate)return File.Exists(file);}}
    internal (bool Enabled,byte[] Password) Read() {
        lock(Gate) {
            if(!File.Exists(file))return(false,[]);
            if(new FileInfo(file).Length is <16 or >4096)throw new CryptographicException();
            byte[] clear=ProtectedData.Unprotect(File.ReadAllBytes(file),Entropy,DataProtectionScope.CurrentUser);
            try{if(clear.Length==8)return(false,clear.ToArray());if(clear.Length!=10||clear[0] is not (2 or 3)||clear[1]>1)throw new CryptographicException();return(clear[1]==1,clear.AsSpan(2).ToArray());}
            finally{CryptographicOperations.ZeroMemory(clear);}
        }
    }
    internal bool Enabled{get{byte[] password=[];try{var value=Read();password=value.Password;return value.Enabled&&Usable(password);}catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or CryptographicException){return false;}finally{CryptographicOperations.ZeroMemory(password);}}}
    internal static bool Usable(ReadOnlySpan<byte> value){if(value.Length!=8)return false;foreach(byte b in value)if(b!=0)return true;return false;}
    internal static bool Valid(ReadOnlySpan<byte> value){if(value.Length!=8)return false;bool letter=false;foreach(byte b in value){if(b is >=65 and <=90 or >=97 and <=122)letter=true;else if(b is <48 or >57)return false;}return letter;}
    internal void Save(bool enabled,ReadOnlySpan<byte> password) {
        if(!Valid(password))throw new ArgumentException("请使用包含字母的独立 8 位访问密码。");
        SaveCore(enabled,password,2);
    }
    internal void SaveExisting(bool enabled,ReadOnlySpan<byte> password) {
        if(!Usable(password))throw new IOException("现有组件没有可接入的访问认证。");
        SaveCore(enabled,password,Valid(password)?(byte)2:(byte)3);
    }
    private void SaveCore(bool enabled,ReadOnlySpan<byte> password,byte format) {
        byte[] clear=new byte[10];clear[0]=format;clear[1]=enabled?(byte)1:(byte)0;password.CopyTo(clear.AsSpan(2));
        try{lock(Gate)Write(ProtectedData.Protect(clear,Entropy,DataProtectionScope.CurrentUser));}finally{CryptographicOperations.ZeroMemory(clear);}
    }
    private void Write(byte[] bytes) {
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);string temporary=file+"."+Guid.NewGuid().ToString("N")+".tmp";
        try{File.WriteAllBytes(temporary,bytes);AtomicFileReplacement.Move(temporary,file);}finally{if(File.Exists(temporary))File.Delete(temporary);}
    }
    internal byte[] EncryptedSnapshot(){lock(Gate){if(!File.Exists(file))return [];if(new FileInfo(file).Length>4096)throw new IOException("组件访问记录无效。");return File.ReadAllBytes(file);}}
    internal void RestoreEncrypted(byte[] bytes,bool existed){lock(Gate){if(existed){if(bytes.Length is <16 or >4096)throw new IOException("组件访问记录备份无效。");Write(bytes);}else File.Delete(file);}}
    internal void Delete(){lock(Gate)File.Delete(file);}
}
