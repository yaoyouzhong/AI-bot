namespace AIBotBridge;

internal static class ResetCreditDisplay
{
    internal readonly record struct Row(int Count, string Date);

    internal static IReadOnlyList<Row> Rows(ProviderQuotaSnapshot? quota, int utcOffsetSeconds)
    {
        if (quota is null) return [];
        var rows = new List<Row>();
        foreach (var epoch in quota.ResetCreditExpiresAt ?? [])
        {
            if (epoch <= 0 || epoch > uint.MaxValue) continue;
            var date = DateTimeOffset.FromUnixTimeSeconds(epoch).AddSeconds(utcOffsetSeconds);
            rows.Add(new Row(1, $"{date.Month}/{date.Day}"));
        }
        var missing = Math.Max(0, (quota.ResetCreditsAvailable ?? 0) - rows.Count);
        if (missing > 0 || (rows.Count == 0 && quota.ResetCreditsAvailable >= 0))
            rows.Add(new Row(missing, "--"));
        return rows;
    }
}
