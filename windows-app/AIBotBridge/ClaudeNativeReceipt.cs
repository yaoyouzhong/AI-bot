using System.Security.Cryptography;
using System.Text;

namespace AIBotBridge;

// Read-only evidence for a future native-client sender. Writing to an input
// control, clearing that control, or receiving assistant text is not a receipt.
// Scope must bind the original native host/process generation at the caller.
internal sealed record ClaudeNativeReceipt(Guid Session,string Scope,string InputDigest,
    bool Limited,HashSet<string> Known,HashSet<string> Matching)
{
    internal static ClaudeNativeReceipt Capture(ClaudeHistory.Value history,Guid session,string scope,string text,IReadOnlyList<string>? images=null)
    {
        if(session==Guid.Empty||string.IsNullOrWhiteSpace(scope)||scope.Length>256||string.IsNullOrWhiteSpace(text)&&images is not {Count:>0})
            throw new ArgumentException("Native receipt identity unavailable");
        string digest=ClaudeNativeContent.Digest(text,images);
        return new(session,scope,digest,history.Limited,new(history.Accepted),
            history.InputDigests.Where(p=>p.Value==digest&&history.Accepted.Contains(p.Key)).Select(p=>p.Key).ToHashSet());
    }
    internal Guid? Confirm(ClaudeNativeReceipt before)
    {
        if(Session!=before.Session||Scope!=before.Scope||InputDigest!=before.InputDigest||Limited||before.Limited||
            !before.Known.IsSubsetOf(Known))return null;
        var added=Known.Except(before.Known).Take(2).ToArray();
        return added.Length==1&&Matching.Contains(added[0])&&Guid.TryParse(added[0],out var user)&&user!=Guid.Empty?user:null;
    }
    internal static ClaudeNativeReceipt Observe(ClaudeHistory.Value history,ClaudeNativeReceipt before,string scope)=>
        new(before.Session,scope,before.InputDigest,history.Limited,new(history.Accepted),
            history.InputDigests.Where(p=>p.Value==before.InputDigest&&history.Accepted.Contains(p.Key)).Select(p=>p.Key).ToHashSet());
}
