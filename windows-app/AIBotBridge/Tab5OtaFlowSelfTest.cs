using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace AIBotBridge;

internal static class Tab5OtaFlowSelfTest
{
    internal static async Task RunAsync() {
        void Check(bool pass){if(!pass)throw new Exception("OTA flow test failed");}
        foreach(string capability in new[]{"","tcp-v1","tcp-v3","TCP-V2","tcp-v20"})
            Check(!Tab5OtaFlow.Fast(capability));
        Check(Tab5OtaFlow.Fast("tcp-v2"));
        using var stop=new CancellationTokenSource(TimeSpan.FromSeconds(25));
        var listener=new TcpListener(IPAddress.Loopback,0);listener.Start();
        try {
            foreach(int mode in new[]{0,1,2}) {
                bool fast=mode>0,compressed=mode==2;
                byte[] image=new byte[fast?6567120:4097];RandomNumberGenerator.Fill(image);
                if(compressed)image=Tab5OtaCompression.Stream(image);
                using var receiver=new TcpClient();await receiver.ConnectAsync((IPEndPoint)listener.LocalEndpoint,stop.Token);
                using var sender=await listener.AcceptTcpClientAsync(stop.Token);sender.SendBufferSize=32768;
                var writing=Tab5OtaFlow.WriteAsync(sender.GetStream(),image,fast,stop.Token,compressed:compressed);
                using var received=new MemoryStream();byte[] buffer=new byte[4096];
                while(received.Length<image.Length+512) {
                    int n=await receiver.GetStream().ReadAsync(buffer,stop.Token);Check(n>0);received.Write(buffer,0,n);
                    var bytes=received.GetBuffer();int header=Encoding.ASCII.GetString(bytes,0,(int)Math.Min(received.Length,512)).IndexOf("\r\n\r\n",StringComparison.Ordinal);
                    if(header>=0&&received.Length==header+4+image.Length) {
                        string headers=Encoding.ASCII.GetString(bytes,0,header);
                        Check(headers.Contains($"Content-Length: {image.Length}"));
                        Check(headers.Contains($"X-AIBot-OTA-Encoding: {Tab5OtaCompression.StreamEncoding}")==compressed);
                        Check(bytes.AsSpan(header+4,image.Length).SequenceEqual(image));break;
                    }
                }
                await writing;
            }
            using var cancelled=new CancellationTokenSource();cancelled.Cancel();
            try{await Tab5OtaFlow.WriteAsync(new MemoryStream(),new byte[16385],true,cancelled.Token);throw new Exception("Cancellation ignored");}
            catch(OperationCanceledException){ }
        }finally{listener.Stop();}
        Console.WriteLine("TAB5_OTA_FLOW_OK 6.56MB bounded TCP stream, exact bytes, tcp-v1 safety fallback, tcp-v2 gating and cancellation");
    }
}
