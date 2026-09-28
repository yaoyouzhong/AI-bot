using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AIBotBridge;

internal static class Tab5VoiceSelfTest
{
    private static void Check(bool pass,string label){if(!pass)throw new InvalidOperationException("TAB5 voice: "+label);}
    private sealed class Audio : ITab5VoiceAudio {
        internal bool Dji=true,Running,Empty=true,FailStop;internal string Source="";internal int Bytes,Starts;
        public string Start(bool force){Running=true;Starts++;return Source=Dji&&!force?"dji":"tab5";}
        public bool DjiHealthy=>Dji;
        public bool InputReady {get;set;}=true;
        public int InputLevel {get;set;}=50;
        public bool Drained=>Empty;
        public void UseTab5()=>Source="tab5";
        public void Feed(byte[] pcm)=>Bytes+=pcm.Length;
        public void Drain(){}
        public void Stop(){Running=false;if(FailStop)throw new InvalidOperationException("synthetic unplug on stop");}
        public void Dispose()=>Stop();
    }
    private sealed class Editor : ITab5VoiceEditor {
        public string Text {get;set;}="";public bool SafeFocus {get;set;}=true;
        internal int Stops;
        public bool Start()=>SafeFocus;
        public bool Stop(){Stops++;return SafeFocus;}
        public void Clear()=>Text="";
    }
    private sealed class Endpoint(Tab5VoiceSession session) : ITab5VoiceEndpoint {
        public Task<Tab5VoiceReply> HandleAsync(JsonElement request,CancellationToken token)=>Task.FromResult(session.Handle(request));
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
    internal static async Task RunAsync() {
        Tab5DoubaoVoiceSelfTest.Run();
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
        audio.Dji=false;start=voice.Handle(Json(new{op="start",taskId=task}));
        Check(start.Source=="tab5"&&start.Text=="","absent DJI falls back, previous text cleared");
        now+=4001;voice.Tick();Check(!audio.Running,"lost heartbeat stops capture");
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
                int starts=audio.Starts;reply=await Send("start",replay:true);
                Check(reply.GetProperty("state").GetString()=="error"&&audio.Starts==starts,"HTTP replay never restarts microphone");
                reply=await Send("audio",id,0,chunk);Check(audio.Bytes==8,"authenticated HTTP PCM is consumed");
                Check(reply.GetProperty("ready").GetBoolean(),"encrypted reply confirms audio readiness on first acknowledged chunk");
                using(var persistent=new TcpClient()) {
                    await persistent.ConnectAsync(IPAddress.Loopback,port);
                    using var stream=persistent.GetStream();
                    for(int attempt=0;attempt<2;attempt++) {
                        string nonce=Guid.NewGuid().ToString("N");
                        var body=Tab5Protocol.Encrypt(key,nonce,JsonSerializer.SerializeToUtf8Bytes(new{op="poll",taskId=task,voiceId=id,seq=0,pcm="",session,issuedAt=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}));
                        var proof=Tab5Protocol.Proof(key,$"POST|/tab5/v1/voice|{pair.DeviceId}|{nonce}|{Convert.ToHexString(SHA256.HashData(body)).ToLowerInvariant()}");
                        byte[] header=Encoding.ASCII.GetBytes($"POST /tab5/v1/voice HTTP/1.1\r\nHost: 127.0.0.1\r\nContent-Length: {body.Length}\r\nX-AIBot-Device: {pair.DeviceId}\r\nX-AIBot-Nonce: {nonce}\r\nX-AIBot-Proof: {proof}\r\n\r\n");
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
                        Check(decoded.RootElement.GetProperty("voiceId").GetString()==id,"consecutive encrypted voice replies share one TCP connection");
                    }
                }
                await Send("audio",id,1,chunk,tamper:true);Check(audio.Bytes==8,"forged audio never consumed");
                reply=await Send("poll",id,sessionOverride:"stale");Check(reply.GetProperty("state").GetString()=="error","old bridge session rejected");
                editor.Text="测试草稿";reply=await Send("poll",id);Check(reply.GetProperty("text").GetString()=="测试草稿","Chinese draft returned encrypted");
                await Send("cancel",id);Check(!audio.Running,"HTTP cancel releases audio");
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
}
