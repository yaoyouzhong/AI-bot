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
        var server = new LanStatusServer(new LanPairing(IPAddress.Loopback, port, token));
        var serverTask = server.RunAsync(SessionActivityReader.Capture, shutdown.Token);

        try
        {
            using var client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}") };
            await WaitUntilListeningAsync(client);
            await ExpectStatusAsync(client, null, HttpStatusCode.Unauthorized);
            await ExpectStatusAsync(client, "wrong-token", HttpStatusCode.Unauthorized);
            await ExpectStatusAsync(client, token, HttpStatusCode.OK, requireSnapshot: true);
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
