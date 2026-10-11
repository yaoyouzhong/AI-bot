using System.Net.Sockets;

namespace AIBotBridge;

internal static class Tab5TcpUpload
{
    // Windows SDK mstcpip.h: SIO_TCP_SET_ACK_FREQUENCY and its one-byte
    // TCP_ACK_FREQUENCY_PARAMETERS. This affects only this accepted socket.
    private const int SetAckFrequency=unchecked((int)0x98000017);
    internal static string Configure(Socket socket)
    {
        if(!OperatingSystem.IsWindows())return "ack=default; platform=other";
        try {socket.IOControl(SetAckFrequency,[1],null);return "ack=1";}
        catch(SocketException ex) {return $"ack=default; socketError={(int)ex.SocketErrorCode}";}
    }
}

internal sealed partial class Tab5Service
{
    private volatile string _tcpUploadDiagnostic="尚无 Wi-Fi RPC";
    internal string TcpUploadDiagnostic=>"\nWi-Fi 上传："+_tcpUploadDiagnostic;
    internal void RecordTcpUpload(string policy,int bytes,long readMs,long handlerMs,long writeMs,int status)=>
        _tcpUploadDiagnostic=$"{DateTime.Now:HH:mm:ss} {policy}; bytes={bytes}; readMs={readMs}; handlerMs={handlerMs}; writeMs={writeMs}; status={status}";
}
