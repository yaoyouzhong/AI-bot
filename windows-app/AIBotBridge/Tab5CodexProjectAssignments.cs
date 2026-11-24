using System.Text.Json;

namespace AIBotBridge;

internal static class Tab5CodexProjectAssignments
{
    internal static Dictionary<string,string> Read(string home) {
        try {
            using var file=new FileStream(Path.Combine(home,".codex-global-state.json"),FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete);
            if(file.Length>16*1024*1024)return new(StringComparer.OrdinalIgnoreCase);
            using var document=JsonDocument.Parse(file);
            return Parse(document.RootElement,home);
        }catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or JsonException) {
            return new(StringComparer.OrdinalIgnoreCase);
        }
    }

    internal static Dictionary<string,string> Parse(JsonElement state,string home) {
        var result=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        var assignments=Tab5CodexDesktop.Child(state,"thread-project-assignments");
        if(assignments.ValueKind!=JsonValueKind.Object)return result;
        var hostIds=Tab5CodexDesktop.Child(state,"thread-project-membership-host-ids");
        var migrations=Tab5CodexDesktop.Child(state,"app-server-project-id-by-legacy-project-id-by-host");
        string hostKey="local:"+Path.TrimEndingDirectorySeparator(Path.GetFullPath(home));
        var mapped=migrations.ValueKind==JsonValueKind.Object?migrations.EnumerateObject()
            .FirstOrDefault(p=>string.Equals(p.Name,hostKey,StringComparison.OrdinalIgnoreCase)).Value:default;
        var detached=Tab5CodexDesktop.Child(state,"projectless-thread-ids");
        var projectless=detached.ValueKind==JsonValueKind.Array?detached.EnumerateArray().Where(v=>v.ValueKind==JsonValueKind.String)
            .Select(v=>v.GetString()!).ToHashSet(StringComparer.OrdinalIgnoreCase):new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach(var assignment in assignments.EnumerateObject()) {
            string id=assignment.Name;
            if(!Guid.TryParse(id,out _)||projectless.Contains(id)||Tab5CodexDesktop.Text(assignment.Value,"projectKind")!="local")continue;
            var host=Tab5CodexDesktop.Child(hostIds,id);
            if(host.ValueKind!=JsonValueKind.Undefined&&(host.ValueKind!=JsonValueKind.String||host.GetString()!="local"))continue;
            string assigned=Tab5CodexDesktop.Text(assignment.Value,"projectId");
            if(!Guid.TryParse(assigned,out _))continue;
            string project=Tab5CodexDesktop.Text(mapped,assigned);
            result[id]=Guid.TryParse(project,out _)?project:assigned;
        }
        return result;
    }
}
