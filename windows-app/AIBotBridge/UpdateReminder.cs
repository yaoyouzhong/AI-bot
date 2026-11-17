namespace AIBotBridge;
internal static class UpdateReminder
{
    internal static UpdatePreferences Read() {
        try{return UserPreferenceFile.Read<UpdatePreferences>("updates.json")??new();}
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException){return new(false);}
    }
    internal static UpdateDevice[] AvailableDevices(UpdateDevice[] devices,IReadOnlyDictionary<string,ComponentUpdate> releases) =>
        devices.Where(d=>releases.TryGetValue(d.Component,out var release)&&UpdateService.Blocked(d,release) is null).ToArray();

    internal static string Key(UpdateDevice device,ComponentUpdate release) => device.Id+":"+release.Version;

    internal static string[] Pending(UpdateDevice[] devices,IReadOnlyDictionary<string,ComponentUpdate> releases,UpdatePreferences preferences) =>
        AvailableDevices(devices,releases).Select(d=>Key(d,releases[d.Component]))
            .Where(key=>!(preferences.Notified??[]).Contains(key)).ToArray();
    internal static void Remember(UpdatePreferences preferences,string[] keys) =>
        UserPreferenceFile.Write("updates.json",preferences with {Notified=(preferences.Notified??[]).Concat(keys).Distinct().TakeLast(128).ToArray()});
}
