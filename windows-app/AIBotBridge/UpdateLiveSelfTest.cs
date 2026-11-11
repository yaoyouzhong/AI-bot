using System.Text.Json;
namespace AIBotBridge;
// Explicit read/download-only acceptance. Never starts an installer or offers OTA.
internal static class UpdateLiveSelfTest
{
    internal static async Task RunAsync(string output,string component="tab5") {
        using var service=new UpdateService();using var stop=new CancellationTokenSource(TimeSpan.FromMinutes(5));
        await service.CheckAsync(stop.Token);var release=service.Available[component];
        if(component=="esp8266" && release.Package?.Name.EndsWith("-flash.zip",StringComparison.Ordinal)!=true)
            throw new InvalidOperationException("The latest ESP8266 release did not select the compact flash ZIP.");
        var prepared=await service.DownloadAsync(release,Application.ProductVersion.Split('+')[0],null,stop.Token);prepared.VerifyUnchanged();
        Directory.CreateDirectory(output);
        await File.WriteAllTextAsync(Path.Combine(output,"live-release.json"),JsonSerializer.Serialize(new{
            releases=service.Available.Values.Select(v=>new{v.Component,v.Version,asset=v.Package?.Name,download=v.Package?.Url}),
            downloaded=Path.GetFileName(prepared.File),prepared.Sha256,embeddedVersion=prepared.Tab5?.Version,
            hardwareWritten=false,installerStarted=false},new JsonSerializerOptions{WriteIndented=true}),stop.Token);
        Console.WriteLine($"LIVE_UPDATE_DOWNLOAD_OK {component}, {Path.GetFileName(prepared.File)}, verified identity and checksum; no device write");
    }
}
