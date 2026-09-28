namespace AIBotBridge;

internal static class Tab5VoiceTiming
{
    // Timing only: never write audio, recognised text or credentials.
    internal static void Log(string stage,long started) {
        var path=Environment.GetEnvironmentVariable("AIBOT_TAB5_DIAG_LOG");
        if(string.IsNullOrEmpty(path))return;
        try {File.AppendAllText(path,$"{DateTimeOffset.Now:O} VOICE_START stage={stage} ms={Environment.TickCount64-started}{Environment.NewLine}");}
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException) { }
    }
}
