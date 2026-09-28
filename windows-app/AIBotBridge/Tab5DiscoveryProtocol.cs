using System.Text;
namespace AIBotBridge;

internal static class Tab5DiscoveryProtocol
{
    internal static byte[]? Respond(byte[] packet,string deviceId,byte[] key,string host,int port)
    {
        if(packet.Length>160||port is <1 or >65535)return null;
        var fields=Encoding.ASCII.GetString(packet).Split('|');
        if(fields.Length!=4||fields[0]!="TAB5_DISCOVER_V1"||fields[1]!=deviceId||
           !Tab5Protocol.ValidNonce(fields[2])||!Tab5Protocol.Verify(key,string.Join('|',fields.Take(3)),fields[3]))return null;
        var body=$"TAB5_BRIDGE_V1|{deviceId}|{fields[2]}|{host}|{port}";
        return Encoding.ASCII.GetBytes(body+"|"+Tab5Protocol.Proof(key,body));
    }
    internal static void SelfTest()
    {
        var key=new byte[32];var nonce=new string('1',32);var id="001122334455";
        var body=$"TAB5_DISCOVER_V1|{id}|{nonce}";
        var request=Encoding.ASCII.GetBytes(body+"|"+Tab5Protocol.Proof(key,body));
        var reply=Respond(request,id,key,"192.168.1.2",18765);
        if(reply is null)throw new InvalidOperationException("TAB5 discovery valid request rejected");
        var fields=Encoding.ASCII.GetString(reply).Split('|');
        if(fields[2]!=nonce||!Tab5Protocol.Verify(key,string.Join('|',fields.Take(5)),fields[5]))
            throw new InvalidOperationException("TAB5 discovery nonce/proof mismatch");
        if(Respond(request,"001122334456",key,"192.168.1.2",18765) is not null)
            throw new InvalidOperationException("TAB5 discovery wrong device accepted");
        request[^1]=(byte)(request[^1]=='0'?'1':'0');
        if(Respond(request,id,key,"192.168.1.2",18765) is not null)
            throw new InvalidOperationException("TAB5 discovery tampered proof accepted");
        Console.WriteLine("TAB5_DISCOVERY_OK authenticated_nonce_bound_no_keys_in_packet");
    }
}
