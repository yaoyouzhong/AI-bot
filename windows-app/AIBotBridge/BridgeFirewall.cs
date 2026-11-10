using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace AIBotBridge;

// Continue an existing per-program LAN permission. Never create a grant from
// a missing, disabled, blocked or more constrained rule we cannot reproduce.
internal sealed class BridgeFirewall : IDisposable
{
    internal const int DefaultHttpPort=8765;
    internal const string Group="AI-bot verified bridge LAN";
    internal sealed record Grant(int Protocol,int Port,int Profiles,string LocalAddresses,string RemoteAddresses,string RemotePorts,string InterfaceTypes);
    internal sealed record Rule(string App,int Direction,int Action,bool Enabled,int Protocol,int Profiles,
        string LocalPorts,string RemotePorts,string LocalAddresses,string RemoteAddresses,string InterfaceTypes,bool Constrained=false);
    private readonly dynamic? _rules;
    private readonly List<string> _added=[];
    private bool _committed;
    private BridgeFirewall(object? rules=null)=>_rules=rules;

    internal static Grant[] Plan(IEnumerable<Rule> rules,string previous,int httpPort){
        var existing=rules.Where(r=>r.Enabled&&r.Direction==1&&r.App.Equals(previous,StringComparison.OrdinalIgnoreCase)).ToArray();
        if(existing.Any(r=>r.Action==0))return [];
        var grants=new HashSet<Grant>();
        foreach(var rule in existing){
            int port=rule.Protocol switch {6=>httpPort,17=>LanDiscoveryServer.Port,_=>0};
            int profiles=rule.Profiles&7;
            if(rule.Action!=1||rule.Constrained||port==0||profiles==0||!IncludesPort(rule.LocalPorts,port)||
                rule.LocalAddresses.Length==0||rule.RemoteAddresses.Length==0||rule.RemotePorts.Length==0||rule.InterfaceTypes.Length==0)continue;
            grants.Add(new(rule.Protocol,port,profiles,rule.LocalAddresses,rule.RemoteAddresses=="*"?"LocalSubnet":rule.RemoteAddresses,rule.RemotePorts,rule.InterfaceTypes));
        }
        return grants.ToArray();
    }

    private static bool IncludesPort(string value,int port)=>value=="*"||value.Split(',').Any(item=>{
        var range=item.Split('-',StringSplitOptions.TrimEntries);
        return int.TryParse(range[0],NumberStyles.None,CultureInfo.InvariantCulture,out int start)&&
            (range.Length==1&&start==port||range.Length==2&&int.TryParse(range[1],NumberStyles.None,CultureInfo.InvariantCulture,out int end)&&start<=port&&port<=end);
    });

    private static dynamic Com(string id)=>Activator.CreateInstance(Type.GetTypeFromProgID(id,true)!)!;
    private static string StringValue(object? value)=>value as string??"";
    private static bool Restricted(object? value)=>StringValue(value) is not ("" or "*");
    private static Rule Read(dynamic r,string app)=>new(app,(int)r.Direction,(int)r.Action,(bool)r.Enabled,(int)r.Protocol,(int)r.Profiles,
        StringValue(r.LocalPorts),StringValue(r.RemotePorts),StringValue(r.LocalAddresses),StringValue(r.RemoteAddresses),StringValue(r.InterfaceTypes),
        (r.Interfaces is Array interfaces&&interfaces.Length!=0)||Restricted(r.ServiceName)||Restricted(r.LocalAppPackageId)||
        Restricted(r.LocalUserOwner)||Restricted(r.LocalUserAuthorizedList)||Restricted(r.RemoteUserAuthorizedList)||Restricted(r.RemoteMachineAuthorizedList)||(int)r.SecureFlags!=0);

    internal static BridgeFirewall Prepare(string? previous,string target,string owner,int port){
        if(previous is null||previous.Equals(target,StringComparison.OrdinalIgnoreCase))return new();
        if(BridgeInstallation.Read(previous,owner) is null||BridgeInstallation.Read(target,owner) is null)
            throw new IOException("bridge_firewall_unverified_payload");
        dynamic policy=Com("HNetCfg.FwPolicy2"),rules=policy.Rules;
        var prior=new List<Rule>();var destination=new Dictionary<string,Rule>(StringComparer.OrdinalIgnoreCase);
        foreach(dynamic entry in rules){
            string app=StringValue(entry.ApplicationName);
            if(app.Equals(previous,StringComparison.OrdinalIgnoreCase))prior.Add(Read(entry,app));
            else if(app.Equals(target,StringComparison.OrdinalIgnoreCase)&&(string)entry.Grouping==Group)destination[(string)entry.Name]=Read(entry,app);
        }
        var grants=Plan(prior,previous,port);
        if(grants.Length>0&&(int)policy.LocalPolicyModifyState!=0)throw new IOException("bridge_firewall_policy_disallows_local_rules");
        var prepared=new BridgeFirewall(rules);
        try{
            foreach(var grant in grants){
                string localPort=grant.Port.ToString(CultureInfo.InvariantCulture);
                string id=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(target.ToUpperInvariant()+"\n"+grant)));
                string name="AI-bot LAN "+id[..24];
                var desired=new Rule(target,1,1,true,grant.Protocol,grant.Profiles,localPort,grant.RemotePorts,grant.LocalAddresses,grant.RemoteAddresses,grant.InterfaceTypes);
                if(destination.TryGetValue(name,out var existing)){
                    if(existing!=desired with{App=existing.App})throw new IOException("bridge_firewall_rule_changed");
                    continue;
                }
                // INetFwRules.Add replaces an existing name; never overwrite
                // an unrecognized administrator or enterprise rule.
                foreach(dynamic entry in rules)if(string.Equals((string)entry.Name,name,StringComparison.OrdinalIgnoreCase))
                    throw new IOException("bridge_firewall_rule_name_in_use");
                dynamic rule=Com("HNetCfg.FwRule");
                rule.Name=name;rule.Grouping=Group;rule.Description="Previously permitted AI-bot LAN traffic for the verified bridge version.";
                rule.ApplicationName=target;rule.Direction=1;rule.Action=1;rule.Protocol=grant.Protocol;rule.LocalPorts=localPort;
                rule.RemotePorts=grant.RemotePorts;rule.LocalAddresses=grant.LocalAddresses;rule.RemoteAddresses=grant.RemoteAddresses;
                rule.InterfaceTypes=grant.InterfaceTypes;rule.Profiles=grant.Profiles;rule.EdgeTraversal=false;rule.Enabled=true;
                rules.Add(rule);prepared._added.Add(name);
                dynamic stored=rules.Item(name);string app=StringValue(stored.ApplicationName);
                if(!app.Equals(target,StringComparison.OrdinalIgnoreCase)||(string)stored.Grouping!=Group||(bool)stored.EdgeTraversal||
                    Read(stored,app)!=desired with{App=app})throw new IOException("bridge_firewall_readback_failed");
            }
            Tab5LocalDiagnostics.Write($"BRIDGE_FIREWALL continued={grants.Length} added={prepared._added.Count}");
            return prepared;
        }catch{prepared.Dispose();throw;}
    }

    internal void Commit()=>_committed=true;
    public void Dispose(){
        if(_committed||_rules is null)return;
        foreach(string name in _added)try{_rules.Remove(name);}catch(COMException){Tab5LocalDiagnostics.Write("BRIDGE_FIREWALL rollback=failed");}
    }
}
