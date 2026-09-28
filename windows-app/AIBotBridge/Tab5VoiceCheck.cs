using System.Diagnostics;
using System.Text.Json;
using NAudio.Wave;

namespace AIBotBridge;

// Explicit developer check: only plays a supplied synthetic 16-kHz mono WAV to
// VB-CABLE. No physical microphone, pairing changes, or Codex task submission.
internal static class Tab5VoiceCheck
{
    internal static void Run(string wavePath,string reportPath,string shortcut="LeftAltSpace") {
        if(shortcut is not ("LeftAltSpace" or "RightAltSpace" or "CtrlAltSpace"))throw new ArgumentException("不支持的语音快捷键");
        using var reader=new WaveFileReader(wavePath);
        if(reader.WaveFormat.Encoding!=WaveFormatEncoding.Pcm||reader.WaveFormat.SampleRate!=16000||
            reader.WaveFormat.BitsPerSample!=16||reader.WaveFormat.Channels!=1||reader.Length>32000*30)
            throw new ArgumentException("检查音频必须为不超过 30 秒的 16 kHz 单声道 PCM16 WAV");
        byte[] pcm=new byte[(int)reader.Length];reader.ReadExactly(pcm);
        using var window=new Form{Text="TAB5 豆包接入检查 · 音频节奏修正版",ClientSize=new Size(600,210),StartPosition=FormStartPosition.CenterScreen};
        var begin=new Button{Text="开始合成语音测试",Dock=DockStyle.Top,Height=45};window.Controls.Add(begin);
        var status=new Label{Text="仅将测试 WAV 送入 VB-CABLE；自动临时切换豆包音源并恢复。",Dock=DockStyle.Bottom,Height=130};window.Controls.Add(status);
        using var draft=new Tab5VoiceDraft(()=>shortcut);draft.InitializeDraft();
        using var session=new Tab5VoiceSession(new Tab5VoiceAudio(()=>""),draft);
        using var timer=new System.Windows.Forms.Timer{Interval=50};
        var watch=new Stopwatch();string id="";int offset=0,sequence=0;bool stop=false;
        var transitions=new List<object>();string last="";
        JsonElement Request(object value)=>JsonSerializer.SerializeToElement(value);
        void Report() {
            var value=session.Snapshot();status.Text=$"{value.State}: {value.Message}\n输入法：{draft.InputLayout} / 草稿焦点：{draft.SafeFocus}";
            string current=value.State+"/"+draft.SafeFocus;
            if(current!=last){transitions.Add(new{ms=watch.ElapsedMilliseconds,state=value.State,safeFocus=draft.SafeFocus,bytesFed=offset});last=current;}
            File.WriteAllText(reportPath,JsonSerializer.Serialize(new{reply=value,inputLayout=draft.InputLayout,safeFocus=draft.SafeFocus,draftOpacity=draft.Opacity,draftVisible=draft.Visible,syntheticAudio=true,transitions},JsonDefaults.Options));
        }
        begin.Click+=async (_,_)=> {
            begin.Enabled=false;
            try {
                await draft.PrepareAsync(CancellationToken.None);
                if(window.IsDisposed)return;
                offset=sequence=0;stop=false;transitions.Clear();last="";watch.Restart();
                var value=session.Handle(Request(new{op="start",taskId=Guid.NewGuid().ToString(),source="tab5"}));
                // Window/IME activation can take seconds. Pace the synthetic stream
                // from the completed start, never catch up that startup delay in a burst.
                watch.Restart();
                id=value.VoiceId;begin.Enabled=false;timer.Start();
            }catch(Exception ex) when(Tab5VoiceHost.IsAudioError(ex)||ex is ArgumentException){session.Fail(ex.Message);begin.Enabled=true;}
            Report();
        };
        timer.Tick+=(_,_)=> {
            try {
                session.Tick();
                if(session.Snapshot().State=="recording") {
                    if(watch.ElapsedMilliseconds>=1000+offset/32) {
                        if(offset<pcm.Length) {
                            int count=Math.Min(6400,pcm.Length-offset);
                            session.Handle(Request(new{op="audio",voiceId=id,seq=sequence++,pcm=Convert.ToBase64String(pcm,offset,count)}));offset+=count;
                        }else if(!stop){session.Handle(Request(new{op="stop",voiceId=id}));stop=true;}
                    }
                }else session.Handle(Request(new{op="poll",voiceId=id}));
                if(!session.Active){timer.Stop();begin.Enabled=true;if(session.Snapshot().State=="review")draft.CompleteReview();else draft.RevealFailure();}
            }catch(Exception ex) when(Tab5VoiceHost.IsAudioError(ex)||ex is ArgumentException){session.Fail(ex.Message);timer.Stop();begin.Enabled=true;}
            Report();
        };
        window.FormClosing+=(_,_)=>{timer.Stop();session.Dispose();draft.Shutdown();};
        Application.Run(window);
    }

}
