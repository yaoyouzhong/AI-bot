using System.Speech.AudioFormat;
using System.Speech.Synthesis;
using System.Text.Json;

namespace AIBotBridge;

internal static class WindowsSpeechSelfTest
{
    private static void Check(bool condition,string name){if(!condition)throw new InvalidOperationException("Windows speech: "+name);}
    internal static async Task RunAsync(){
        using(var pipe=new WindowsSpeechPcmStream()){
            var bytes=Enumerable.Range(0,64000).Select(i=>(byte)i).ToArray();
            Check(pipe.Push(bytes,bytes.Length)&&!pipe.Push([1,2],2),"bounded queue rejects whole excess block");
            var output=new byte[64000];int read=pipe.Read(output,0,output.Length);
            Check(read==bytes.Length&&output.SequenceEqual(bytes),"exact PCM order and content");
            var pending=Task.Run(()=>pipe.Read(output,0,2));
            await Task.Delay(30);Check(!pending.IsCompleted,"empty live stream waits instead of ending recognition");
            pipe.Complete();Check(await pending.WaitAsync(TimeSpan.FromSeconds(2))==0,"completion releases blocked reader");
        }
        Console.WriteLine("WINDOWS_SPEECH_PCM_OK bounded exact_order backpressure EOF unblock");
        using(var pipe=new WindowsSpeechPcmStream()){
            var output=new byte[4];var pending=Task.Run(()=>pipe.Read(output,0,4));
            Check(pipe.Push([1,2],2),"first fragment accepted");await Task.Delay(30);
            Check(!pending.IsCompleted,"fragment is not exposed as a short-read EOF");
            Check(pipe.Push([3,4],2)&&await pending.WaitAsync(TimeSpan.FromSeconds(2))==4&&output.SequenceEqual(new byte[]{1,2,3,4}),"read spans exact producer fragments");
            Check(pipe.Push([5,6],2),"final fragment accepted");pipe.Complete();
            Check(pipe.Read(output,0,4)==2&&output[0]==5&&output[1]==6&&pipe.Read(output,0,4)==0,"EOF drains a final short block once");
        }
        var installed=WindowsSpeechBackend.Availability();
        Check(installed is not null,"Microsoft zh-CN recognizer installed");
        using var synth=new SpeechSynthesizer();
        var chinese=synth.GetInstalledVoices().FirstOrDefault(v=>v.Enabled&&v.VoiceInfo.Culture.Name=="zh-CN");
        Check(chinese is not null,"Chinese synthesizer available for in-memory audio fixture");
        synth.SelectVoice(chinese!.VoiceInfo.Name);
        using var audio=new MemoryStream();
        synth.SetOutputToAudioStream(audio,new SpeechAudioFormatInfo(16000,AudioBitsPerSample.Sixteen,AudioChannel.Mono));
        synth.Speak("今天天气很好。现在开始测试语音识别。");synth.SetOutputToNull();
        // End-to-end through the actual installed SAPI engine and existing wire
        // state machine, using generated audio only. No microphone or desktop UI.
        using var backend=new WindowsSpeechBackend(()=>Tab5VoiceAudio.Tab5Selection);
        await backend.PrepareAsync(CancellationToken.None);
        using var session=new Tab5VoiceSession(backend,backend);
        Tab5VoiceReply Call(object request){using var doc=JsonSerializer.SerializeToDocument(request);return session.Handle(doc.RootElement);}
        var start=Call(new{op="start",taskId=Guid.NewGuid().ToString("D"),source="tab5"});
        Check(start.State=="recording"&&start.Source=="tab5","native engine starts with direct TAB5 PCM");
        var pcm=audio.ToArray();int sequence=0;long feedDeadline=Environment.TickCount64+15000;
        for(int offset=0;offset<pcm.Length;){
            Check(Environment.TickCount64<feedDeadline,"SAPI consumes live PCM within its bound");
            if(!backend.CanAcceptAudio){await Task.Delay(10);session.Tick();continue;}
            int count=Math.Min(3200,pcm.Length-offset);
            var reply=Call(new{op="audio",voiceId=start.VoiceId,seq=sequence++,codec="pcm16",pcm=Convert.ToBase64String(pcm,offset,count)});
            Check(reply.State=="recording","audio remains in its take");offset+=count;
        }
        Call(new{op="stop",voiceId=start.VoiceId});
        var deadline=Environment.TickCount64+15000;
        while(session.Active&&Environment.TickCount64<deadline){await Task.Delay(20);session.Tick();}
        var result=session.Snapshot();
        if(result.State!="review"||result.Text.Length==0||!result.CanInsert)
            Console.WriteLine($"WINDOWS_SPEECH_RESULT state={result.State}; characters={result.Text.Length}; canInsert={result.CanInsert}; message={result.Message}; {session.Diagnostic}");
        Check(result.State=="review"&&result.Text.Length>0&&result.CanInsert,"actual SAPI final text reaches review after explicit stop");
        Check(!result.Message.Contains("豆包"),"Windows result has provider-appropriate status");
        bool rejectsOld=false;try{Call(new{op="poll",voiceId="old"});}catch(Tab5VoiceRequestException){rejectsOld=true;}
        Check(rejectsOld,"old take cannot read current transcript");
        Call(new{op="cancel",voiceId=start.VoiceId});
        Check(session.Snapshot().State=="cancelled"&&session.Snapshot().Text.Length==0,"cancel clears native transcript");
        await backend.PrepareAsync(CancellationToken.None);
        var second=Call(new{op="start",taskId=Guid.NewGuid().ToString("D"),source="tab5"});
        Call(new{op="audio",voiceId=second.VoiceId,seq=0,codec="pcm16",pcm=Convert.ToBase64String(pcm,0,Math.Min(3200,pcm.Length))});
        Call(new{op="cancel",voiceId=second.VoiceId});await Task.Delay(50);
        Check(second.VoiceId!=start.VoiceId&&session.Snapshot().State=="cancelled"&&session.Snapshot().Text.Length==0,"cancel active second take and ignore late engine events");
        Console.WriteLine("WINDOWS_SPEECH_INSTALLED_ENGINE_OK zh-CN generated_audio direct_PCM final_review explicit_insert stale_id cancel; no microphone/UI/network");
    }
    internal static void RunHost(){
        using var host=new Tab5VoiceHost(new Tab5VoiceSettings(DjiId:Tab5VoiceAudio.Tab5Selection,Provider:"windows"));
        Tab5VoiceReply Call(object request){
            using var document=JsonSerializer.SerializeToDocument(request);
            var pending=host.HandleAsync(document.RootElement,CancellationToken.None);
            long until=Environment.TickCount64+10000;
            while(!pending.IsCompleted&&Environment.TickCount64<until){Application.DoEvents();Thread.Sleep(1);}
            Check(pending.IsCompleted,"host dispatcher completes within bound");return pending.GetAwaiter().GetResult();
        }
        var start=Call(new{op="start",taskId=Guid.NewGuid().ToString("D"),source="tab5"});
        Check(start.State=="recording"&&host.Busy&&host.Diagnostic.Contains("recognizer=Windows SAPI"),"production host selects Windows backend");
        Check(Application.OpenForms.Cast<Form>().All(f=>!f.Visible),"Windows host opens no desktop editor");
        var cancelled=Call(new{op="cancel",voiceId=start.VoiceId});
        Check(cancelled.State=="cancelled"&&!host.Busy&&cancelled.Text.Length==0,"host cancellation stops owned engine");
        Console.WriteLine("WINDOWS_SPEECH_HOST_OK production_dispatch selection cancellation no_visible_editor no_microphone");
    }
}
