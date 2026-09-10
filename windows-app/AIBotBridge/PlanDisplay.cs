namespace AIBotBridge;

internal static class PlanDisplay
{
    internal static string Normalize(string? value)
    {
        var label=(value??"").Trim().ToUpperInvariant().Replace('_',' ').Replace('-',' ');
        if(label.StartsWith("CLAUDE ",StringComparison.Ordinal)) label=label[7..];
        label=string.Join(' ',label.Split(' ',StringSplitOptions.RemoveEmptyEntries));
        return label switch {"PROLITE"=>"PRO LITE","MAX5X"=>"MAX 5X","MAX20X"=>"MAX 20X",_=>label};
    }
}
