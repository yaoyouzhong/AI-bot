using System.Text;

namespace AIBotBridge;

// One reader per TCP connection. Bytes read past CRLFCRLF belong to its body
// or the next request; retain them rather than doing one socket read per byte.
internal sealed class HttpConnectionReader(Stream stream)
{
    private readonly byte[] _buffer=new byte[4096];
    private int _at,_end;
    internal async Task<int> ReadAsync(Memory<byte> output,CancellationToken token) {
        if(output.Length==0)return 0;
        if(_at==_end)return await stream.ReadAsync(output,token);
        int n=Math.Min(output.Length,_end-_at);_buffer.AsMemory(_at,n).CopyTo(output);_at+=n;return n;
    }
    internal async Task ReadExactlyAsync(Memory<byte> output,CancellationToken token) {
        while(output.Length>0){int n=await ReadAsync(output,token);if(n==0)throw new EndOfStreamException("HTTP body truncated");output=output[n..];}
    }
    internal async Task<IReadOnlyList<string>> ReadHeaderAsync(CancellationToken token) {
        var header=new byte[8192];int used=0;
        while(used<header.Length) {
            token.ThrowIfCancellationRequested();
            if(_at==_end){_at=0;_end=await stream.ReadAsync(_buffer,token);if(_end==0)return [];}
            while(_at<_end&&used<header.Length) {
                header[used++]=_buffer[_at++];
                if(used>=4&&header[used-4]==13&&header[used-3]==10&&header[used-2]==13&&header[used-1]==10)
                    return Encoding.ASCII.GetString(header,0,used).Split("\r\n");
            }
        }
        throw new IOException("HTTP header exceeds limit");
    }
}
