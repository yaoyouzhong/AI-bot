namespace AIBotBridge;

// Length-prefixed authenticated frames are identical on old and new devices.
// New devices advertise WriteWithoutResponse; the signed frame ACK is still
// mandatory. Older firmware retains acknowledged writes with a size budget.
internal static class Tab5BleTransfer
{
    // One complete exchange owns ATT, including its ACK. Waiting for another
    // exchange must not consume this exchange's transfer timeout.
    internal static async Task<T> SerializeAsync<T>(SemaphoreSlim gate,Func<Task<T>> exchange,CancellationToken token)
    {
        await gate.WaitAsync(token);
        try{return await exchange();}finally{gate.Release();}
    }
    // A response every sixteen chunks drains the host/controller queue. The last
    // chunk is also a barrier before the authenticated whole-frame ACK read.
    internal static bool RequiresResponse(int offset,int length,int total,int chunkSize,bool withoutResponse,int window=16)
        => !withoutResponse || (offset/chunkSize+1)%window==0 || offset+length>=total;
    internal static async Task SendAsync(byte[] packet,int chunkSize,bool withoutResponse,
        Func<ReadOnlyMemory<byte>,CancellationToken,Task> write,
        Func<CancellationToken,Task<bool>> acknowledge,CancellationToken token,int window=16)
    {
        if(packet.Length<4||packet.Length>Tab5Protocol.MaximumFrame+4||chunkSize is <20 or >488||window is <1 or >16)
            throw new ArgumentOutOfRangeException(nameof(packet));
        int chunks=(packet.Length+chunkSize-1)/chunkSize;
        using var transfer=CancellationTokenSource.CreateLinkedTokenSource(token);
        // Each acknowledged drain incurs a radio round trip as well as the
        // queued writes. Account for those barriers when using a smaller window.
        int barriers=(chunks+(withoutResponse?window:1)-1)/(withoutResponse?window:1);
        transfer.CancelAfter(TimeSpan.FromMilliseconds(Math.Clamp(chunks*120+barriers*750+3000,10000,120000)));
        for(int offset=0;offset<packet.Length;offset+=chunkSize) {
            // A stalled individual GATT operation fails quickly even when a
            // small negotiated MTU needs more time for the complete frame.
            using var operation=CancellationTokenSource.CreateLinkedTokenSource(transfer.Token);
            operation.CancelAfter(TimeSpan.FromSeconds(4));
            await write(packet.AsMemory(offset,Math.Min(chunkSize,packet.Length-offset)),operation.Token);
            // Keep controller pacing as well as explicit window drain barriers:
            // WinRT completion does not mean the radio consumed every command.
            if(withoutResponse)await Task.Delay(8,transfer.Token);
        }
        using var ack=CancellationTokenSource.CreateLinkedTokenSource(transfer.Token);
        ack.CancelAfter(TimeSpan.FromSeconds(3));
        while(!await acknowledge(ack.Token))await Task.Delay(80,ack.Token);
    }
}
