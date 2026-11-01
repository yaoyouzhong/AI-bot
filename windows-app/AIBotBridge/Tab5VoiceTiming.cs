namespace AIBotBridge;

internal static class Tab5VoiceTiming
{
    // Timing only: never write audio, recognised text or credentials.
    internal static void Log(string stage,long started) {
        Tab5LocalDiagnostics.Write($"VOICE_START stage={stage} ms={Environment.TickCount64-started}");
    }
}
