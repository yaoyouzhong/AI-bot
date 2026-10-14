using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;

namespace AIBotBridge;

internal static class Tab5GalleryHostProfileSelfTest
{
    internal static async Task RunAsync()
    {
        var profile=new Tab5GalleryHostProfile();
        Parallel.For(0,300,i=>{profile.Add(new{sample=i});profile.Idle(2);});
        using(var json=JsonDocument.Parse(profile.Json)) {
            var root=json.RootElement;
            if(root.GetProperty("dropped").GetInt32()!=44||root.GetProperty("samples").GetArrayLength()!=256||
                root.GetProperty("idleReads").GetInt32()!=300||root.GetProperty("idleUs").GetInt64()!=600)
                throw new Exception("Gallery profile bounds/concurrent accounting failed");
        }
        byte[] fixture=new byte[88];
        BinaryPrimitives.WriteUInt32LittleEndian(fixture.AsSpan(20),12345);
        BinaryPrimitives.WriteUInt64LittleEndian(fixture.AsSpan(48),uint.MaxValue+1234UL);
        BinaryPrimitives.WriteUInt32LittleEndian(fixture.AsSpan(80),7);
        var fields=JsonSerializer.SerializeToElement(Tab5GalleryHostProfile.ParseTcp(fixture));
        if(fields.GetProperty("rttUs").GetUInt32()!=12345||fields.GetProperty("bytesOut").GetUInt64()!=uint.MaxValue+1234UL||
            fields.GetProperty("timeouts").GetUInt32()!=7||JsonSerializer.SerializeToElement(Tab5GalleryHostProfile.ParseTcp(fixture.AsSpan(0,87))).GetProperty("available").GetBoolean())
            throw new Exception("TCP_INFO_v0 layout/truncation failed");
        if(OperatingSystem.IsWindows()) {
            using var listener=new TcpListener(IPAddress.Loopback,0);listener.Start();
            using var sender=new TcpClient();using var stop=new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var accepting=listener.AcceptTcpClientAsync(stop.Token);
            await sender.ConnectAsync((IPEndPoint)listener.LocalEndpoint,stop.Token);
            using var receiver=await accepting;
            await sender.GetStream().WriteAsync(new byte[4096],stop.Token);
            await receiver.GetStream().ReadExactlyAsync(new byte[4096],stop.Token);
            var actual=JsonSerializer.SerializeToElement(Tab5GalleryHostProfile.Tcp(receiver.Client));
            if(!actual.GetProperty("available").GetBoolean()||actual.GetProperty("bytesIn").GetUInt64()<4096)
                throw new Exception("Real Windows TCP_INFO query failed: "+actual);
        }
        Console.WriteLine("GALLERY_HOST_PROFILE_OK bounded concurrent accounting, TCP_INFO ABI and real loopback read-only query");
    }
}
