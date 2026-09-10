using System.Globalization;
using System.Text;

namespace AIBotBridge;

internal sealed class StockService
{
    private const int MaxSymbols = 20;
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(6) };
    private static readonly Encoding QuoteEncoding;
    private string[] _symbols;
    private readonly bool _persistCache;
    private readonly HttpClient _http;
    private readonly object _sync = new();
    private StockSnapshot? _snapshot;

    static StockService()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        QuoteEncoding = Encoding.GetEncoding("GB18030");
    }

    internal StockService(BridgeSettings settings, bool persistCache = true, HttpClient? http = null)
    {
        _http = http ?? Http;
        _persistCache = persistCache;
        _symbols = settings.GetList("stock_symbols", "sh000001")
            .Select(Normalize)
            .Where(symbol => symbol.Length > 2)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MaxSymbols)
            .ToArray();
        var cached = persistCache ? SnapshotCache.Load<StockSnapshot>("stock-cache.json") : null;
        if (cached is not null && cached.Quotes.Count > 0 && cached.UpdatedAt != default)
            _snapshot = cached with { Stale = true,Quotes=cached.Quotes.Select(q=>q with {Code=DisplayCode(q.Symbol)}).ToArray() };
    }

    internal StockSnapshot? Snapshot
    {
        get { lock (_sync) return _snapshot; }
    }
    internal void ReloadSettings(BridgeSettings settings)
    {
        var symbols=settings.GetList("stock_symbols","sh000001").Select(Normalize).Where(s=>s.Length>2).Distinct(StringComparer.OrdinalIgnoreCase).Take(MaxSymbols).ToArray();
        lock(_sync)
        {
            if(_symbols.SequenceEqual(symbols)) return;
            _symbols=symbols;
            if(_snapshot is not null) _snapshot=_snapshot with {Quotes=_snapshot.Quotes.Where(q=>symbols.Contains(q.Symbol,StringComparer.OrdinalIgnoreCase)).OrderBy(q=>Array.FindIndex(symbols,s=>s.Equals(q.Symbol,StringComparison.OrdinalIgnoreCase))).ToArray(),Stale=true};
        }
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
        string[] symbols;lock(_sync) symbols=_symbols;
        if (symbols.Length == 0) return;
        try
        {
            var quotes = await FetchAsync(symbols, false, cancellationToken);
            if (quotes.Count == 0) quotes = await FetchAsync(symbols, true, cancellationToken);
            if (quotes.Count == 0) throw new InvalidOperationException("No valid quotes returned.");
            var next = new StockSnapshot(quotes, DateTimeOffset.UtcNow, false);
            lock (_sync)
            {
                if(!ReferenceEquals(symbols,_symbols)) return; // Settings changed while the request was in flight.
                _snapshot = next;
                if (_persistCache) SnapshotCache.Save("stock-cache.json", next);
            }
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

    private async Task<IReadOnlyList<StockQuote>> FetchAsync(string[] symbols, bool fallback, CancellationToken token)
    {
        try
        {
            string query = string.Join(',', fallback ? symbols.Select(SinaSymbol) : symbols);
            using var request = new HttpRequestMessage(HttpMethod.Get,
                (fallback ? "https://hq.sinajs.cn/list=" : "https://qt.gtimg.cn/q=") + query);
            request.Headers.UserAgent.ParseAdd("AI-bot/0.1");
            if (fallback) request.Headers.Referrer = new Uri("https://finance.sina.com.cn/");
            using var response = await _http.SendAsync(request, token);
            response.EnsureSuccessStatusCode();
            string text = QuoteEncoding.GetString(await response.Content.ReadAsByteArrayAsync(token));
            return fallback ? ParseSina(text, symbols) : ParseTencent(text, symbols);
        }
        catch (Exception ex) when (!token.IsCancellationRequested && ex is HttpRequestException or TaskCanceledException)
        {
            return [];
        }
    }

    internal static string SinaSymbol(string symbol) => symbol[..2] switch
    {
        "us" => "gb_" + symbol[2..].ToLowerInvariant(),
        "hk" => "rt_" + symbol,
        _ => symbol.ToLowerInvariant()
    };

    internal static IReadOnlyList<StockQuote> ParseSina(string text, IReadOnlyList<string> order)
    {
        var result = new Dictionary<string,StockQuote>(StringComparer.OrdinalIgnoreCase);
        foreach (string raw in text.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            string line=raw.Trim();
            int separator=line.IndexOf('=');
            if (!line.StartsWith("var hq_str_",StringComparison.Ordinal) || separator<12) continue;
            string key=line[11..separator].Trim();
            string symbol=Normalize(key.StartsWith("rt_hk")?key[3..]:key.StartsWith("gb_")?"us"+key[3..]:key);
            if (symbol.Length==0) continue;
            string[] values=line[(separator+1)..].Trim('"',';','\r',' ').Split(',');
            int priceIndex=symbol.StartsWith("hk")?6:symbol.StartsWith("us")?1:3;
            int nameIndex=symbol.StartsWith("hk")?1:0;
            if (values.Length<=priceIndex || !Number(values[priceIndex],out double price)) continue;
            double change, percent;
            if (symbol.StartsWith("hk") || symbol.StartsWith("us"))
            {
                int changeIndex=symbol.StartsWith("hk")?7:4, percentIndex=symbol.StartsWith("hk")?8:2;
                if(values.Length<=Math.Max(changeIndex,percentIndex) || !Number(values[changeIndex],out change) || !Number(values[percentIndex],out percent)) continue;
            }
            else
            {
                if(!Number(values[2],out double previous) || previous<=0) continue;
                change=price-previous;percent=100*change/previous;
            }
            result[symbol]=new(symbol,DisplayCode(symbol),values[nameIndex].Trim(),FormatPrice(price),
                percent.ToString("+0.00;-0.00;+0.00",CultureInfo.InvariantCulture)+"%",Math.Sign(change));
        }
        return order.Select(Normalize).Where(result.ContainsKey).Select(s=>result[s]).Take(MaxSymbols).ToArray();
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

            parsed[symbol] = new StockQuote(
                Symbol: symbol,
                Code: DisplayCode(symbol),
                Name: fields[1].Trim(),
                Price: FormatPrice(price),
                ChangePercent: percentage.ToString("+0.00;-0.00;+0.00", CultureInfo.InvariantCulture) + "%",
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
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out number) && double.IsFinite(number);

    private static string FormatPrice(double value)
    {
        var format = value>=10000 ? "0" : value>=1000 ? "0.0" : "0.00";
        return value.ToString(format, CultureInfo.InvariantCulture);
    }
    internal static string DisplayCode(string symbol)
    {
        var normalized=Normalize(symbol);
        return normalized.Length>2?normalized[2..]:symbol.ToUpperInvariant();
    }
}
