using System.Drawing.Imaging;
using System.Security.Cryptography;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;

namespace AIBotBridge;

// Read visible labels from a fresh hardware frame before touching actions.
// All regions below use the verified .318 landscape viewport.
internal sealed class Tab5NativeScreen
{
    private readonly Tab5AutomationRunner.Backend _backend;
    private readonly Tab5UIAutomation.Result _viewport;
    private readonly string _directory;
    private readonly OcrEngine _ocr;
    private int _sequence;
    private readonly string? _keyboardReference;
    private static readonly Rectangle KeyboardRow=new(38,351,1204,54);
    private static readonly Rectangle Navigation=new(0,617,1168,103);
    private static readonly Rectangle Header=new(30,92,390,85);
    private static readonly Rectangle Send=new(1020,395,225,132);
    private static readonly Rectangle Reply=new(1010,94,234,85);
    private static readonly Rectangle Stop=new(868,94,200,90);
    internal Tab5NativeScreen(Tab5AutomationRunner.Backend backend,Tab5UIAutomation.Result viewport,string directory,string? keyboardReference=null){
        _backend=backend;_viewport=viewport;_directory=directory;
        _keyboardReference=keyboardReference;
        _ocr=OcrEngine.TryCreateFromLanguage(new Language("zh-Hans"))??throw new IOException("Chinese_OCR_unavailable");
    }
    private async Task<Bitmap> Capture(CancellationToken token){
        var frame=await _backend.Capture(token);
        using var original=Tab5ScreenPreviewForm.Render(frame);
        if(original.Width*9!=original.Height*16)throw new IOException("device_frame_orientation_changed");
        return new Bitmap(original,new Size(1280,720));
    }
    private void Write(Bitmap frame,string name)=>frame.Save(Path.Combine(_directory,$"{++_sequence:D3}-{name}.png"),ImageFormat.Png);
    internal async Task Save(string name,CancellationToken token){using var frame=await Capture(token);Write(frame,name);}
    internal async Task CheckClientSwitch(string operation,CancellationToken token){
        await Save("initial",token);
        if(operation=="capture")return;
        await Labeled("总览",Navigation,"overview-tab",token);
        if(operation=="prepare"){await Save("overview",token);return;}
        if(operation is not ("verify" or "sync"))throw new ArgumentException("unknown diagnostic");
        await Labeled("对话",Navigation,"dialogue-tab",token);
        using(var initial=await Capture(token)){
            if(await Locate("返回",Reply,initial) is {} back)await Tap(back,token);
        }
        if(operation=="sync"){
            foreach(string client in new[]{"Codex","Claude"}){
                await SelectClient(client,token);
                await Task.Delay(TimeSpan.FromSeconds(18),token);
                await Save("after-"+client,token);
            }
            await Labeled("总览",Navigation,"overview-after-sync",token);
            return;
        }
        await SelectClient("Claude",token);
        await SelectClient("Codex",token);
        using var frame=await Capture(token);Write(frame,"codex-button");
        Point? button=await Locate("追加",Send,frame)??await Locate("发送",Send,frame);
        if(button==null&&!Matches318Append(frame))throw new IOException("codex_send_label_unconfirmed");
        var fill=frame.GetPixel(1090,button?.Y??472);
        if(fill.B<155||fill.G<140||fill.B-fill.R<18)throw new IOException("codex_send_still_disabled");
        await Labeled("总览",Navigation,"return-overview",token);
    }
    // The Windows OCR engine occasionally misses the two glyphs. This exact
    // RGB digest includes both the glyphs and the full enabled .318 control
    // in an independent hardware reference (005-after-Codex.png).
    internal static bool Matches318Append(Bitmap frame){
        if(frame.Width!=1280||frame.Height!=720)return false;
        byte[] rgb=new byte[164*76*3];int index=0;
        for(int y=434;y<510;y++)for(int x=1064;x<1228;x++){
            var pixel=frame.GetPixel(x,y);
            rgb[index++]=pixel.R;rgb[index++]=pixel.G;rgb[index++]=pixel.B;
        }
        return Convert.ToHexString(SHA256.HashData(rgb))=="2B1D2BD75A187BACDEFCEE871B8B60FA49B551051DCB26C5A5EEECE0E282CEBE";
    }
    private async Task SelectClient(string client,CancellationToken token){
        for(int attempt=0;attempt<5;attempt++){
            using var frame=await Capture(token);
            var button=await Locate(client,Header,frame);
            if(button is null)throw new IOException("client_selector_unavailable");
            var codex=frame.GetPixel(76,128);
            var claude=frame.GetPixel(254,128);
            int difference=codex.G-claude.G;
            if((client=="Codex"?difference:-difference)>15){
                Write(frame,"selected-"+client);return;
            }
            if(attempt<4){await Tap(button.Value,token);await Task.Delay(800,token);}
        }
        throw new IOException("client_selection_not_confirmed_"+client);
    }
    private Task<Point?> Locate(string label,Rectangle region,Bitmap frame)=>Locate(_ocr,label,region,frame);
    private static async Task<Point?> Locate(OcrEngine ocr,string label,Rectangle region,Bitmap frame){
        using var crop=frame.Clone(region,PixelFormat.Format32bppArgb);
        using var bytes=new MemoryStream();crop.Save(bytes,ImageFormat.Png);
        using var stream=new InMemoryRandomAccessStream();
        using(var writer=new DataWriter(stream)){writer.WriteBytes(bytes.ToArray());await writer.StoreAsync();writer.DetachStream();}
        stream.Seek(0);var decoder=await BitmapDecoder.CreateAsync(stream);
        using var bitmap=await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8,BitmapAlphaMode.Ignore);
        var results=await ocr.RecognizeAsync(bitmap);
        List<Point> matches=[];
        foreach(var line in results.Lines){
            for(int i=0;i<line.Words.Count;i++){
                string value="";
                var first=line.Words[i].BoundingRect;
                for(int j=i;j<line.Words.Count && value.Length<label.Length;j++){
                    value+=line.Words[j].Text.Replace(" ","",StringComparison.Ordinal);
                    // The Chinese OCR engine reads Claude's lowercase l as I
                    // on actual TAB5 frames. Normalize this exact client name
                    // only; action labels and arbitrary prefixes stay strict.
                    if(label=="Claude"&&value.StartsWith("CIaude",StringComparison.Ordinal))
                        value="Claude"+value[6..];
                    // OCR can join a client's passive count badge to its name.
                    // Accept only the bounded selector's exact name + count;
                    // send/stop and all other action labels remain exact.
                    bool clientBadge=label is "Claude" or "Codex"&&value.StartsWith(label,StringComparison.Ordinal)&&
                        value[label.Length..] is "1" or "2" or "3" or "4" or "5" or "6" or "7" or "8" or "9" or "9+";
                    if(value!=label&&!clientBadge)continue;
                    var last=line.Words[j].BoundingRect;
                    matches.Add(new Point(region.X+(int)((first.X+last.Right)/2),region.Y+(int)((first.Y+last.Bottom)/2)));
                }
            }
        }
        return matches.Count==1?matches[0]:null;
    }
    internal static async Task CheckScreenReaderAsync(){
        var engine=OcrEngine.TryCreateFromLanguage(new Language("zh-Hans"))??throw new IOException("Chinese_OCR_unavailable");
        using var image=new Bitmap(480,100);
        using(var g=Graphics.FromImage(image)){
            g.Clear(Color.FromArgb(38,55,61));
            using var font=new Font("Microsoft YaHei",27,FontStyle.Regular,GraphicsUnit.Pixel);
            g.DrawString("停止任务",font,Brushes.White,new Point(195,32));
        }
        var found=await Locate(engine,"停止任务",new Rectangle(130,20,260,70),image);
        if(found is null||found.Value.X<193||found.Value.X>308||found.Value.Y<30||found.Value.Y>74)
            throw new IOException("OCR action location unavailable");
        Console.WriteLine("TAB5_SUITE_OCR_OK Chinese label recognized in a bounded synthetic frame");
    }
    private async Task Tap(Point point,CancellationToken token){
        if(!Tab5DesktopActivation.IsInteractive())throw new IOException("desktop_locked_or_unavailable");
        var action=System.Text.Json.JsonSerializer.SerializeToElement(new{op="tap",width=_viewport.Width,height=_viewport.Height,rotation=_viewport.Rotation,x=point.X,y=point.Y});
        await _backend.Action(action,token);
    }
    private async Task<Point> Labeled(string label,Rectangle region,string evidence,CancellationToken token){
        for(int i=0;i<3;i++){
            using var frame=await Capture(token);
            var point=await Locate(label,region,frame);
            if(point is null&&label=="追加"&&region==Send&&Matches318Append(frame))point=new Point(1146,472);
            if(point!=null){Write(frame,evidence);await Tap(point.Value,token);return point.Value;}
            if(i==2){Write(frame,evidence+"-unavailable");throw new IOException("device_label_unconfirmed_"+evidence);}
            await Task.Delay(300,token);
        }
        throw new IOException("device_label_unconfirmed_"+evidence);
    }
    internal async Task EnterClaude(CancellationToken token){
        using(var frame=await Capture(token)){
            if(await Locate("对话",Navigation,frame)==null){
                Write(frame,"wake");
                await Tap(new Point(20,20),token);
                await Task.Delay(300,token);
            }
        }
        await Labeled("对话",Navigation,"dialogue",token);
        if(_keyboardReference is not null){
            using var frame=await Capture(token);
            if(KeyboardMatches(frame))await Tap(new Point(1158,570),token);
        }
        await SelectClient("Claude",token);
        // In this diagnostic workspace the only visible Claude row is the bound
        // original session. The subsequent device draft-load must match its alias.
        await Tap(new Point(192,290),token);
    }
    internal async Task OpenReply(CancellationToken token){
        using var frame=await Capture(token);
        if(await Locate("返回",Reply,frame)!=null)return;
        var point=await Locate("查看新回复",Reply,frame)??await Locate("查看回复",Reply,frame);
        if(point==null){Write(frame,"reply-unavailable");throw new IOException("device_reply_entry_unconfirmed");}
        Write(frame,"open-reply");await Tap(point.Value,token);
    }
    internal Task ReturnToComposer(CancellationToken token)=>Labeled("返回",Reply,"return",token);
    private static int DraftInk(Bitmap frame){
        int count=0;
        for(int y=183;y<337;y++)for(int x=479;x<1213;x++){
            var p=frame.GetPixel(x,y);
            if(p.R>180&&p.G>180&&p.B>180)count++;
        }
        return count;
    }
    internal async Task<bool> HasEmptyOrW(CancellationToken token){
        using var frame=await Capture(token);Write(frame,"append-before-work");
        if(DraftInk(frame)<50)return true;
        if(await Locate("w",new Rectangle(467,173,758,170),frame)!=null)return false;
        // The Chinese OCR engine can miss this single Latin glyph. Compare
        // the complete .318 composer interior against the reviewed w frame;
        // accepting this reference never types over an existing draft.
        byte[] rgb=new byte[734*228*3];int index=0;
        for(int y=183;y<411;y++)for(int x=479;x<1213;x++){
            var pixel=frame.GetPixel(x,y);
            rgb[index++]=pixel.R;rgb[index++]=pixel.G;rgb[index++]=pixel.B;
        }
        if(Convert.ToHexString(SHA256.HashData(rgb))=="47209783719829D103A28E08F7DC09C028E1940D15CE0B11638F4F665E73DCF7")return false;
        throw new IOException("prepared_append_not_confirmed_on_device");
    }
    internal async Task<bool> InspectControlDraft(CancellationToken token){
        using var frame=await Capture(token);
        Write(frame,"resumed-composer");
        if(await Locate("查看回复",Reply,frame)==null&&await Locate("查看新回复",Reply,frame)==null)
            throw new IOException("original_test_composer_unavailable");
        if(DraftInk(frame)<50)return true;
        if(await Locate("接口验收",new Rectangle(467,173,758,170),frame)!=null)return false;
        throw new IOException("resumed_test_draft_unrecognized");
    }
    internal async Task<(Point Submit,Point Reply)> ComposerActions(CancellationToken token){
        using var frame=await Capture(token);
        var submit=await Locate("发送",Send,frame);
        var reply=await Locate("查看回复",Reply,frame)??await Locate("查看新回复",Reply,frame);
        if(submit is null||reply is null)throw new IOException("device_composer_actions_unavailable");
        Write(frame,"ready-for-running-turn");
        return (submit.Value,reply.Value);
    }
    internal Task TapLocated(Point point,CancellationToken token)=>Tap(point,token);
    private async Task EnsureKeyboard(string name,CancellationToken token){
        using(var initial=await Capture(token)){
            if(KeyboardMatches(initial)){Write(initial,name);return;}
        }
        await Tap(new Point(820,250),token);
        for(int i=0;i<6;i++){
            using var frame=await Capture(token);
            if(KeyboardMatches(frame)){Write(frame,name);return;}
            if(i==5)Write(frame,"unexpected-keyboard");
            await Task.Delay(300,token);
        }
        throw new IOException("verified_keyboard_layout_changed");
    }
    internal async Task VerifyKeyboard(CancellationToken token){
        await EnsureKeyboard("verified-keyboard",token);await Tap(new Point(1190,567),token);
    }
    internal async Task TypeVerifiedW(CancellationToken token){
        // The same session, orientation and stock keyboard were verified before
        // the timed turn; the device must acknowledge the exact draft before Send.
        await Tap(new Point(820,250),token);
        await Tap(new Point(290,379),token);
        await Tap(new Point(1190,567),token);
    }
    internal Task<Point> ActionButton(string label,CancellationToken token)=>Labeled(label,label=="停止任务"?Stop:Send,"action-"+(label=="停止任务"?"stop":label=="追加"?"append":"send"),token);
    internal async Task ClearFixture(CancellationToken token){
        using var frame=await Capture(token);
        using var reference=new Bitmap(_keyboardReference!);
        if(!RegionMatches(frame,reference,Header))throw new IOException("fixture_client_selection_unconfirmed");
        var point=await Locate("长按清空",new Rectangle(900,94,165,85),frame);
        if(point is null)throw new IOException("fixture_clear_unavailable");
        Write(frame,"clear-owned-fixture");
        if(!Tab5DesktopActivation.IsInteractive())throw new IOException("desktop_locked_or_unavailable");
        // This continuation exposes only the original diagnostic session and its
        // owned q/w drafts. A stationary swipe holds the existing long-press control.
        await _backend.Action(System.Text.Json.JsonSerializer.SerializeToElement(new{
            op="swipe",width=_viewport.Width,height=_viewport.Height,rotation=_viewport.Rotation,
            x=point.Value.X,y=point.Value.Y,toX=point.Value.X,toY=point.Value.Y,durationMs=1000}),token);
    }
    internal async Task TypeLetter(string letter,CancellationToken token){
        using(var frame=await Capture(token)){
            if(await Locate("Claude",Header,frame)==null)throw new IOException("device_composer_not_visible");
        }
        if(_keyboardReference is null){
            await Tap(new Point(820,250),token);
            await Labeled(letter,new Rectangle(30,340,1220,90),"key-"+letter,token);
        }
        else{
            if(letter is not ("q" or "w"))throw new IOException("unsupported_fixture_key");
            await EnsureKeyboard("key-"+letter,token);
            await Tap(new Point(letter=="q"?195:290,379),token);
        }
        // The .318 stock LVGL keyboard's bottom-right Apply key commits pending
        // pinyin. The campaign independently verifies the exact device draft-save.
        await Tap(new Point(1190,567),token);
    }
    private bool KeyboardMatches(Bitmap frame){
        using var reference=new Bitmap(_keyboardReference!);
        return RegionMatches(frame,reference,KeyboardRow);
    }
    private static bool RegionMatches(Bitmap frame,Bitmap reference,Rectangle region){
        if(reference.Width!=1280||reference.Height!=720)return false;
        for(int y=region.Top;y<region.Bottom;y++)
            for(int x=region.Left;x<region.Right;x++)
                if(frame.GetPixel(x,y)!=reference.GetPixel(x,y))return false;
        return true;
    }
}
