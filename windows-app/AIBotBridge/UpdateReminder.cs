namespace AIBotBridge;
internal static class UpdateReminder
{
    internal static UpdatePreferences Read() {
        try{return UserPreferenceFile.Read<UpdatePreferences>("updates.json")??new();}
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException){return new(false);}
    }
    internal static string[] Pending(UpdateDevice[] devices,IReadOnlyDictionary<string,ComponentUpdate> releases,UpdatePreferences preferences) =>
        devices.Where(d=>releases.TryGetValue(d.Component,out var release)&&UpdateService.Blocked(d,release) is null)
            .Select(d=>d.Id+":"+releases[d.Component].Version).Where(key=>!(preferences.Notified??[]).Contains(key)).ToArray();
    internal static void Remember(UpdatePreferences preferences,string[] keys) =>
        UserPreferenceFile.Write("updates.json",preferences with {Notified=(preferences.Notified??[]).Concat(keys).Distinct().TakeLast(128).ToArray()});
}
