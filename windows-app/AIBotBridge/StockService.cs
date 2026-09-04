using System.Globalization;
using System.Text;

namespace AIBotBridge;

internal sealed class StockService
{
    private const int MaxSymbols = 20;
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(6) };
    private static readonly Encoding QuoteEncoding;
    private readonly string[] _symbols;
    private readonly bool _persistCache;
    private readonly object _sync = new();
    private StockSnapshot? _snapshot;

    static StockService()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        QuoteEncoding = Encoding.GetEncoding("GB18030");
    }

    internal StockService(BridgeSettings settings, bool persistCache = true)
    {
        _persistCache = persistCache;
        _symbols = settings.GetList("stock_symbols", "sh000001")
            .Select(Normalize)
            .Where(symbol => symbol.Length > 2)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MaxSymbols)
            .ToArray();
        var cached = persistCache ? SnapshotCache.Load<StockSnapshot>("stock-cache.json") : null;
        if (cached is not null && cached.Quotes.Count > 0 && cached.UpdatedAt != default)
            _snapshot = cached with { Stale = true };
    }

    internal StockSnapshot? Snapshot
    {
        get { lock (_sync) return _snapshot; }
    }

    internal async Task RunAsync(CancellationToken cancellationToken)
    {
        await RefreshAsync(cancellationToken);
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
        while (await timer.WaitForNextTickAsync(cancellationToken))
            await RefreshAsync(cancellationToken);
    }

    internal async Task RefreshAsync(CancellationToken cancellationToken)
    {
        if (_symbols.Length == 0) return;
        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Get, "https://qt.gtimg.cn/q=" + string.Join(',', _symbols));
            request.Headers.UserAgent.ParseAdd("AI-bot/0.1");
            using var response = await Http.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();
            var text = QuoteEncoding.GetString(await response.Content.ReadAsByteArrayAsync(cancellationToken));
            var quotes = ParseTencent(text, _symbols);
            if (quotes.Count == 0) throw new InvalidOperationException("No valid quotes returned.");
            var next = new StockSnapshot(quotes, DateTimeOffset.UtcNow, false);
            lock (_sync) _snapshot = next;
            if (_persistCache) SnapshotCache.Save("stock-cache.json", next);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            lock (_sync)
                if (_snapshot is not null) _snapshot = _snapshot with { Stale = true };
        }
    }

    internal static IReadOnlyList<StockQuote> ParseTencent(string text, IReadOnlyList<string> order)
    {
        var parsed = new Dictionary<string, StockQuote>(StringComparer.OrdinalIgnoreCase);
        foreach (var rawLine in text.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var line = rawLine.Trim();
            var equals = line.IndexOf('=');
            if (equals <= 2 || !line.StartsWith("v_", StringComparison.Ordinal)) continue;
            var symbol = Normalize(line[2..equals]);
            var fields = line[(equals + 1)..].Trim('"', ';', '\r').Split('~');
            if (fields.Length <= 32 || !Number(fields[3], out var price) ||
                !Number(fields[31], out var change) || !Number(fields[32], out var percentage))
                continue;

            var code = fields[2].Split('.', 2)[0];
            parsed[symbol] = new StockQuote(
                Symbol: symbol,
                Code: code,
                Name: fields[1].Trim(),
                Price: FormatPrice(price),
                ChangePercent: percentage.ToString("+0.00;-0.00;0.00", CultureInfo.InvariantCulture) + "%",
                Trend: change > 0 ? 1 : change < 0 ? -1 : 0);
        }

        return order.Select(Normalize)
            .Where(parsed.ContainsKey)
            .Select(symbol => parsed[symbol])
            .Take(MaxSymbols)
            .ToArray();
    }

    internal static string Normalize(string input)
    {
        var value = input.Trim();
        if (value.Length == 6 && value.All(char.IsDigit))
            value = value[0] switch
            {
                '6' => "sh" + value,
                '0' or '3' => "sz" + value,
                '4' or '8' => "bj" + value,
                _ => value
            };
        if (value.Length <= 2) return string.Empty;
        var market = value[..2].ToLowerInvariant();
        if (market is not ("sh" or "sz" or "bj" or "hk" or "us")) return string.Empty;
        return market + value[2..].ToUpperInvariant();
    }

    private static bool Number(string value, out double number) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out number);

    private static string FormatPrice(double value)
    {
        var format = Math.Abs(value) < 1 ? "0.000" : "0.00";
        return value.ToString(format, CultureInfo.InvariantCulture);
    }
}
