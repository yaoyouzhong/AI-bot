using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Win32.SafeHandles;

namespace AIBotBridge;

// Short-lived worker; never attach the resident GUI process to a user's
// console. Do not change modes/codepages, flush input or signal process groups.
internal static class ClaudeNativeConsole
{
    internal static void Run() {
        using var output=Console.OpenStandardOutput();
        string stage="profile";
        try {
            AppProfileGuard.EnsureDesktopProfile();
            string raw=Console.ReadLine()??"";if(raw.Length>16000)throw new IOException("Invalid native request");
            var ticket=JsonSerializer.Deserialize<ClaudeNativeTerminal.Ticket>(raw)??throw new IOException("Invalid native request");
            stage="identity";
            var native=ClaudeNativeTerminal.Find(ticket.Session)??throw new IOException("Native terminal unavailable");
            void Identity(){if(native.Scope()!=ticket.Scope||native.State is not ("idle" or "busy" or "working"))throw new IOException("Native terminal changed");}
            Identity();stage="integrity";if(Integrity((uint)native.Pid)!=Integrity((uint)Environment.ProcessId))throw new IOException("Native terminal integrity differs");
            stage="attach";
            FreeConsole();if(!AttachConsole((uint)native.Pid))throw new Win32Exception(Marshal.GetLastWin32Error());
            try {
                using var input=CreateFileW("CONIN$",0xC0000000,3,0,3,0,0);using var screen=CreateFileW("CONOUT$",0x80000000,3,0,3,0,0);
                if(input.IsInvalid||screen.IsInvalid)throw new IOException("Native console unavailable");
                uint[] pids=new uint[128];uint count=GetConsoleProcessList(pids,128);
                if(count==0||count>128||!pids.Take((int)count).Contains((uint)native.Pid))throw new IOException("Native console identity changed");
                foreach(uint pid in pids.Take((int)count).Where(p=>p!=native.Pid&&p!=Environment.ProcessId)) {
                    using var p=Process.GetProcessById((int)pid);if(p.ProcessName=="claude")throw new IOException("Ambiguous native console");
                }
                bool exclusive=ticket.Action is "exclusive" or "foreground-type" or "foreground-send" or "foreground-interrupt" or "foreground-verify";
                void Ready(){
                    Identity();
                    if(!GetConsoleMode(input,out uint mode)||(mode&7)!=0)throw new IOException("Native raw input unavailable");
                    if(exclusive){
                        uint owners=GetConsoleProcessList(pids,128);
                        if(owners!=2||!pids.Take(2).Contains((uint)native.Pid)||!pids.Take(2).Contains((uint)Environment.ProcessId))
                            throw new IOException("Native console is shared");
                    }
                }
                void BeforeWrite(){
                    Ready();
                    if(ticket.ParentUser is {} user&&!native.RunningTurn(user))throw new IOException("Native turn changed");
                    if(Environment.TickCount64>=ticket.WriteDeadline)throw new IOException("Native write expired");
                }
                stage="raw_mode";Ready();stage="composer";var before=Snapshot(screen);
                var value=before;
                if(ticket.Action is not ("read" or "exclusive")) {
                    stage="input";
                    string keys=Path.Combine(AppPaths.GetFolderPath(Environment.SpecialFolder.UserProfile),".claude","keybindings.json");
                    if(File.Exists(keys))throw new IOException("Native keybindings changed");
                    if(ticket.Expected!=before)throw new IOException("Native snapshot changed");
                    if(new[]{0x10,0x11,0x12,0x5B,0x5C}.Any(k=>(GetAsyncKeyState(k)&0x8000)!=0))throw new IOException("Native modifier active");
                    BeforeWrite();
                    if(ticket.Action=="foreground-type"){
                        if(ticket.ParentUser is null&&native.State!="idle"||ticket.Text.Length is 0 or >8192||ticket.Text.Any(char.IsControl)||
                            before.Images!=0||!(before.Empty||before.SuggestionCandidate))throw new IOException("Native input changed");
                        if(before.SuggestionCandidate)CheckPlaceholder(input,screen,before,BeforeWrite);
                        BeforeWrite();
                        var current=Snapshot(screen);
                        if(current.Target!=before.Target||!current.Empty&&current!=before)throw new IOException("Native input changed");
                        Write(input,ticket.Text.SelectMany(c=>new[]{Key(c,0,0,true),Key(c,0,0,false)}).ToArray());
                    }else if(ticket.Action is "foreground-send" or "foreground-verify"){
                        if(ticket.ParentUser is null&&native.State!="idle"||before.Empty||before.SuggestionCandidate||before.Images!=0)throw new IOException("Native input changed");
                        if(before.Text!=ticket.Text){
                            if(!before.HasHiddenSpace(ticket.Text))throw new IOException("Native input changed");
                            VerifyWrappedSpace(input,screen,before,ticket.Text,BeforeWrite);
                        }
                        BeforeWrite();
                        if(Snapshot(screen)!=before)throw new IOException("Native input changed");
                        if(ticket.Action=="foreground-send")Write(input,[Key('\r',0x0D,0,true),Key('\r',0x0D,0,false)]);
                    }else if(ticket.Action=="foreground-interrupt"){
                        if(ticket.ParentUser is null||!before.Empty&&!before.SuggestionCandidate||before.Images!=0)throw new IOException("Native input changed");
                        if(before.SuggestionCandidate)CheckPlaceholder(input,screen,before,BeforeWrite);
                        BeforeWrite();
                        var current=Snapshot(screen);
                        if(current!=before&&(!current.Empty||current.Target!=before.Target))throw new IOException("Native input changed");
                        Write(input,[Key((char)27,0x1B,0,true),Key((char)27,0x1B,0,false)]);
                    }else if(ticket.Action=="type") {
                        if(ticket.Text.Any(char.IsControl)||ticket.Text.Length>2000||before.Text.Length!=0)throw new IOException("Native input unsupported");
                        var events=ticket.Text.SelectMany(c=>new[]{Key(c,0,0,true),Key(c,0,0,false)}).ToArray();Write(input,events);
                    } else if(ticket.Action=="image") {
                        stage="image_verify";
                        if(ticket.Path is null||ticket.Digest is null||before.Images>=3||ClaudeNativeContent.FileDigest(ticket.Path)!=ticket.Digest)throw new IOException("Native image changed");
                        stage="clipboard_prepare";
                        using var lease=new ClipboardImage(ticket.Path);
                        BeforeWrite();lease.CheckOwnership();stage="paste_key";
                        Write(input,[Key('v',0x56,2,true),Key('v',0x56,2,false)]);
                        stage="paste_observe";
                        value=WaitForPaste(before,()=>Snapshot(screen),()=>{Ready();lease.CheckOwnership();},
                            ()=>Environment.TickCount64,Thread.Sleep);
                        stage="clipboard_restore";
                    } else if(ticket.Action is "send" or "queue") {
                        if(ticket.Action=="send"&&native.State!="idle"||ticket.Action=="queue"&&native.State is not ("busy" or "working"))throw new IOException("Native turn changed");
                        // The full draft has already been read back from the
                        // original raw-mode Claude console before this chord.
                        Write(input,ticket.Action=="send"?[Key((char)24,0x58,8,true),Key((char)24,0x58,8,false),Key((char)19,0x53,8,true),Key((char)19,0x53,8,false)]:
                            [Key((char)24,0x58,8,true),Key((char)24,0x58,8,false),Key('\r',0x0D,0,true),Key('\r',0x0D,0,false)]);
                    } else if(ticket.Action=="stop") {
                        if(native.State is not ("busy" or "working"))throw new IOException("Native turn changed");
                        Write(input,[Key((char)3,0x43,8,true),Key((char)3,0x43,8,false)]);
                    } else throw new IOException("Native action unsupported");
                }
                // Mutation acknowledgement is not delivery; the parent always
                // confirms via the original native transcript / settled state.
                output.Write(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value)+"\n"));
            }finally{FreeConsole();}
        }catch(Exception e) when(e is IOException or UnauthorizedAccessException or JsonException or ArgumentException or InvalidOperationException or Win32Exception or ExternalException){
            string reason=e.Message switch{
                "Native composer boundary unavailable"=>"composer_boundary",
                "Native multiline draft unavailable"=>"multiline_draft",
                "Native prompt unavailable"=>"prompt",
                "Native console layout unavailable"=>"layout",
                "Clipboard ownership changed"=>"clipboard_changed",
                "Clipboard format cannot be preserved"=>"clipboard_format",
                "Native write expired"=>"expired",
                "Native paste target changed"=>"paste_target",
                "Native paste text changed"=>"paste_draft",
                "Native paste acknowledgement timed out"=>"paste_timeout",
                "Native keybindings changed"=>"keybindings",
                "Native snapshot changed"=>"snapshot",
                "Native modifier active"=>"modifier",
                "Native turn changed"=>"turn",
                _=>stage
            };
            output.Write(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new{error="native_console_"+reason+"_unconfirmed"})+"\n"));Environment.ExitCode=1;
        }
    }
    private static void VerifyWrappedSpace(SafeFileHandle input,SafeFileHandle screen,ClaudeNativeTerminal.View before,string expected,Action ready){
        if(!GetConsoleScreenBufferInfo(screen,out var info))throw new IOException("Native console layout unavailable");
        var end=info.Cursor;
        if(end.X!=before.CursorColumn||Snapshot(screen)!=before)throw new IOException("Native input changed");
        int distance=expected.Length-1;
        void Move(ushort key){
            ready();
            Write(input,Enumerable.Range(0,distance).SelectMany(_=>new[]{Key('\0',key,0x100,true),Key('\0',key,0x100,false)}).ToArray());
        }
        bool At(int x,int y){
            for(int i=0;i<40;i++){
                if(!GetConsoleScreenBufferInfo(screen,out var current))break;
                if(current.Cursor.X==x&&current.Cursor.Y==y)return true;
                Thread.Sleep(25);
            }
            return false;
        }
        // In this two-row projection every glyph except the boundary space
        // matches. From the end, N-1 Lefts must leave the caret immediately
        // after the first ASCII glyph. A missing space takes it to column 2.
        Move(0x25);
        bool lengthMatches=At(3,end.Y-1);
        Move(0x27);
        if(!At(end.X,end.Y)||Snapshot(screen)!=before||!lengthMatches)throw new IOException("Native input changed");
    }
    private static void CheckPlaceholder(SafeFileHandle input,SafeFileHandle screen,ClaudeNativeTerminal.View original,Action ready){
        // Determine whether the faint suffix is a suggestion or actual text at
        // the start of a draft. Undo only our one proven insertion.
        ready();if(Snapshot(screen)!=original)throw new IOException("Native input changed");
        Write(input,[Key('z',0x5A,0,true),Key('z',0x5A,0,false)]);
        var deadline=Environment.TickCount64+900;ClaudeNativeTerminal.View changed;
        do{changed=Snapshot(screen);if(changed!=original)break;Thread.Sleep(25);}while(Environment.TickCount64<deadline);
        if(changed.Target!=original.Target||changed.Images!=0||changed.CursorColumn!=3||
            changed.Text!="z"&&changed.Text!="z"+original.Text)throw new IOException("Native input changed");
        ready();if(Snapshot(screen)!=changed)throw new IOException("Native input changed");
        Write(input,[Key('\b',0x08,0,true),Key('\b',0x08,0,false)]);
        deadline=Environment.TickCount64+900;
        do{
            var restored=Snapshot(screen);
            if(restored==original||restored.Target==original.Target&&restored.Empty){
                if(changed.Text=="z")return;
                break;
            }
            Thread.Sleep(25);
        }while(Environment.TickCount64<deadline);
        throw new IOException("Native input changed");
    }
    internal static ClaudeNativeTerminal.View WaitForPaste(ClaudeNativeTerminal.View before,
        Func<ClaudeNativeTerminal.View> observe,Action verify,Func<long> clock,Action<int> wait){
        // Windows Claude Code can start two PowerShell processes in sequence to
        // check and read the image. Hold the lease until the chip appears, even
        // if a screen refresh temporarily hides the prompt or its lower border.
        long deadline=clock()+20000;
        while(clock()<deadline){
            verify();
            ClaudeNativeTerminal.View current;
            try{current=observe();}
            catch(IOException ex) when(ex.Message is "Native prompt unavailable" or "Native composer boundary unavailable"){
                wait(80);continue;
            }
            if(current.Target!=before.Target)throw new IOException("Native paste target changed");
            if(current.Text!=before.Text||current.Images<before.Images||current.Images>before.Images+1)
                throw new IOException("Native paste text changed");
            if(current.Images==before.Images+1){verify();return current;}
            wait(80);
        }
        throw new IOException("Native paste acknowledgement timed out");
    }
    private static ClaudeNativeTerminal.View Snapshot(SafeFileHandle handle) {
        if(!GetConsoleScreenBufferInfo(handle,out var info)||info.Size.X is <20 or >600)throw new IOException("Native console layout unavailable");
        int width=info.Size.X;short top=(short)Math.Max(info.Window.Top,info.Cursor.Y-24),bottom=(short)Math.Min(info.Window.Bottom,info.Cursor.Y+2);int height=bottom-top+1;
        var cells=new Cell[width*height];var rect=new Rect{Left=0,Top=top,Right=(short)(width-1),Bottom=bottom};
        if(!ReadConsoleOutputW(handle,cells,new(){X=(short)width,Y=(short)height},new(),ref rect))throw new IOException("Native console read unavailable");
        string Row(int y){var text=new StringBuilder();for(int x=0;x<width;x++){var c=cells[(y-top)*width+x];if((c.Attributes&0x200)==0)text.Append(c.Character=='\0'?' ':c.Character);}return text.ToString();}
        bool Dim(int y,int len)=>Enumerable.Range(2,Math.Min(len,width-2)).All(x=>char.IsWhiteSpace(cells[(y-top)*width+x].Character)||(cells[(y-top)*width+x].Attributes&15)==8);
        return ParseRows(Row,Dim,top,bottom,info.Cursor.Y,info.Cursor.X,GetConsoleWindow()+":"+width);
    }
    internal static ClaudeNativeTerminal.View ParseRows(Func<int,string> Row,Func<int,int,bool> gray,int top,int bottom,int cursorY,int cursorX,string target){
        static bool Divider(string row){string v=row.TrimEnd();return v.Length>=8&&v.All(c=>c is '─' or '━' or ' ');}
        int first=cursorY;
        while(first>=top){string row=Row(first);if(row.Length>=2&&row[0] is '>' or '❯'&&char.IsWhiteSpace(row[1]))break;first--;}
        if(first<=top)throw new IOException("Native prompt unavailable");
        string borderAbove=Row(first-1).TrimEnd();
        bool statusInBorder=borderAbove.StartsWith(new string('─',24),StringComparison.Ordinal)&&borderAbove.EndsWith('─')&&borderAbove.Count(c=>c=='─')>borderAbove.Length*0.6;
        if(!Divider(borderAbove)&&!statusInBorder)throw new IOException("Native prompt unavailable");
        var rows=new List<string>();for(int y=first;y<=cursorY;y++){string row=Row(y);rows.Add(row.Length>=2?row[2..].TrimEnd():"");}
        string value=string.Concat(rows);
        // A suggestion and a user's draft can have identical text. Never infer
        // an empty composer merely from the English "Try" prefix.
        // A caret at the start of a draft is not proof that subsequent rows
        // are empty. Only the bounded, fully observed composer is supported.
        bool border=false;
        for(int y=cursorY+1;y<=bottom;y++){
            string following=Row(y).TrimEnd();
            if(Divider(following)){border=true;break;}
            if(following.Length>0)throw new IOException("Native multiline draft unavailable");
        }
        if(!border)throw new IOException("Native composer boundary unavailable");
        int images=Regex.Matches(value,@"\[Image #\d+\]").Count;
        string text=images>0?Regex.Replace(value,@"\[Image #\d+\] ?",""):value;
        bool candidate=first==cursorY&&cursorX==2&&images==0&&text.StartsWith("Try ",StringComparison.Ordinal);
        return new(text,images,target,text.Length==0&&images==0&&first==cursorY&&cursorX==2,cursorX,candidate&&gray(first,value.Length),candidate,string.Join('\n',rows));
    }
    private sealed class ClipboardImage:IDisposable {
        private readonly DataObject _previous=new();
        private readonly List<IDisposable> _owned=[];
        private uint _sequence;
        private bool _set;

        internal ClipboardImage(string path) {
            uint originalSequence=GetClipboardSequenceNumber();
            try{
                var previous=Clipboard.GetDataObject();
                var formats=previous?.GetFormats(false)??[];
                if(formats.Length>64)throw new IOException("Clipboard unavailable");
                foreach(string format in formats){
                    var copy=previous!.GetData(format,false) switch{
                        byte[] bytes when bytes.Length<=20_000_000=>(object)bytes.ToArray(),
                        MemoryStream stream when stream.Length<=20_000_000=>new MemoryStream(stream.ToArray()),
                        Image image=>new Bitmap(image),
                        string text when text.Length<=2_000_000=>text,
                        string[] files=>files.ToArray(),
                        System.Collections.Specialized.StringCollection list=>list.Cast<string>().ToArray(),
                        int number=>number,
                        bool flag=>flag,
                        _=>throw new IOException("Clipboard format cannot be preserved")
                    };
                    if(copy is IDisposable owned)_owned.Add(owned);
                    _previous.SetData(format,false,copy);
                }
                using var source=Image.FromFile(path);
                using var bitmap=new Bitmap(source);
                if(originalSequence!=GetClipboardSequenceNumber())throw new IOException("Clipboard ownership changed");
                Clipboard.SetImage(bitmap);
                _sequence=GetClipboardSequenceNumber();
                _set=true;
            }catch{
                foreach(var owned in _owned)owned.Dispose();
                throw;
            }
        }
        internal void CheckOwnership(){if(!_set||GetClipboardSequenceNumber()!=_sequence)throw new IOException("Clipboard ownership changed");}
        public void Dispose(){
            try{
                if(_set&&GetClipboardSequenceNumber()==_sequence)Clipboard.SetDataObject(_previous,true);
            }finally{
                foreach(var owned in _owned)owned.Dispose();
            }
        }
    }
    private static void Write(SafeFileHandle input,InputRecord[] keys){if(keys.Length>0&&(!WriteConsoleInputW(input,keys,(uint)keys.Length,out uint written)||written!=keys.Length))throw new IOException("Native input uncertain");}
    private static InputRecord Key(char character,ushort vk,uint flags,bool down)=>new(){Type=1,Down=down?1:0,Repeat=1,Vk=vk,Character=character,Flags=flags};
    private static int Integrity(uint pid){
        nint p=OpenProcess(0x1000,false,pid);if(p==0)throw new IOException("Native process unavailable");
        try{if(!OpenProcessToken(p,8,out var token))throw new IOException("Native token unavailable");try{GetTokenInformation(token,25,0,0,out uint size);nint b=Marshal.AllocHGlobal((int)size);try{if(!GetTokenInformation(token,25,b,size,out _))throw new IOException("Native token unavailable");nint sid=Marshal.ReadIntPtr(b);return Marshal.ReadInt32(GetSidSubAuthority(sid,(uint)(Marshal.ReadByte(GetSidSubAuthorityCount(sid))-1)));}finally{Marshal.FreeHGlobal(b);}}finally{CloseHandle(token);}}finally{CloseHandle(p);}
    }
    [StructLayout(LayoutKind.Sequential)]private struct Coord{public short X,Y;}
    [StructLayout(LayoutKind.Sequential)]private struct Rect{public short Left,Top,Right,Bottom;}
    [StructLayout(LayoutKind.Sequential)]private struct Info{public Coord Size,Cursor;public ushort Attributes;public Rect Window;public Coord Maximum;}
    [StructLayout(LayoutKind.Explicit,CharSet=CharSet.Unicode)]private struct Cell{[FieldOffset(0)]public char Character;[FieldOffset(2)]public ushort Attributes;}
    [StructLayout(LayoutKind.Explicit,Size=20,CharSet=CharSet.Unicode)]private struct InputRecord{[FieldOffset(0)]public ushort Type;[FieldOffset(4)]public int Down;[FieldOffset(8)]public ushort Repeat;[FieldOffset(10)]public ushort Vk;[FieldOffset(12)]public ushort Scan;[FieldOffset(14)]public char Character;[FieldOffset(16)]public uint Flags;}
    [DllImport("kernel32.dll")]private static extern bool FreeConsole();
    [DllImport("kernel32.dll",SetLastError=true)]private static extern bool AttachConsole(uint pid);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]private static extern SafeFileHandle CreateFileW(string path,uint access,uint share,nint security,uint creation,uint flags,nint template);
    [DllImport("kernel32.dll")]private static extern bool GetConsoleMode(SafeFileHandle h,out uint mode);
    [DllImport("kernel32.dll")]private static extern bool GetConsoleScreenBufferInfo(SafeFileHandle h,out Info info);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode)]private static extern bool ReadConsoleOutputW(SafeFileHandle h,[Out] Cell[] cells,Coord size,Coord start,ref Rect area);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode)]private static extern bool WriteConsoleInputW(SafeFileHandle h,InputRecord[] records,uint count,out uint written);
    [DllImport("kernel32.dll")]private static extern uint GetConsoleProcessList(uint[] pids,uint count);
    [DllImport("kernel32.dll")]private static extern nint GetConsoleWindow();
    [DllImport("user32.dll")]private static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")]private static extern uint GetClipboardSequenceNumber();
    [DllImport("kernel32.dll")]private static extern nint OpenProcess(uint access,bool inherit,uint pid);
    [DllImport("kernel32.dll")]private static extern bool CloseHandle(nint h);
    [DllImport("advapi32.dll")]private static extern bool OpenProcessToken(nint p,uint access,out nint token);
    [DllImport("advapi32.dll")]private static extern bool GetTokenInformation(nint token,int kind,nint info,uint length,out uint needed);
    [DllImport("advapi32.dll")]private static extern nint GetSidSubAuthorityCount(nint sid);
    [DllImport("advapi32.dll")]private static extern nint GetSidSubAuthority(nint sid,uint index);
}
