using System.Text.Json;

namespace AIBotBridge;

internal static class Tab5DoubaoMicrophoneSelfTest
{
    internal static async Task RunAsync() {
        string directory=Path.Combine(Path.GetTempPath(),"tab5-mic-"+Guid.NewGuid().ToString("N"));
        string journal=Path.Combine(directory,"lease.json"),selected="dji";bool uncertain=false;int writes=0;
        Task<JsonElement> Call(string method,object? payload) {
            if(method=="settings.update") {
                selected=JsonSerializer.SerializeToElement(payload).GetProperty("patch").GetProperty("voice").GetProperty("selectedMicrophoneId").GetString()!;writes++;
                if(uncertain){uncertain=false;throw new InvalidOperationException("reply lost after applied update");}
                return Task.FromResult(JsonSerializer.SerializeToElement(new{}));
            }
            return Task.FromResult(JsonSerializer.SerializeToElement(new {selectedMicrophoneId=selected,devices=new[]{new{id="cable",name="CABLE Output (VB-Audio Virtual Cable)"}}}));
        }
        static void Check(bool value){if(!value)throw new InvalidOperationException("Doubao microphone lease regression");}
        try {
            var mic=new Tab5DoubaoMicrophone(journal,Call);
            await mic.AcquireAsync();Check(selected=="cable"&&File.Exists(journal));
            await mic.RestoreAsync();Check(selected=="dji"&&!File.Exists(journal));
            selected="";await mic.AcquireAsync();await mic.RestoreAsync();Check(selected=="");
            selected="dji";await mic.AcquireAsync();selected="manual";int before=writes;
            await mic.RestoreAsync();Check(selected=="manual"&&writes==before);
            selected="dji";uncertain=true;
            try {await mic.AcquireAsync();throw new Exception("Must retain uncertain update");}catch(InvalidOperationException){}
            await new Tab5DoubaoMicrophone(journal,Call).RestoreAsync();Check(selected=="dji");
            selected="cable";before=writes;await mic.AcquireAsync();await mic.RestoreAsync();Check(writes==before);
        } finally {if(Directory.Exists(directory))Directory.Delete(directory,true);}
        Console.WriteLine("TAB5_DOUBAO_MICROPHONE_OK restore/default/manual override/uncertain update recovery; synthetic only");
    }
}
