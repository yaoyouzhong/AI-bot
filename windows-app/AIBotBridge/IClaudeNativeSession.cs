namespace AIBotBridge;

internal sealed record ClaudeNativeRequest(string Operation,string Text,string[] Images,string[] ImageDigests,Func<bool> Current,Guid? Id=null,Guid? ParentUser=null);
internal interface IClaudeNativeSession
{
    ClaudeSessionSource Source=>ClaudeSessionSource.Unknown;
    string State { get; }
    bool HasSessionApi=>false;
    bool CanAddressInput=>HasSessionApi;
    bool CanAppend=>false;
    bool CanInterrupt=>false;
    bool CanImages=>false;
    bool UsesFileImageReferences=>false;
    bool IsVerifiedBlank()=>false;
    string Scope();
    Task<ClaudeNativeSend.Result> SendAsync(string text,Func<ClaudeHistory.Value> history,Action dispatch,Func<bool> authorized,CancellationToken token);
    Task<ClaudeNativeSend.Result> ExecuteAsync(ClaudeNativeRequest request,Func<ClaudeHistory.Value> history,Action dispatch,Func<bool> authorized,CancellationToken token)=>
        request.Operation=="send"&&request.Images.Length==0?SendAsync(request.Text,history,dispatch,authorized,token):Task.FromResult(new ClaudeNativeSend.Result(false,false,null,"native_operation_unavailable"));
}
