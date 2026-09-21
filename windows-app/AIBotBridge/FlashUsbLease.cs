using System.IO.Pipes;
using System.Security.Principal;

namespace AIBotBridge;

// A same-user, local pipe owns the pause. EOF also releases it, so a crashed
// flasher cannot leave the resident bridge suspended indefinitely.
internal sealed class FlashUsbLease : IDisposable
{
    private readonly NamedPipeClientStream _pipe;
    private FlashUsbLease(NamedPipeClientStream pipe) => _pipe = pipe;
    internal static string PipeName => "AIBotBridge.FlashUsb.v1." + WindowsIdentity.GetCurrent().User!.Value;

    internal static async Task<FlashUsbLease> AcquireAsync(CancellationToken token, string? name = null)
    {
        var pipe = new NamedPipeClientStream(".", name ?? PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
            timeout.CancelAfter(TimeSpan.FromSeconds(15));
            await pipe.ConnectAsync(timeout.Token);
            await pipe.WriteAsync(new byte[] { 1 }, timeout.Token);
            if (await ReadByteAsync(pipe, timeout.Token) != 1) throw new IOException("USB 暂停未确认。");
            return new(pipe);
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested)
        {
            pipe.Dispose();
            throw new IOException("桥接未能释放 USB。请确认没有其他刷机窗口正在操作，并将桥接更新到新版后重试。");
        }
        catch { pipe.Dispose(); throw; }
    }

    internal async Task ReleaseAsync(bool restoreCycle)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await _pipe.WriteAsync(new byte[] { restoreCycle ? (byte)3 : (byte)2 }, timeout.Token);
        if (await ReadByteAsync(_pipe, timeout.Token) != 1) throw new IOException("桥接未确认恢复 USB。");
    }

    public void Dispose() => _pipe.Dispose();

    private static async Task<int> ReadByteAsync(Stream stream, CancellationToken token)
    {
        var data = new byte[1];
        return await stream.ReadAsync(data, token) == 0 ? -1 : data[0];
    }

    internal static async Task ServeAsync(Func<CancellationToken, Task> pause, Action resume,
        Func<CancellationToken, Task> restoreCycle, CancellationToken token, string? name = null)
    {
        while (!token.IsCancellationRequested)
        {
            using var pipe = new NamedPipeServerStream(name ?? PipeName, PipeDirection.InOut, 1,
                PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            var paused = false;
            try
            {
                await pipe.WaitForConnectionAsync(token);
                using (var handshake = CancellationTokenSource.CreateLinkedTokenSource(token))
                {
                    handshake.CancelAfter(TimeSpan.FromSeconds(15));
                    if (await ReadByteAsync(pipe, handshake.Token) != 1) continue;
                    await pause(handshake.Token);
                    paused = true;
                    await pipe.WriteAsync(new byte[] { 1 }, handshake.Token);
                }
                var command = await ReadByteAsync(pipe, token);
                if (command == 3) await restoreCycle(token);
                resume(); paused = false;
                if (command is 2 or 3) await pipe.WriteAsync(new byte[] { 1 }, token);
            }
            catch (Exception ex) when (ex is IOException or OperationCanceledException) { }
            finally { if (paused) resume(); }
        }
    }
}
