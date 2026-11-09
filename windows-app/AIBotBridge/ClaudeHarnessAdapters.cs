namespace AIBotBridge;

// Compiled adapter seam for Claude-compatible Harnesses. No plugin loading or
// arbitrary commands. Implementations verify native identity and report only
// supported capabilities through IClaudeNativeSession.
internal interface IClaudeHarnessAdapter
{
    ClaudeSessionSource Source {get;}
    IEnumerable<ClaudeSessionObservation> Discover();
    IEnumerable<Guid> LiveSessions();
    IClaudeNativeSession? Resolve(Guid session);
}

internal sealed class ClaudeHarnessAdapters
{
    private sealed class BuiltIn(ClaudeSessionSource source,Func<IReadOnlySet<Guid>?,IEnumerable<ClaudeSessionObservation>> discover,
        Func<IEnumerable<Guid>> live,Func<Guid,IClaudeNativeSession?> resolve):IClaudeHarnessAdapter
    {
        public ClaudeSessionSource Source=>source;
        public IEnumerable<ClaudeSessionObservation> Discover()=>discover(null);
        internal IEnumerable<ClaudeSessionObservation> DiscoverMissing(IReadOnlySet<Guid> known)=>discover(known);
        public IEnumerable<Guid> LiveSessions()=>live();
        public IClaudeNativeSession? Resolve(Guid session)=>resolve(session);
    }
    private readonly IClaudeHarnessAdapter[] _adapters;
    internal static ClaudeHarnessAdapters Default {get;}=new([
        new BuiltIn(ClaudeSessionSource.VSCode,ClaudeSessionApi.NewCodePanels,ClaudeNativeCode.LiveSessions,
            id=>(IClaudeNativeSession?)ClaudeSessionApi.Find(id)??ClaudeNativeCode.Find(id)),
        new BuiltIn(ClaudeSessionSource.Background,_=>[],ClaudeBackgroundApi.LiveSessions,ClaudeBackgroundApi.Find),
        new BuiltIn(ClaudeSessionSource.Terminal,ClaudeNativeTerminal.NewPanels,ClaudeNativeTerminal.LiveSessions,ClaudeNativeTerminal.Find)]);
    internal ClaudeHarnessAdapters(IEnumerable<IClaudeHarnessAdapter> adapters){
        _adapters=adapters.ToArray();
        if(_adapters.Length>32||_adapters.Any(a=>!a.Source.Valid||a.Source.Id is "unknown" or "multiple")||
            _adapters.Select(a=>a.Source.Id).Distinct(StringComparer.Ordinal).Count()!=_adapters.Length)throw new ArgumentException("Invalid or duplicate Harness source");
    }
    internal ClaudeHarnessAdapters With(params IClaudeHarnessAdapter[] additional)=>new(_adapters.Concat(additional));
    internal IEnumerable<ClaudeSessionObservation> Discover()=>_adapters.SelectMany(a=>a.Discover()).DistinctBy(s=>s.Id);
    internal IEnumerable<ClaudeSessionObservation> DiscoverMissing(IReadOnlySet<Guid> known)=>_adapters
        .SelectMany(a=>a is BuiltIn builtIn?builtIn.DiscoverMissing(known):a.Discover())
        .Where(s=>!known.Contains(s.Id)).DistinctBy(s=>s.Id);
    internal HashSet<Guid> LiveSessions()=>_adapters.SelectMany(a=>a.LiveSessions()).ToHashSet();
    internal IClaudeNativeSession? Resolve(Guid session){
        IClaudeNativeSession? found=null;
        foreach(var adapter in _adapters){
            var client=adapter.Resolve(session);if(client is null)continue;
            if(client.Source!=adapter.Source)throw new IOException("native_source_identity_changed");
            if(found is not null)return new MultipleOwners();found=client;
        }
        return found;
    }
    private sealed class MultipleOwners:IClaudeNativeSession
    {
        public ClaudeSessionSource Source=>ClaudeSessionSource.Multiple;
        public string State=>"unknown";
        public string Scope()=>throw new IOException("multiple_native_owners");
        public Task<ClaudeNativeSend.Result> SendAsync(string text,Func<ClaudeHistory.Value> history,Action dispatch,Func<bool> authorized,CancellationToken token)=>
            Task.FromResult(new ClaudeNativeSend.Result(false,false,null,"multiple_native_owners"));
    }
}
