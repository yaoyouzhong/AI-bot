using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using NAudio.Wave;

namespace AIBotBridge;

internal static class Tab5VoiceSelfTest
{
    private static void Check(bool pass,string label){if(!pass)throw new InvalidOperationException("TAB5 voice: "+label);}
    private sealed class Audio : ITab5VoiceAudio {
        internal bool Dji=true,Running,Empty=true,FailStop;internal string Source="";internal int Bytes,Starts;
        internal BufferedWaveProvider? Buffer;
        internal bool Room=true;
        public bool CanAcceptAudio=>Room&&(Buffer is null||Tab5VoiceAudio.HasPlaybackRoom(Buffer));
        public string Start(bool force){Running=true;Starts++;return Source=Dji&&!force?"dji":"tab5";}
        public bool DjiHealthy=>Dji;
        public string SourceName=>Source=="dji"?"麦克风 (Wireless Mic Rx)":"TAB5 内置麦克风";
        public bool InputReady {get;set;}=true;
        public int InputLevel {get;set;}=50;
        public bool? CapturedSound {get;set;}
        public bool Drained=>Empty;
        public void UseTab5()=>Source="tab5";
        public void Feed(byte[] pcm){Buffer?.AddSamples(pcm,0,pcm.Length);Bytes+=pcm.Length;}
        public void Drain(){}
        public void Stop(){Running=false;if(FailStop)throw new InvalidOperationException("synthetic unplug on stop");}
        public void Dispose()=>Stop();
    }
    private sealed class Editor : ITab5VoiceEditor {
        public string Text {get;set;}="";public bool SafeFocus {get;set;}=true;
        internal int Stops;
        internal bool RestoreAllowed;
        internal int Restores;
        public bool TryRestoreFocus(){Restores++;if(RestoreAllowed)SafeFocus=true;return SafeFocus;}
        public bool CaptureStopped {get;set;}=true;
        public bool RecognitionComplete {get;set;}
        public bool Start()=>SafeFocus;
        public bool Stop(){Stops++;return SafeFocus;}
        public void Clear()=>Text="";
    }
    private sealed class Endpoint(Tab5VoiceSession session) : ITab5VoiceEndpoint {
        public Task<Tab5VoiceReply> HandleAsync(JsonElement request,CancellationToken token)=>session.HandleAsync(request,token);
        public void ShowSettings(IWin32Window owner){}
        public void Dispose()=>session.Dispose();
    }
    private static JsonElement Json(object value)=>JsonSerializer.SerializeToElement(value);
    internal static void PreviewSettings(string path) {
        using var host=new Tab5VoiceHost(new Tab5VoiceSettings());
        using var form=host.CreateSettings(preview:true);
        form.StartPosition=FormStartPosition.Manual;form.Location=new Point(-32000,-32000);form.ShowInTaskbar=false;
        form.Show();Application.DoEvents();form.PerformLayout();
        using var bitmap=new Bitmap(form.Width,form.Height);
        form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size));
        bitmap.Save(path,System.Drawing.Imaging.ImageFormat.Png);
        Console.WriteLine("TAB5_VOICE_SETTINGS_PREVIEW_OK (read-only device check, capture disabled)");
    }
    internal static void RunUi() {
        using(var draft=new Tab5VoiceDraft(()=>"",_=>false)) {
            draft.InitializeDraft();draft.Prepare();Application.DoEvents();
            Check(draft.Visible&&draft.Opacity==0&&!draft.ShowInTaskbar,"normal voice editor is transparent and absent from taskbar");
            draft.RevealFailure("test failure");Application.DoEvents();
            Check(draft.Opacity==1&&draft.ShowInTaskbar,"failure reveals a recoverable editor");
            draft.Prepare();Application.DoEvents();
            Check(draft.Opacity==0&&!draft.ShowInTaskbar,"retry returns to transparent editor");
            draft.Hide();
        }
        static Tab5VoiceReply Wait(Task<Tab5VoiceReply> reply) {
            long until=Environment.TickCount64+2000;
            while(!reply.IsCompleted&&Environment.TickCount64<until){Application.DoEvents();Thread.Sleep(5);}
            Check(reply.IsCompletedSuccessfully,"STA dispatch completes");return reply.Result;
        }
        using var host=new Tab5VoiceHost(new Tab5VoiceSettings());
        var reply=Wait(host.HandleAsync(Json(new{op="start",taskId=Guid.NewGuid().ToString()}),CancellationToken.None));
        Check(reply.State=="error"&&reply.Message.Contains("配置语音"),"real STA dispatch rejects unconfigured capture without opening microphone");
        var audio=new Audio();var editor=new Editor();
        using var enabled=new Tab5VoiceHost(new Tab5VoiceSettings(Enabled:true),audio,editor);
        var task=Guid.NewGuid().ToString();
        var started=Wait(enabled.HandleAsync(Json(new{op="start",taskId=task}),CancellationToken.None));
        editor.Text="当前会话草稿";
        reply=Wait(enabled.HandleAsync(Json(new{op="cancel",voiceId="old-session"}),CancellationToken.None));
        Check(reply.State=="error"&&reply.Text==""&&audio.Running&&editor.Stops==0,"stale cancel cannot terminate or read active draft");
        reply=Wait(enabled.HandleAsync(Json(new{op="start",taskId=task}),CancellationToken.None));
        Check(reply.State=="error"&&audio.Starts==1&&audio.Running,"second start does not stop first capture");
        reply=Wait(enabled.HandleAsync(Json(new{op="poll",voiceId=started.VoiceId}),CancellationToken.None));
        Check(reply.State=="recording"&&reply.Text==editor.Text,"active session survives rejected requests");
        Wait(enabled.HandleAsync(Json(new{op="cancel",voiceId=started.VoiceId}),CancellationToken.None));
    }
    private static void CheckFocusRecovery() {
        long now=1000;var audio=new Audio{Dji=false};var editor=new Editor{RestoreAllowed=true};
        using var voice=new Tab5VoiceSession(audio,editor,()=>now);
        var started=voice.Handle(Json(new{op="start",taskId=Guid.NewGuid().ToString(),source="tab5"}));
        voice.Tick();editor.Text="已有文字";editor.SafeFocus=false;
        var continued=voice.Handle(Json(new{op="audio",voiceId=started.VoiceId,seq=0,pcm=Convert.ToBase64String(new byte[6400])}));
        Check(continued.State=="recording"&&continued.VoiceId==started.VoiceId&&continued.Text=="已有文字"&&audio.Bytes==6400&&audio.Starts==1&&editor.Stops==0&&editor.Restores==1,"focus restoration keeps the same take and audio without restarting");
        voice.Handle(Json(new{op="stop",voiceId=started.VoiceId}));
        editor.SafeFocus=false;voice.Tick();
        Check(voice.Snapshot().State=="draining"&&audio.Running&&editor.Stops==0&&editor.Restores==2,"draining focus restoration preserves queued tail");
        voice.Handle(Json(new{op="cancel",voiceId=started.VoiceId}));
        int restores=editor.Restores;editor.SafeFocus=false;voice.Tick();
        Check(editor.Restores==restores&&!audio.Running,"cancelled take never reclaims focus");
        editor.SafeFocus=true;voice.Handle(Json(new{op="start",taskId=Guid.NewGuid().ToString()}));
        editor.RestoreAllowed=false;editor.SafeFocus=false;voice.Tick();
        Check(voice.Snapshot().State=="error"&&!audio.Running&&audio.Starts==2,"failed recovery is visible and never starts a replacement take");
        Console.WriteLine("TAB5_VOICE_FOCUS_RECOVERY_OK same_take_audio_text_tail preserved; no restart; synthetic only");
    }
    internal static async Task RunAsync() {
        CheckFocusRecovery();
        await CheckAudioBackpressureAsync();
        Tab5DoubaoVoiceSelfTest.Run();
        CheckQuietCompletion();
        CheckStoppedAudio();
        CheckReconnectRecovery();
        Check(Tab5AudioMeter.Level(new byte[8],16)==0,"zero PCM has no level");
        Check(Tab5AudioMeter.Level(new byte[]{0,128},16)==100,"negative PCM16 full scale");
        Check(Tab5AudioMeter.Level(new byte[]{0,0,128},24)==100,"PCM24 sign extension");
        Check(Tab5AudioMeter.Level(BitConverter.GetBytes(float.NaN),32,true)==0,"non-finite float ignored");
        Check(Tab5AudioMeter.Level(BitConverter.GetBytes(0.1f),32,true) is >=66 and <=67,"float input logarithmic level");
        long now=1000;var audio=new Audio();var editor=new Editor();
        using var voice=new Tab5VoiceSession(audio,editor,()=>now);
        var task=Guid.NewGuid().ToString();
        var start=voice.Handle(Json(new{op="start",taskId=task}));
        Check(start.Source=="dji"&&audio.Running,"DJI preferred on start");
        Check(start.SourceName=="麦克风 (Wireless Mic Rx)","full selected endpoint name is retained without guessed model");
        Check(!start.Ready,"shortcut success alone never announces ready");
        audio.InputReady=false;voice.Tick();Check(!voice.Snapshot().Ready,"audio opening delay stays preparing");
        audio.InputReady=true;
        voice.Tick();Check(voice.Snapshot().Source=="dji","silence is not inferred as disconnect");
        Check(voice.Snapshot().Ready,"live DJI input and virtual output announce ready");
        audio.InputLevel=0;
        for(int i=0;i<7;i++){now+=1000;voice.Handle(Json(new{op="poll",voiceId=start.VoiceId}));voice.Tick();}
        Check(voice.Snapshot().Silent&&voice.Snapshot().State=="recording","six seconds of silence warns without ending recording");
        audio.InputLevel=50;voice.Tick();Check(!voice.Snapshot().Silent&&voice.Snapshot().Level==50,"speech clears silence notice immediately");
        audio.Dji=false;voice.Tick();Check(voice.Snapshot().Source=="tab5","unplug during speech falls back");
        Check(voice.Snapshot().SourceName=="TAB5 内置麦克风","fallback replaces the external microphone name");
        Check(!voice.Snapshot().Ready,"fallback waits for TAB5 audio before announcing ready");
        audio.Dji=true;voice.Tick();Check(voice.Snapshot().Source=="tab5","reconnect does not interrupt utterance");
        var chunk=new byte[]{1,0,2,0};
        voice.Handle(Json(new{op="audio",voiceId=start.VoiceId,seq=0,pcm=Convert.ToBase64String(chunk)}));
        Check(audio.Bytes==4,"TAB5 PCM reaches output");
        voice.Tick();Check(voice.Snapshot().Ready,"TAB5 ready after audio receipt and virtual output");
        bool rejected=false;
        try{voice.Handle(Json(new{op="audio",voiceId=start.VoiceId,seq=0,pcm=Convert.ToBase64String(chunk)}));}catch(ArgumentException){rejected=true;}
        Check(rejected&&audio.Bytes==4,"duplicate audio cannot play twice");
        audio.Empty=false;voice.Handle(Json(new{op="stop",voiceId=start.VoiceId}));
        Check(voice.Snapshot().State=="draining"&&audio.Running,"tail drains before recognition stops");
        Check(!voice.Snapshot().Ready,"stopping clears readiness immediately");
        now+=1000;voice.Tick();Check(audio.Running&&editor.Stops==0,"buffered tail is not cut off at 200ms after stop");
        audio.Empty=true;voice.Tick();now+=199;voice.Tick();Check(audio.Running,"WASAPI latency allowed after queue becomes empty");
        now+=1;voice.Tick();Check(!audio.Running&&editor.Stops==1,"microphone closes after tail playback");
        editor.Text="继续检查这个任务";voice.Tick();now+=1199;voice.Tick();
        Check(voice.Snapshot().State=="recognizing","minimum final commit guard before hiding draft");
        now+=1;voice.Tick();
        Check(voice.Snapshot().State=="review"&&voice.Snapshot().TaskId==task&&voice.Snapshot().Text==editor.Text,"draft remains attached to selected task");
        Check(!voice.Snapshot().CanInsert,"unconfirmed recognizer cannot automatically insert");
        editor.RecognitionComplete=true;
        Check(voice.Snapshot().CanInsert,"explicit stop and complete transcript permit draft insertion");
        editor.Text=new string('测',701);Check(!voice.Snapshot().CanInsert,"truncated transcript cannot automatically insert");
        editor.RecognitionComplete=false;
        audio.Dji=false;start=voice.Handle(Json(new{op="start",taskId=task}));
        Check(start.Source=="tab5"&&start.Text=="","absent DJI falls back, previous text cleared");
        now+=4001;voice.Tick();Check(voice.Snapshot().State=="draining","lost heartbeat drains already received audio");
        voice.Tick();now+=200;voice.Tick();Check(!audio.Running,"lost heartbeat ends capture after buffered tail");
        now+=8000;voice.Tick();audio.Dji=true;start=voice.Handle(Json(new{op="start",taskId=task}));
        voice.Handle(Json(new{op="tab5",voiceId=start.VoiceId}));Check(voice.Snapshot().Source=="tab5","manual fallback while receiver remains connected");
        editor.SafeFocus=false;voice.Tick();Check(!audio.Running&&voice.Snapshot().State=="error","focus loss stops microphone");
        editor.SafeFocus=true;start=voice.Handle(Json(new{op="start",taskId=task}));
        editor.Text="应丢弃";voice.Handle(Json(new{op="cancel",voiceId=start.VoiceId}));
        Check(voice.Snapshot().Text==""&&!voice.Active&&!audio.Running,"cancel clears draft and releases audio");
        start=voice.Handle(Json(new{op="start",taskId=task}));
        for(int i=0;i<59;i++){now+=1000;voice.Handle(Json(new{op="poll",voiceId=start.VoiceId}));}
        now+=1000;voice.Tick();Check(voice.Snapshot().State=="draining","60 second cap drains tail despite healthy heartbeats");
        voice.Tick();now+=200;voice.Tick();Check(!audio.Running,"60 second cap closes audio after drain");
        now+=8000;voice.Tick();Check(voice.Snapshot().State=="error"&&voice.Snapshot().Message.Contains("未收到豆包文字"),"missing IME result is not a successful review");voice.Handle(Json(new{op="cancel",voiceId=start.VoiceId}));
        start=voice.Handle(Json(new{op="start",taskId=task}));
        voice.Handle(Json(new{op="stop",voiceId=start.VoiceId}));voice.Tick();now+=200;voice.Tick();
        now+=5000;voice.Tick();Check(voice.Snapshot().State=="recognizing","empty late result retains focus");
        editor.Text="延迟返回";voice.Tick();now+=600;editor.Text+="的最后一句";voice.Tick();
        now+=799;voice.Tick();Check(voice.Snapshot().State=="recognizing","last text revision restarts settle timer");
        now+=1;voice.Tick();Check(voice.Snapshot().State=="review"&&voice.Snapshot().Text=="延迟返回的最后一句","late final text retained without fixed extra wait");
        start=voice.Handle(Json(new{op="start",taskId=task}));audio.Empty=false;
        voice.Handle(Json(new{op="stop",voiceId=start.VoiceId}));now+=3001;voice.Tick();
        Check(!audio.Running&&voice.Snapshot().State=="error","stalled output stops instead of claiming complete recognition");audio.Empty=true;
        start=voice.Handle(Json(new{op="start",taskId=task}));audio.FailStop=true;int stops=editor.Stops;
        voice.Fail("测试设备断开");Check(!audio.Running&&editor.Stops==stops+1&&voice.Snapshot().State=="error","IME stop attempted even when audio cleanup fails");audio.FailStop=false;
        Check(Encoding.UTF8.GetByteCount(Tab5VoiceSession.BoundText(string.Concat(Enumerable.Repeat("语音😀",500))))<=2000,"UTF-8 bound preserves rune boundaries");
        Check(Tab5VoiceAudio.IsDji("麦克风 (Wireless Mic Rx)")&&!Tab5VoiceAudio.IsDji("Intel Microphone Array"),"DJI known endpoint identity, unrelated mic excluded");
        Tab5AudioDevice[] capture=[new("dji","麦克风 (Wireless Mic Rx)"),new("cable-out","CABLE Output (VB-Audio Virtual Cable)")];
        Tab5AudioDevice[] render=[new("cable-in","CABLE Input (VB-Audio Virtual Cable)")];
        Check(Tab5VoiceAudio.Inspect(capture,render,"") is {CableReady:true,DjiAvailable:true},"both virtual endpoints and DJI detected");
        Check(!Tab5VoiceAudio.Inspect(capture[..1],render,"").CableReady,"missing cable capture endpoint blocks setup");
        Check(!Tab5VoiceAudio.Inspect(capture,render,"disconnected-id").DjiAvailable,"disconnected explicit device never selects unrelated input");
        Check(!Tab5VoiceAudio.Inspect(capture,render,"cable-out").DjiAvailable&&Tab5VoiceAudio.SelectInput(capture,"cable-out") is null,"saved virtual cable selection cannot feed output back into itself");
        Check(Tab5VoiceAudio.SelectInput(capture,"dji")?.Id=="dji","explicit physical input remains available");
        start=voice.Handle(Json(new{op="start",taskId=task}));audio.InputReady=false;
        for(int i=0;i<8;i++){now+=1000;voice.Handle(Json(new{op="poll",voiceId=start.VoiceId}));}
        Check(voice.Snapshot().State=="error"&&!voice.Snapshot().Ready&&!audio.Running,"unready audio times out without a false ready cue");
        audio.InputReady=true;

        // Real loopback HTTP and production crypto/route, synthetic audio/editor only.
        string directory=Path.Combine(Path.GetTempPath(),"tab5-voice-test-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
        try {
            var store=new Tab5PairingStore(Path.Combine(directory,"pair.dat"));var pair=store.Pair("001122334455",@"USB\VID_303A&PID_1001\VOICE_TEST");
            var key=Convert.FromBase64String(pair.Key);
            using var service=new Tab5Service(store);service.AttachVoice(new Endpoint(voice));
            var snapshot=new StatusSnapshot(1,"12:00",1000,28800,DateTimeOffset.UtcNow,new("idle",0),new("offline",null));service.Publish(snapshot);
            using var frame=JsonDocument.Parse(service.CurrentFrame!);string session=frame.RootElement.GetProperty("session").GetString()!;
            using var reservation=new TcpListener(IPAddress.Loopback,0);reservation.Start();int port=((IPEndPoint)reservation.LocalEndpoint).Port;reservation.Stop();
            using var stop=new CancellationTokenSource();var ready=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var server=new LanStatusServer(new(IPAddress.Loopback,port,"synthetic"),tab5:service).RunAsync(()=>snapshot,stop.Token,()=>ready.SetResult());
            try {
                await ready.Task.WaitAsync(TimeSpan.FromSeconds(5));using var client=new HttpClient{Timeout=TimeSpan.FromSeconds(5)};
                async Task<JsonElement> Send(string op,string voiceId="",int seq=0,byte[]? pcm=null,bool replay=false,bool tamper=false,string? sessionOverride=null) {
                    string nonce=replay?new string('a',32):Guid.NewGuid().ToString("N");
                    var body=Tab5Protocol.Encrypt(key,nonce,JsonSerializer.SerializeToUtf8Bytes(new{op,taskId=task,voiceId,seq,pcm=Convert.ToBase64String(pcm??[]),session=sessionOverride??session,issuedAt=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}));
                    var proof=Tab5Protocol.Proof(key,$"POST|/tab5/v1/voice|{pair.DeviceId}|{nonce}|{Convert.ToHexString(SHA256.HashData(body)).ToLowerInvariant()}");
                    if(tamper)body[^1]^=1;
                    using var request=new HttpRequestMessage(HttpMethod.Post,$"http://127.0.0.1:{port}/tab5/v1/voice"){Content=new ByteArrayContent(body)};
                    request.Headers.Add("X-AIBot-Device",pair.DeviceId);request.Headers.Add("X-AIBot-Nonce",nonce);request.Headers.Add("X-AIBot-Proof",proof);
                    using var response=await client.SendAsync(request);
                    if(tamper){Check(response.StatusCode==HttpStatusCode.Unauthorized,"tampered HTTP audio rejected");return Json(new{});}
                    Check(response.StatusCode==HttpStatusCode.OK,"authenticated voice HTTP reply");
                    byte[] encrypted=await response.Content.ReadAsByteArrayAsync();
                    Check(!Encoding.UTF8.GetString(encrypted).Contains("voiceId"),"voice reply encrypted");
                    return JsonDocument.Parse(Tab5Protocol.Decrypt(key,nonce,encrypted)).RootElement.Clone();
                }
                audio.Dji=false;
                var reply=await Send("start",replay:true);string id=reply.GetProperty("voiceId").GetString()!;
                Check(reply.GetProperty("source").GetString()=="tab5","HTTP start negotiates fallback");
                Check(reply.GetProperty("sourceName").GetString()=="TAB5 内置麦克风","encrypted reply carries the actual source name");
                int starts=audio.Starts;reply=await Send("start",replay:true);
                Check(reply.GetProperty("state").GetString()=="error"&&audio.Starts==starts,"HTTP replay never restarts microphone");
                reply=await Send("audio",id,0,chunk);Check(audio.Bytes==8,"authenticated HTTP PCM is consumed");
                Check(reply.GetProperty("ready").GetBoolean(),"encrypted reply confirms audio readiness on first acknowledged chunk");
                foreach(bool rpc in new[]{false,true})using(var persistent=new TcpClient()) {
                    await persistent.ConnectAsync(IPAddress.Loopback,port);
                    using var stream=persistent.GetStream();
                    for(int attempt=0;attempt<2;attempt++) {
                        string nonce=Guid.NewGuid().ToString("N");
                        var clear=rpc?JsonSerializer.SerializeToUtf8Bytes(new{kind="benchmark",offset=attempt*8192,count=8192,session,issuedAt=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}):JsonSerializer.SerializeToUtf8Bytes(new{op="poll",taskId=task,voiceId=id,seq=0,pcm="",session,issuedAt=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()});
                        var body=Tab5Protocol.Encrypt(key,nonce,clear);string route=rpc?"rpc":"voice";
                        var proof=Tab5Protocol.Proof(key,$"POST|/tab5/v1/{route}|{pair.DeviceId}|{nonce}|{Convert.ToHexString(SHA256.HashData(body)).ToLowerInvariant()}");
                        byte[] header=Encoding.ASCII.GetBytes($"POST /tab5/v1/{route} HTTP/1.1\r\nHost: 127.0.0.1\r\nContent-Length: {body.Length}\r\nX-AIBot-Device: {pair.DeviceId}\r\nX-AIBot-Nonce: {nonce}\r\nX-AIBot-Proof: {proof}\r\n\r\n");
                        await stream.WriteAsync(header);await stream.WriteAsync(body);
                        var responseHeader=new List<byte>();var one=new byte[1];
                        while(responseHeader.Count<8192) {
                            Check(await stream.ReadAsync(one)==1,"voice connection stayed open");
                            responseHeader.Add(one[0]);int count=responseHeader.Count;
                            if(count>=4&&responseHeader[count-4]=='\r'&&responseHeader[count-3]=='\n'&&responseHeader[count-2]=='\r'&&responseHeader[count-1]=='\n')break;
                        }
                        string fields=Encoding.ASCII.GetString(responseHeader.ToArray());
                        Check(fields.StartsWith("HTTP/1.1 200 OK\r\n",StringComparison.Ordinal)&&fields.Contains("Connection: keep-alive",StringComparison.OrdinalIgnoreCase),"voice keep-alive response");
                        int size=int.Parse(fields.Split("\r\n").Single(line=>line.StartsWith("Content-Length:",StringComparison.OrdinalIgnoreCase)).Split(':')[1].Trim());
                        var encrypted=new byte[size];await stream.ReadExactlyAsync(encrypted);
                        using var decoded=JsonDocument.Parse(Tab5Protocol.Decrypt(key,nonce,encrypted));
                        if(rpc)Check(decoded.RootElement.GetProperty("status").GetInt32()==200&&Convert.FromBase64String(decoded.RootElement.GetProperty("body").GetProperty("data").GetString()!).SequenceEqual(Tab5TransportBenchmark.Pattern(attempt*8192,8192)),"consecutive authenticated RPC bytes share one TCP connection");
                        else Check(decoded.RootElement.GetProperty("voiceId").GetString()==id,"consecutive encrypted voice replies share one TCP connection");
                    }
                }
                await Send("audio",id,1,chunk,tamper:true);Check(audio.Bytes==8,"forged audio never consumed");
                reply=await Send("poll",id,sessionOverride:"stale");Check(reply.GetProperty("state").GetString()=="error","old bridge session rejected");
                editor.Text="测试草稿";reply=await Send("poll",id);Check(reply.GetProperty("text").GetString()=="测试草稿","Chinese draft returned encrypted");
                await Send("cancel",id);Check(!audio.Running,"HTTP cancel releases audio");
                async Task<JsonElement> UsbVoice(string op,string voiceId="") {
                    string nonce=Guid.NewGuid().ToString("N");
                    var body=Tab5Protocol.Encrypt(key,nonce,JsonSerializer.SerializeToUtf8Bytes(new{kind="voice",op,taskId=task,voiceId,source="tab5",seq=0,pcm=Convert.ToBase64String(chunk),session,issuedAt=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}));
                    var proof=Tab5Protocol.Proof(key,$"POST|/tab5/v1/rpc|{pair.DeviceId}|{nonce}|{Convert.ToHexString(SHA256.HashData(body)).ToLowerInvariant()}");
                    var response=await service.RpcAsync(pair.DeviceId,nonce,proof,body,CancellationToken.None,transport:"USB");
                    using var decoded=JsonDocument.Parse(Tab5Protocol.Decrypt(key,nonce,response.Packet!));
                    Check(decoded.RootElement.GetProperty("status").GetInt32()==200,"USB voice RPC accepted");
                    return decoded.RootElement.GetProperty("body").Clone();
                }
                var usbVoice=await UsbVoice("start");string usbVoiceId=usbVoice.GetProperty("voiceId").GetString()!;
                Check(usbVoice.GetProperty("source").GetString()=="tab5","USB microphone source retained");
                int beforeUsbBytes=audio.Bytes;await UsbVoice("audio",usbVoiceId);Check(audio.Bytes==beforeUsbBytes+chunk.Length,"USB authenticated PCM consumed");
                await UsbVoice("cancel",usbVoiceId);Check(!audio.Running,"USB cancel releases microphone without sending a message");
                async Task<JsonElement> Ble(string op,string voiceId="",bool tamper=false) {
                    string n=Guid.NewGuid().ToString("N");
                    var clear=JsonSerializer.SerializeToUtf8Bytes(new {op,taskId=task,voiceId,source="tab5",seq=0,codec="ima-adpcm8k",pcm=Convert.ToBase64String(new byte[]{4,0,0,0,0,0,0,0}),session,issuedAt=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()});
                    var body=Tab5Protocol.Encrypt(key,n,clear);
                    string p=Tab5Protocol.Proof(key,$"POST|/tab5/v1/voice|{pair.DeviceId}|{n}|{Convert.ToHexString(SHA256.HashData(body)).ToLowerInvariant()}");
                    if(tamper)body[^1]^=1;
                    byte[] wire=Encoding.ASCII.GetBytes(n+p).Concat(body).ToArray();
                    var result=await Tab5BleVoiceSelfTest.ExchangeAsync(wire,(nonce,proof,packet,ct)=>service.VoiceAsync(pair.DeviceId,nonce,proof,packet,ct,compact:true));
                    using var decoded=JsonDocument.Parse(Tab5Protocol.Decrypt(key,n,result));return decoded.RootElement.Clone();
                }
                reply=await Ble("start");id=reply.GetProperty("voiceId").GetString()!;
                int audioBefore=audio.Bytes;reply=await Ble("audio",id);
                Check(audio.Bytes==audioBefore+16&&reply.GetProperty("ready").GetBoolean(),"GATT compressed audio reaches shared voice state machine");
                try{await Ble("audio",id,true);throw new Exception("Forged GATT audio accepted");}catch(IOException){}
                Check(audio.Bytes==audioBefore+16,"forged GATT frame never reaches audio output");
                editor.Text="蓝牙返回草稿";reply=await Ble("poll",id);
                Check(reply.GetProperty("text").GetString()=="","GATT recording ACK omits repeated transcript");
                await Ble("stop",id);voice.Tick();now+=200;voice.Tick();now+=1200;voice.Tick();reply=await Ble("poll",id);
                Check(reply.GetProperty("text").GetString()==editor.Text,"GATT draft returned encrypted without sending");
                await Ble("cancel",id);Check(!audio.Running,"GATT cancel releases audio");
            }finally{stop.Cancel();await server;}
        }finally{Directory.Delete(directory,true);}
        Console.WriteLine("TAB5_VOICE_SELF_TEST_OK routing, fallback, PCM order, drain, disconnect, focus, cancel, encrypted HTTP and draft return (synthetic audio/IME)");
    }

    private static void CheckReconnectRecovery() {
        long now=1000;var audio=new Audio{Dji=false,Empty=false};var editor=new Editor();
        using var voice=new Tab5VoiceSession(audio,editor,()=>now);
        string task=Guid.NewGuid().ToString();var take=voice.Handle(Json(new{op="start",taskId=task}));
        voice.Handle(Json(new{op="audio",voiceId=take.VoiceId,seq=0,pcm=Convert.ToBase64String(new byte[]{1,0,2,0})}));
        now+=4001;voice.Tick();Check(voice.Snapshot().State=="draining"&&audio.Running,"disconnect preserves already received playback tail");
        now+=1000;voice.Tick();Check(editor.Stops==0,"disconnect does not cut queued playback");
        audio.Empty=true;voice.Tick();now+=200;voice.Tick();
        Check(!audio.Running&&editor.Stops==1,"disconnect ends IME after draining");
        editor.Text="断线前已识别的文字";voice.Tick();now+=1200;voice.Tick();
        Check(voice.Snapshot().State=="review","disconnected take settles without device polling");
        now+=75000;voice.Tick();
        var recovered=voice.Handle(Json(new{op="stop",voiceId=take.VoiceId}));
        Check(recovered.VoiceId==take.VoiceId&&recovered.TaskId==task&&recovered.Text==editor.Text&&recovered.State=="review","same take survives device reply timeout and reconnect");
        int starts=audio.Starts;long bytes=audio.Bytes;
        for(int i=0;i<3;i++)Check(voice.Handle(Json(new{op="poll",voiceId=take.VoiceId})).Text==recovered.Text,"repeated recovery reads same full text");
        Check(audio.Starts==starts&&audio.Bytes==bytes&&editor.Stops==1,"recovery neither starts capture nor replays PCM");
        var next=voice.Handle(Json(new{op="start",taskId=task}));
        bool rejected=false;try{voice.Handle(Json(new{op="stop",voiceId=take.VoiceId}));}catch(Tab5VoiceRequestException){rejected=true;}
        Check(rejected&&audio.Running&&voice.Snapshot().VoiceId==next.VoiceId,"old recovery cannot touch a new take");
        voice.Handle(Json(new{op="cancel",voiceId=next.VoiceId}));now+=300001;voice.Tick();
        Check(voice.Snapshot().State=="idle"&&voice.Snapshot().Text=="","expired result is bounded and cleared");
    }
    private static void CheckQuietCompletion() {
        static byte[] Pcm(Func<int,double> sample) {
            var bytes=new byte[6400];
            for(int i=0;i<3200;i++)System.Buffers.Binary.BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(i*2),(short)(Math.Clamp(sample(i),-1,1)*32767));
            return bytes;
        }
        foreach(int offset in new[]{0,160,640,1100}) {
            var cue=Pcm(i=>{int t=i-offset;double envelope=t<0||t>=1920?0:Math.Min(1,Math.Min(t/160d,(1920-t)/160d));return .3*envelope*Math.Sin(2*Math.PI*880*t/16000);});
            Check(Tab5ReadyCue.IsTone(cue),"known readiness tone with attack/release is distinct from speech");
        }
        Check(!Tab5ReadyCue.IsTone(new byte[6400]),"silence is not a readiness tone");
        Check(!Tab5ReadyCue.IsTone(Pcm(i=>.3*Math.Sin(2*Math.PI*820*i/16000))),"nearby non-cue frequency keeps the recognition grace");
        Check(!Tab5ReadyCue.IsTone(Pcm(i=>.3*Math.Sin(2*Math.PI*1200*i/16000))),"other tones do not bypass the activity latch");
        Check(!Tab5ReadyCue.IsTone(Pcm(i=>.3*Math.Sin(2*Math.PI*880*i/16000)+.15*Math.Sin(2*Math.PI*220*i/16000)+.1*Math.Sin(2*Math.PI*440*i/16000))),"voice-like harmonics mixed with the cue retain the normal wait");
        var activity=new Tab5CaptureActivity();
        Check(activity.Sound is null,"missing capture evidence is not silence");
        int level=Tab5AudioMeter.Measure(new byte[640],16,false,out bool valid);
        activity.Observe(level,valid);Check(activity.Sound==false,"zero PCM confirms quiet capture");
        activity.Observe(50,true);activity.Observe(0,true);
        Check(activity.Sound==true,"speech between meter polls remains latched after a quiet tail");
        activity.Reset();Check(activity.Sound is null,"next take does not inherit silence or speech");
        level=Tab5AudioMeter.Measure(BitConverter.GetBytes(float.NaN),32,true,out valid);
        activity.Observe(level,valid);Check(activity.Sound==true,"invalid capture cannot prove silence");
        activity.Reset();level=Tab5AudioMeter.Measure(new byte[3],16,false,out valid);
        activity.Observe(level,valid);Check(activity.Sound==true,"partial sample cannot prove silence");

        long now=1000;var audio=new Audio{InputLevel=0,CapturedSound=false};var editor=new Editor();
        using var voice=new Tab5VoiceSession(audio,editor,()=>now);
        string task=Guid.NewGuid().ToString();
        Tab5VoiceReply StartAndStop() {
            var start=voice.Handle(Json(new{op="start",taskId=task}));voice.Tick();
            voice.Handle(Json(new{op="stop",voiceId=start.VoiceId}));voice.Tick();now+=200;voice.Tick();return start;
        }
        StartAndStop();now+=1199;voice.Tick();Check(voice.Active,"quiet completion retains the final commit guard");
        now++;voice.Tick();Check(voice.Snapshot() is {State:"cancelled",Text:"",Message:"未检测到语音，已结束"}&&!audio.Running&&editor.Stops==1,"empty quiet take finishes at 1.2 seconds without a microphone error");
        Check(voice.Diagnostic.Contains("finalWaitMs=1200"),"final wait diagnostic freezes at completion");

        editor.CaptureStopped=false;StartAndStop();now+=1200;voice.Tick();
        Check(voice.Active,"never dismiss while recognizer still owns capture");
        editor.Text="稍晚返回的短句";voice.Tick();editor.CaptureStopped=true;now+=800;voice.Tick();
        Check(voice.Snapshot().State=="review"&&voice.Snapshot().Text==editor.Text,"late text wins over quiet classification");

        audio.CapturedSound=true;StartAndStop();now+=5000;voice.Tick();
        Check(voice.Active,"previous sound retains full recognition grace despite silent ending");
        editor.Text="最后一句";voice.Tick();now+=800;voice.Tick();Check(voice.Snapshot().State=="review","spoken late result remains intact");

        audio.CapturedSound=null;StartAndStop();now+=1200;voice.Tick();Check(voice.Active,"unknown input evidence retains normal wait");
        now+=6800;voice.Tick();Check(voice.Snapshot().State=="error","unknown empty result retains original diagnostic timeout");
        audio.CapturedSound=false;StartAndStop();editor.Text="临时结果";voice.Tick();editor.Text="";now+=1200;voice.Tick();
        Check(voice.Active,"a temporarily empty text revision is not no-speech completion");
        voice.Handle(Json(new{op="cancel",voiceId=voice.Snapshot().VoiceId}));
        audio.CapturedSound=true;editor.RecognitionComplete=false;StartAndStop();now+=1200;voice.Tick();
        Check(voice.Active,"keyboard acknowledgement cannot prove final empty recognition");
        editor.RecognitionComplete=true;voice.Tick();now+=799;voice.Tick();
        Check(voice.Active,"native idle retains a final TSF delivery guard");
        now++;voice.Tick();Check(voice.Snapshot() is {State:"cancelled",Message:"未识别到文字，已结束"},"confirmed native empty result finishes despite ambient sound");
        StartAndStop();now+=400;voice.Tick();editor.RecognitionComplete=false;now+=800;voice.Tick();
        Check(voice.Active,"busy recognizer resets idle proof");
        editor.RecognitionComplete=true;voice.Tick();now+=600;editor.Text="最后返回的文字";voice.Tick();now+=800;voice.Tick();
        Check(voice.Snapshot() is {State:"review",Text:"最后返回的文字"},"late committed text wins over native empty completion");
        Console.WriteLine("TAB5_VOICE_QUIET_END_OK silence, native idle, pending commit, noise, late text and unknown capture; synthetic only");
    }
    private static async Task CheckAudioBackpressureAsync() {
        BufferedWaveProvider Buffer()=>new(new WaveFormat(16000,16,1)){BufferDuration=TimeSpan.FromSeconds(2),DiscardOnBufferOverflow=false};
        var burst=Buffer();bool overflow=false;
        try{for(int i=0;i<11;i++)burst.AddSamples(new byte[6400],0,6400);}
        catch(InvalidOperationException ex){overflow=ex.Message.Contains("Buffer full");}
        Check(overflow,"real NAudio provider reproduces Buffer full on unpaced burst");
        long now=1000;var audio=new Audio{Dji=false,Buffer=Buffer()};var editor=new Editor();
        using var voice=new Tab5VoiceSession(audio,editor,()=>now);
        string task=Guid.NewGuid().ToString();
        Tab5VoiceReply Start()=>voice.Handle(Json(new{op="start",taskId=task}));
        JsonElement Chunk(string id,int seq,byte[] pcm)=>Json(new{op="audio",voiceId=id,seq,pcm=Convert.ToBase64String(pcm)});
        var start=Start();using var expected=new MemoryStream();using var played=new MemoryStream();
        int waits=0,peak=0;byte[] read=new byte[640];
        for(int seq=0;seq<30;seq++) {
            byte[] pcm=Enumerable.Range(0,6400).Select(i=>(byte)((seq*17+i)%251)).ToArray();expected.Write(pcm);
            var reply=await voice.HandleAsync(Chunk(start.VoiceId,seq,pcm),default,_=>{
                waits++;now+=20;Check(audio.Buffer.BufferedBytes>=read.Length,"never consume fabricated silence");
                audio.Buffer.Read(read,0,read.Length);played.Write(read);return Task.CompletedTask;
            });
            Check(reply.State=="recording","paced burst remains recording");peak=Math.Max(peak,audio.Buffer.BufferedBytes);
        }
        while(audio.Buffer.BufferedBytes>0){int count=Math.Min(read.Length,audio.Buffer.BufferedBytes);audio.Buffer.Read(read,0,count);played.Write(read,0,count);}
        Check(waits>0&&peak<=12800&&audio.Bytes==192000&&played.ToArray().SequenceEqual(expected.ToArray()),
            "six-second network burst stays below 400ms playback with every byte in order");
        audio.Buffer=null;audio.Room=false;voice.Handle(Json(new{op="cancel",voiceId=start.VoiceId}));start=Start();
        int before=audio.Bytes;
        var cancelled=await voice.HandleAsync(Chunk(start.VoiceId,0,new byte[6400]),default,_=>{
            now+=20;voice.Handle(Json(new{op="cancel",voiceId=start.VoiceId}));return Task.CompletedTask;
        });
        Check(cancelled.State=="cancelled"&&audio.Bytes==before,"cancel interrupts backpressure without feeding pending audio");
        start=Start();
        var stalled=await voice.HandleAsync(Chunk(start.VoiceId,0,new byte[6400]),default,_=>{now+=20;return Task.CompletedTask;});
        Check(stalled.State=="error"&&stalled.Message.Contains("播放积压")&&!stalled.CanInsert&&audio.Bytes==before&&!audio.Running,
            "stalled playback fails boundedly without truncated success or buffer growth");
        start=Start();
        var lostFocus=await voice.HandleAsync(Chunk(start.VoiceId,0,new byte[6400]),default,_=>{now+=20;editor.SafeFocus=false;return Task.CompletedTask;});
        Check(lostFocus.State=="error"&&audio.Bytes==before,"focus loss while waiting never feeds another owner");
        editor.SafeFocus=true;start=Start();
        using var cancel=new CancellationTokenSource();bool tokenCancelled=false;
        try{await voice.HandleAsync(Chunk(start.VoiceId,0,new byte[6400]),cancel.Token,_=>{cancel.Cancel();return Task.CompletedTask;});}
        catch(OperationCanceledException){tokenCancelled=true;}
        Check(tokenCancelled&&audio.Bytes==before,"cancelled request does not feed audio");
        Console.WriteLine("TAB5_AUDIO_BACKPRESSURE_OK real_NAudio_overflow_reproduced 6s_burst_exact_192000_bytes peak_400ms cancel_focus_stall_bounds");
    }
    private static void CheckStoppedAudio() {
        long now=1000;var audio=new Audio{Dji=false};var editor=new Editor();
        using var voice=new Tab5VoiceSession(audio,editor,()=>now);
        string task=Guid.NewGuid().ToString();
        JsonElement Chunk(string id)=>Json(new{op="audio",voiceId=id,seq=0,pcm="AAA="});
        var start=voice.Handle(Json(new{op="start",taskId=task}));
        editor.SafeFocus=false;
        var failed=voice.Handle(Chunk(start.VoiceId));
        Check(failed.State=="error"&&failed.Message.Contains("焦点")&&audio.Bytes==0,"late audio preserves focus-loss cause and is not played");
        Check(voice.Handle(Chunk(start.VoiceId))==failed&&editor.Stops==1,"repeated late audio neither replaces error nor repeats stop");
        editor.SafeFocus=true;start=voice.Handle(Json(new{op="start",taskId=task}));
        voice.Handle(Json(new{op="stop",voiceId=start.VoiceId}));
        Check(voice.Handle(Chunk(start.VoiceId)).State=="draining"&&audio.Bytes==0,"audio after stop returns drain status without failing the take");
        voice.Tick();now+=200;voice.Tick();
        Check(voice.Handle(Chunk(start.VoiceId)).State=="recognizing"&&audio.Bytes==0,"audio during recognition cannot undo stop");
        editor.Text="完整草稿";voice.Tick();now+=1200;voice.Tick();
        Check(voice.Handle(Chunk(start.VoiceId)) is {State:"review",Text:"完整草稿"},"late audio preserves completed draft");
        Console.WriteLine("TAB5_VOICE_LATE_AUDIO_OK focus failure, duplicate audio, drain, recognition and reviewed text; synthetic only");
    }
}
