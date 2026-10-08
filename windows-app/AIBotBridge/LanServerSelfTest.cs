using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text.Json;

namespace AIBotBridge;

internal static class LanServerSelfTest
{
    internal static async Task RunAsync()
    {
        var port = ReservePort();
        const string token = "self-test-token-that-is-never-used-in-production";
        using var shutdown = new CancellationTokenSource();
        string? firmware="not-observed";var observed=0;
        var server = new LanStatusServer(new LanPairing(IPAddress.Loopback, port, token),legacyFirmware:value=>{firmware=EspFirmwareVersion.Read(value);Interlocked.Increment(ref observed);});
        var serverTask = server.RunAsync(SessionActivityReader.Capture, shutdown.Token);

        try
        {
            using var client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}") };
            await WaitUntilListeningAsync(client);
            await ExpectStatusAsync(client, null, HttpStatusCode.Unauthorized);
            await ExpectStatusAsync(client, "wrong-token", HttpStatusCode.Unauthorized);
            if(observed!=0)throw new Exception("Unauthenticated firmware observed");
            await ExpectStatusAsync(client, token, HttpStatusCode.OK, requireSnapshot: true);
            for(int i=0;i<50&&observed<1;i++)await Task.Delay(10);
            if(firmware!=EspFirmwareVersion.Legacy)throw new Exception("Legacy firmware header not handled");
            using(var request=new HttpRequestMessage(HttpMethod.Get,"/status")) {
                request.Headers.Add("X-AIBot-Token",token);request.Headers.Add("X-AIBot-Firmware","0.5.1");
                using var response=await client.SendAsync(request);response.EnsureSuccessStatusCode();
                for(int i=0;i<50&&observed<2;i++)await Task.Delay(10);
                if(firmware!="0.5.1")throw new Exception("Authenticated firmware not observed");
            }
            using(var request=new HttpRequestMessage(HttpMethod.Get,"/status")) {
                request.Headers.Add("X-AIBot-Token","wrong-token");request.Headers.Add("X-AIBot-Firmware","9.0.0");
                using var response=await client.SendAsync(request);
                if(response.StatusCode!=HttpStatusCode.Unauthorized||firmware!="0.5.1")throw new Exception("Unauthorized version replaced firmware");
            }
            Console.WriteLine("LAN_SELF_TEST_OK");
        }
        finally
        {
            shutdown.Cancel();
            await serverTask;
        }
    }

    private static int ReservePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static async Task WaitUntilListeningAsync(HttpClient client)
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            try
            {
                using var response = await client.GetAsync("/status");
                return;
            }
            catch (HttpRequestException) when (attempt < 19)
            {
                await Task.Delay(25);
            }
        }
    }

    private static async Task ExpectStatusAsync(
        HttpClient client, string? token, HttpStatusCode expected, bool requireSnapshot = false)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/status");
        if (token is not null)
            request.Headers.Add("X-AIBot-Token", token);
        using var response = await client.SendAsync(request);
        if (response.StatusCode != expected)
            throw new InvalidOperationException($"Expected {(int)expected}, got {(int)response.StatusCode}.");
        if (!requireSnapshot)
            return;

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        if (document.RootElement.GetProperty("version").GetInt32() != 1)
            throw new InvalidOperationException("Authenticated response was not a version 1 snapshot.");
    }
}
