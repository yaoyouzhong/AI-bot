using System.Text;
using System.Text.RegularExpressions;

namespace AIBotBridge;

internal static class Tab5SpeechTranscript
{
    // The recognizer owns only the suffix captured at Start, never the user's
    // draft prefix. Recompute each snapshot so corrected segments are not appended.
    internal static string Project(string text,string prefix)
    {
        if(!text.StartsWith(prefix,StringComparison.Ordinal))return text;
        var parts=Regex.Split(text[prefix.Length..],@"[\r\n\u0085\u2028\u2029]+[ \t]*");
        var result=new StringBuilder(parts[0]);
        foreach(string part in parts.Skip(1)) {
            var next=part.TrimStart(' ','\t');
            if(next.Length==0)continue;
            while(result.Length>0&&result[^1] is ' ' or '\t')result.Length--;
            if(result.Length>0&&!Cjk(result[^1])&&!Cjk(next[0])&&!char.IsPunctuation(next[0]))result.Append(' ');
            result.Append(next);
        }
        return prefix+result;
    }
    private static bool Cjk(char c)=>c is >= '\u2e80' and <= '\u9fff' or >= '\uf900' and <= '\ufaff' or >= '\u3000' and <= '\u303f';
    internal static void SelfTest() {
        void Check(bool ok){if(!ok)throw new InvalidOperationException("Speech paragraph boundary");}
        const string draft="原草稿\r\n手动第二行\r\n";
        Check(Project(draft+"今天\r\n天气\u2028很好",draft)==draft+"今天天气很好");
        Check(Project("hello\nworld\u2029again","")=="hello world again");
        Check(Project("hello\n世界","")=="hello世界");
        Check(Project("corrected\ntext","")=="corrected text");
        Check(Project("用户修改\n原文","原文")=="用户修改\n原文");
        Check(Tab5VoiceSession.BoundText(Project(new string('中',800)+"\n文","")).Length==666);
        Console.WriteLine("TAB5_SPEECH_PARAGRAPH_OK draft/CRLF/Unicode/English/correction/manual-edit/UTF8");
    }
}
