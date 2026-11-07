using System.Security.Cryptography;
using System.Text;

namespace AIBotBridge;

internal static class ClaudeNativeReceiptSelfTest
{
    internal static void Run()
    {
        Guid session=Guid.NewGuid(),old=Guid.NewGuid(),sent=Guid.NewGuid(),other=Guid.NewGuid();
        const string text="中文🙂\nKeep spaces  and punctuation!";int checks=0;
        string Digest(string value)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
        ClaudeHistory.Value History(Dictionary<Guid,string> inputs,bool limited=false,Guid? image=null)=>
            new([],limited,inputs.Keys.Select(id=>id.ToString()).Concat(image is {} i?[i.ToString()]:[]).ToHashSet(),inputs.ToDictionary(p=>p.Key.ToString(),p=>Digest(p.Value)));
        ClaudeNativeReceipt Read(ClaudeHistory.Value value,string expected=text,string scope="host-generation-a",Guid? id=null)=>
            ClaudeNativeReceipt.Capture(value,id??session,scope,expected);
        void Require(bool value,string reason){checks++;if(!value)throw new InvalidOperationException(reason);}
        var initial=History(new(){{old,text}});var before=Read(initial);
        var after=Read(History(new(){{old,text},{sent,text}}));
        Require(after.Confirm(before)==sent,"Exact newly accepted native user message not found");
        Require(Read(initial).Confirm(before) is null,"Old identical input was mistaken for a new send");
        Require(Read(History(new(){{old,text},{sent,text+" "}})).Confirm(before) is null,"Changed spacing was accepted");
        Require(Read(History(new(){{old,text},{sent,text.Replace("\n"," ")}})).Confirm(before) is null,"Changed newline was accepted");
        Require(Read(History(new(){{old,text},{sent,text},{other,text}})).Confirm(before) is null,"Two matching native inputs were ambiguous");
        Require(Read(History(new(){{old,text},{sent,text},{other,"other input"}})).Confirm(before) is null,"Intervening user activity was ignored");
        Require(Read(History(new(){{sent,text}})).Confirm(before) is null,"Changed main branch was accepted");
        Require(Read(History(new(){{old,text},{sent,text}},limited:true)).Confirm(before) is null,"Incomplete after history was accepted");
        Require(after.Confirm(Read(History(new(){{old,text}},limited:true))) is null,"Incomplete baseline was accepted");
        Require(Read(History(new(){{old,text},{sent,text}}),scope:"host-generation-b").Confirm(before) is null,"A replaced native host was accepted");
        Require(Read(History(new(){{old,text},{sent,text}}),id:Guid.NewGuid()).Confirm(before) is null,"Another session was accepted");
        Require(Read(History(new(){{old,text}},image:sent)).Confirm(before) is null,"Image-bearing input satisfied a text-only receipt");
        // Public replies and peer records do not add accepted user IDs/hashes.
        var repliesOnly=History(new(){{old,text}});repliesOnly.Turns.Add(new(){["items"]=new[]{new{type="agentMessage",text}}});
        Require(Read(repliesOnly).Confirm(before) is null,"Assistant output was mistaken for a user receipt");
        var noHash=History(new(){{old,text},{sent,text}});noHash.InputDigests.Remove(sent.ToString());
        Require(Read(noHash).Confirm(before) is null,"An unverified user body was accepted");
        Require(!System.Text.Json.JsonSerializer.Serialize(after).Contains("Keep spaces"),"Receipt retained private prompt text");
        byte[] ImageBytes(Color color,System.Drawing.Imaging.ImageFormat format){using var bitmap=new Bitmap(8,8);using(var g=Graphics.FromImage(bitmap))g.Clear(color);using var stream=new MemoryStream();bitmap.Save(stream,format);return stream.ToArray();}
        var png=ImageBytes(Color.Coral,System.Drawing.Imaging.ImageFormat.Png);
        var bmp=ImageBytes(Color.Coral,System.Drawing.Imaging.ImageFormat.Bmp);
        var otherPng=ImageBytes(Color.Navy,System.Drawing.Imaging.ImageFormat.Png);
        string photo=ClaudeNativeContent.ImageDigest(png),different=ClaudeNativeContent.ImageDigest(otherPng);
        Require(photo==ClaudeNativeContent.ImageDigest(bmp),"Lossless reencoding changed photo identity");
        Require(photo!=different,"Different photos collided");
        string Message(params byte[][] photos)=>ClaudeNativeContent.MessageDigest(System.Text.Json.JsonSerializer.SerializeToElement(
            new object[]{new{type="text",text}}.Concat(photos.Select(bytes=>(object)new{type="image",source=new{type="base64",media_type="image/png",data=Convert.ToBase64String(bytes)}}))))!;
        var photoBefore=ClaudeNativeReceipt.Capture(initial,session,"host-generation-a",text,[photo,different]);
        ClaudeNativeReceipt PhotoAfter(string digest){var h=History(new(){{old,text}});h.Accepted.Add(sent.ToString());h.InputDigests[sent.ToString()]=digest;return ClaudeNativeReceipt.Observe(h,photoBefore,"host-generation-a");}
        Require(PhotoAfter(Message(png,otherPng)).Confirm(photoBefore)==sent,"Exact ordered photos were not accepted");
        Require(PhotoAfter(Message(otherPng,png)).Confirm(photoBefore) is null,"Reordered photos were accepted");
        Require(PhotoAfter(Message(png)).Confirm(photoBefore) is null,"Missing photo was accepted");
        Require(PhotoAfter(Message(otherPng,otherPng)).Confirm(photoBefore) is null,"Wrong photo was accepted");
        Require(PhotoAfter(Digest(text)).Confirm(photoBefore) is null,"Text-only input satisfied photo receipt");
        Require(ClaudeNativeContent.Digest("",[photo])!=ClaudeNativeContent.Digest(""),"Image-only input collided with empty input");
        Console.WriteLine($"CLAUDE_NATIVE_RECEIPT_SELF_TEST_OK {checks} exact-input/session/host/branch/ambiguity guards; no native input performed");
    }
}
