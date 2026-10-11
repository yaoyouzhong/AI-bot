using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;

namespace AIBotBridge;

// Reviewed local text, joined by stable work identity rather than daily index or title.
internal static class Tab5ArtGuides
{
    // Native readers already consume UTF-8. Avoid doubling Chinese originals
    // into ASCII \uXXXX escapes, retaining JSON's HTML-sensitive escaping.
    internal static readonly JsonSerializerOptions WireOptions=new(JsonDefaults.Options){Encoder=JavaScriptEncoder.Create(UnicodeRanges.All)};
    internal sealed record Guide(string WorkId,string Title,string Author,string Period,string Museum,string Brief,string Overview,string Looking,string Transcription,string Explanation);
    private static readonly Lazy<IReadOnlyDictionary<string,Guide>> Catalog=new(()=> {
        using var stream=typeof(Tab5ArtGuides).Assembly.GetManifestResourceStream("AIBotBridge.ArtGuides");
        // Standard releases receive artwork text only from installed collection packs.
        // A local curation build may explicitly embed its reviewed catalogue.
        if(stream is null)return new Dictionary<string,Guide>(StringComparer.Ordinal);
        using var document=JsonDocument.Parse(stream);
        return Read(document.RootElement);
    });
    internal static Guide? Find(string workId)=>Catalog.Value.GetValueOrDefault(workId);
    internal static IReadOnlyDictionary<string,Guide> ReadPack(JsonElement root,Tab5Gallery.Artwork[] works) {
        try {
            if(root.GetProperty("schemaVersion").GetInt32()!=1||root.GetProperty("language").GetString()!="zh-CN")
                throw new InvalidDataException("图库导读格式不兼容。");
            var expected=works.ToDictionary(w=>w.Id,StringComparer.Ordinal);
            if(expected.Count==0)throw new InvalidDataException("图库导读没有作品。");
            var seen=new HashSet<string>(StringComparer.Ordinal);
            foreach(var row in root.GetProperty("works").EnumerateArray()) {
                string id=row.GetProperty("id").GetString()??"";
                if(!seen.Add(id)||!expected.TryGetValue(id,out var work)||row.GetProperty("category").GetString()!=work.Category||
                   row.GetProperty("workId").GetString()!=(work.WorkId??work.Source)||
                   row.GetProperty("sources").GetArrayLength()==0)
                    throw new InvalidDataException("图库导读与作品身份或来源不一致。");
                string? status=row.GetProperty("transcription").GetProperty("status").GetString();
                if(status is not ("source_verified" or "reference_verified" or "not_available" or "withheld_pending_review"))
                    throw new InvalidDataException("图库原文核实状态无效。");
                if(status is "source_verified" or "reference_verified"&&string.IsNullOrWhiteSpace(row.GetProperty("transcription").GetProperty("text").GetString()))
                    throw new InvalidDataException("图库已核实原文为空。");
            }
            if(seen.Count!=works.Length)throw new InvalidDataException("图库导读缺少作品。");
            var guides=Read(root);
            foreach(var work in works) {
                var guide=guides[work.WorkId??work.Source];
                if(string.IsNullOrWhiteSpace(guide.Title)||string.IsNullOrWhiteSpace(guide.Author)||string.IsNullOrWhiteSpace(guide.Overview))
                    throw new InvalidDataException("图库导读内容不完整。");
                var manifest=new{status=200,body=new{work.Id,work.Title,work.Author,work.Source,work.License,date="2026-10-10",size=1048576,sha256=new string('f',64),frames=work.Frames.Length,width=1280,height=720,orientation="portrait",guideVersion=2,guide}};
                if(JsonSerializer.SerializeToUtf8Bytes(manifest,WireOptions).Length>16384)
                    throw new InvalidDataException("图库导读超过设备回复上限。");
            }
            return guides;
        } catch(Exception ex) when(ex is KeyNotFoundException or InvalidOperationException or FormatException) {
            throw new InvalidDataException("图库导读字段无效。",ex);
        }
    }
    internal static IReadOnlyDictionary<string,Guide> Read(JsonElement root) {
        var result=new Dictionary<string,Guide>(StringComparer.Ordinal);
        foreach(var row in root.GetProperty("works").EnumerateArray()) {
            string Text(JsonElement item,string name,int limit) {
                if(item.TryGetProperty(name,out var value)&&value.ValueKind!=JsonValueKind.String)
                    throw new InvalidDataException("Artwork guide field must be text: "+name);
                string text=value.ValueKind==JsonValueKind.String?value.GetString()!:"";
                if(text.Contains('\0')||Encoding.UTF8.GetByteCount(text)>=limit)throw new InvalidDataException("Artwork guide field exceeds device bounds: "+name);
                return text;
            }
            string id=Text(row,"workId",80);if(string.IsNullOrEmpty(id))throw new InvalidDataException("Artwork guide identity missing");
            string transcription="",explanation="";
            if(row.TryGetProperty("transcription",out var original)&&original.ValueKind==JsonValueKind.Object&&
               original.TryGetProperty("status",out var status)&&status.GetString() is "source_verified" or "reference_verified") {
                transcription=Text(original,"text",8192);explanation=Text(original,"explanation",512);
            }
            var guide=new Guide(id,Text(row,"title",256),Text(row,"author",128),Text(row,"period",96),Text(row,"museum",80),
                Text(row,"brief",256),Text(row,"overview",640),Text(row,"looking",384),transcription,explanation);
            if(!result.TryAdd(id,guide))throw new InvalidDataException("Duplicate artwork guide identity");
        }
        return result;
    }
    internal static Guide? ForReader(string workId,int version) {
        return ForReader(Find(workId),version);
    }
    internal static Guide? ForReader(Guide? guide,int version) {
        if(guide is not null&&version<2&&Encoding.UTF8.GetByteCount(guide.Transcription)>=512)
            return guide with {Transcription="这幅作品所写的诗文已补入完整原文。\n当前设备版本只支持短篇，请升级后阅读完整正文。",Explanation=""};
        return guide;
    }
    internal static void SelfTest() {
        using var optionalSource=typeof(Tab5ArtGuides).Assembly.GetManifestResourceStream("AIBotBridge.ArtGuides");
        if(optionalSource is null) {
            if(Catalog.Value.Count!=0||Find("colbase:tnm:TB-363") is not null)
                throw new Exception("Standard release unexpectedly contains artwork text");
            var fixture=new Guide("fixture","作品","作者","年代","藏馆","简要导读","简介","欣赏提示",new string('文',1000),"原文说明");
            if(ForReader(fixture,2)!=fixture||ForReader(fixture,1)?.Transcription.Contains("当前设备版本")!=true||ForReader((Guide?)null,2) is not null)
                throw new Exception("Pack-only guide reader compatibility failed");
            Console.WriteLine("ART_GUIDES_PACK_ONLY_OK no bundled collection text; full v2 original, v1 upgrade notice and absent legacy guide");
            return;
        }
        var guides=Catalog.Value;if(guides.Count!=2632)throw new Exception("Reviewed guide catalogue coverage changed");
        using var source=typeof(Tab5ArtGuides).Assembly.GetManifestResourceStream("AIBotBridge.ArtGuides")!;
        using var frozen=JsonDocument.Parse(source);
        int objectTexts=0,referenceTexts=0;
        foreach(var row in frozen.RootElement.GetProperty("works").EnumerateArray()) {
            var original=row.GetProperty("transcription");
            string? status=original.GetProperty("status").GetString();
            bool verified=status is "source_verified" or "reference_verified";
            string expected=verified?original.GetProperty("text").GetString()!:"";
            if(Find(row.GetProperty("workId").GetString()!)!.Transcription!=expected)
                throw new Exception("Frozen reviewed text changed or was truncated");
            if(status=="source_verified")objectTexts++;
            if(status=="reference_verified")referenceTexts++;
        }
        int readable=objectTexts+referenceTexts;
        if(readable<87||guides.Values.Count(g=>g.Transcription.Length>0)!=readable)
            throw new Exception("Reviewed original-text coverage regressed");
        string[] previous={"https://digitalarchive.npm.gov.tw/Collection/Detail/19?dep=P","https://digitalarchive.npm.gov.tw/Collection/Detail/2?dep=P","https://www.dpm.org.cn/collection/handwriting/228199.html","https://www.dpm.org.cn/collection/handwriting/228204.html","npm:K2B000001","npm:K2B000002","cma:1961.421.2","cma:2003.353","cma:2003.354","colbase:tnm:TB-1271","colbase:tnm:TB-1272","colbase:tnm:TB-1443","colbase:tnm:TB-1589","paris:M.C. 2004-17+M.C. 2004-18"};
        foreach(string id in previous)if(Find(id)?.Transcription.Length is not >0)throw new Exception("Previous reviewed original lost: "+id);
        foreach(var guide in guides.Values) {
            if(guide.Overview.Length==0)throw new Exception("Artwork guide overview missing");
            // v2 accommodates complete classical essays; protocol replies allow
            // 32768 bytes, while we keep the entire manifest below 16 KiB.
            var manifest=new{status=200,body=new{id="fixture",title=guide.Title,author=guide.Author,source=new string('x',256),license="fixture",date="2026-10-10",size=1048576,sha256=new string('f',64),frames=64,width=1280,height=720,orientation="portrait",guideVersion=2,guide}};
            if(JsonSerializer.SerializeToUtf8Bytes(manifest,WireOptions).Length>16384)throw new Exception("Full v2 manifest exceeds reply budget");
            var legacy=ForReader(guide.WorkId,1)!;
            if(Encoding.UTF8.GetByteCount(legacy.Transcription)>=512||Encoding.UTF8.GetByteCount(legacy.Explanation)>=512)throw new Exception("Old firmware received oversized original");
            if(JsonSerializer.SerializeToUtf8Bytes(new{status=200,body=new{guide=legacy}},JsonDefaults.Options).Length>8192)throw new Exception("Legacy guide reply grew beyond its budget");
            if(Encoding.UTF8.GetByteCount(guide.Transcription)>=512&&(!legacy.Transcription.Contains("当前设备版本")||legacy.Explanation.Length!=0))throw new Exception("Legacy whole-text notice missing");
            if(ForReader(guide.WorkId,2)!=guide)throw new Exception("v2 text was replaced or truncated");
        }
        if(Find("unknown-work") is not null)throw new Exception("Unknown identity gained an unrelated guide");
        var today=Find("colbase:tnm:TB-1261")!;
        if(!today.Transcription.Contains("满市花风起")||!today.Transcription.EndsWith("转盼夕烟浮。"))throw new Exception("Today's Qin Guan poem incomplete");
        var longest=Find("colbase:tnm:TB-1224")!;
        string body=longest.Transcription.Split("\n\n",2)[1];
        if(body.Count(c=>c is >= '\u3400' and <= '\u9fff')!=1000||!body.EndsWith("焉哉乎也。"))throw new Exception("Thousand Character Classic truncated");
        var taohua=Find("mia:2001.138.2")!;
        if(!taohua.Transcription.StartsWith("苏轼《桃花悟道》（通行全文）")||!taohua.Transcription.EndsWith("豈可得哉？")||!taohua.Explanation.Contains("不将古籍文字冒充本幅逐字释文"))throw new Exception("Reviewed Peach Blossom reference lost its scope or full text");
        if(Find("colbase:tnm:TB-1577")!.Transcription!="王维《书事》\n轻阴阁小雨，深院昼慵开。\n坐看青苔色，欲上人衣来。")throw new Exception("Image-checked Wang Wei poem or its variant changed");
        if(Find("cma:2023.162")!.Transcription!="永夜閒階臥桂影\n清晨簾幕卷輕霜")throw new Exception("Image-checked couplet regressed to museum OCR error");
        if(Find("colbase:tnm:TB-1518")!.Transcription.EndsWith("千载不相违。")!=true)throw new Exception("Second-batch Tao poem incomplete");
        if(!guides.Values.Any(g=>g.Transcription.Contains("走笔谢孟谏议寄新茶")&&g.Transcription.EndsWith("到头合得苏息否？")))throw new Exception("Tea Song full ending missing");
        var memorials=Find("colbase:tnm:TB-1217")!;
        if(Encoding.UTF8.GetByteCount(memorials.Transcription)<=4096||!memorials.Transcription.Contains("不知所言。\n\n《后出师表》")||!memorials.Transcription.EndsWith("非臣之明所能逆睹也。"))throw new Exception("Both expedition memorials must remain complete beyond 4 KiB");
        if(!guides.Values.Any(g=>g.Transcription.Contains("送王渙之彥舟")&&g.Transcription.EndsWith("溪堂米黻記。")))throw new Exception("Shusu Tie final poem or signature missing");
        if(Find("mia:2022.76.27")?.Title!="行书苏轼《临皋闲题》节录扇面")throw new Exception("Su Shi prose mislabelled as a poem");
        var stele=Find("colbase:tnm:TB-363")!;
        if(Encoding.UTF8.GetByteCount(stele.Transcription)!=7390||!stele.Transcription.EndsWith("河南史華刻"))
            throw new Exception("Full Duobao Pagoda inscription changed or was truncated");
        Console.WriteLine($"ART_GUIDES_OK works={guides.Count}; readableOriginals={readable}; {objectTexts} object transcriptions + {referenceTexts} literary references; exact frozen text, complete Qin Guan, expedition memorials, Shusu Tie, 1000-character text and 7390-byte stele; v1/v2 bounds and identity");
    }
}
