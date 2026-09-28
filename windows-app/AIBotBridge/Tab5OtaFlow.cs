using System.Text;

namespace AIBotBridge;

internal sealed class Tab5OtaFlow
{
    // tcp-v1 receivers can exhaust the SDIO TX pool during flash writes.
    // Only authenticated requests advertising the packet-pool fix use full speed.
    internal static bool Fast(string capability)=>capability=="tcp-v2";
    internal static async Task WriteAsync(Stream stream,byte[] image,bool fast,CancellationToken token,Action<int>? progress=null) {
        var headers=Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\nContent-Type: application/octet-stream\r\nContent-Length: {image.Length}\r\nConnection: close\r\n\r\n");
        await stream.WriteAsync(headers,token);
        progress?.Invoke(0);
        int chunk=fast?16384:2048;
        for(int at=0;at<image.Length;at+=chunk) {
            // Bounded writes await TCP backpressure; never queue the whole image.
            await stream.WriteAsync(image.AsMemory(at,Math.Min(chunk,image.Length-at)),token);
            int sent=Math.Min(at+chunk,image.Length);
            if(sent==image.Length||sent%65536<chunk)progress?.Invoke(sent);
            if(!fast&&at+chunk<image.Length)await Task.Delay(64,token);
        }
    }
}
