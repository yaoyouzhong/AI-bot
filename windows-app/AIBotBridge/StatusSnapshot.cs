using System.Text.Json;

namespace AIBotBridge;

internal sealed record ToolState(string State, long? AgeSeconds);

internal sealed record StatusSnapshot(
    int Version,
    string Time,
    DateTimeOffset CapturedAt,
    ToolState Codex,
    ToolState Claude);

internal static class JsonDefaults
{
    internal static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };
}
