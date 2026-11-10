using System.Runtime.InteropServices;

namespace AIBotBridge;

internal static class Tab5CodexCatalogSelfTest
{
    internal static void Run() {
        string home=Path.Combine(Path.GetTempPath(),"aibot-catalog-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(home,"sqlite"));
        string current=Path.Combine(home,"sqlite","codex.db"),legacy=Path.Combine(home,"sqlite","codex-dev.db");
        void Require(bool ok,string message){if(!ok)throw new InvalidOperationException("Catalog: "+message);}
        void Execute(string path,string sql) {
            nint db=0;try {
                Require(sqlite3_open_v2(path,out db,6,0)==0,"fixture open");
                Require(sqlite3_exec(db,sql,0,0,0)==0,"fixture SQL");
            }finally{if(db!=0)sqlite3_close(db);}
        }
        const string schema="CREATE TABLE local_thread_catalog(thread_id TEXT,display_title TEXT,cwd TEXT,source_updated_at INTEGER,host_id TEXT,missing_candidate INTEGER);";
        string Row(string id,string title,string host="local",int time=1)=>$"INSERT INTO local_thread_catalog VALUES('{id}','{title}','{home.Replace("'","''")}',{time},'{host}',0);";
        IReadOnlyList<Tab5CodexTask> Read(bool confirmation)=>Tab5CodexCatalog.ReadCatalog(confirmation,Tab5CodexCatalog.CatalogPath(home),[new("p","Test",home)],new Dictionary<string,Tab5CodexCatalog.Activity>());
        try {
            string old=Guid.NewGuid().ToString(),live=Guid.NewGuid().ToString();
            Execute(legacy,schema+Row(old,"Old"));
            Require(Tab5CodexCatalog.CatalogPath(home)==legacy&&Read(true).Single().Id==old,"missing current uses legacy");
            Execute(current,schema+Row(live,"Current"));
            Require(Read(true).Single().Id==live&&Read(false).Single().Id==live,"both readers use current");
            Require(Read(true).All(t=>t.Id!=old),"missing target cannot reuse old identity");
            Execute(current,Row(Guid.NewGuid().ToString(),"Current","remote",2));
            Require(Tab5CodexComposer.UniqueTitle(live,Read(true)) is null,"remote duplicate must block title confirmation");
            Execute(current,string.Concat(Enumerable.Range(0,520).Select(i=>Row(Guid.NewGuid().ToString(),"Long "+i,time:100+i))));
            Require(Read(true).Count==522&&Read(false).Count==64,"confirmation unbounded, recent bounded");
            Require(Tab5CodexCatalog.ReadCatalog(true,current,[],new Dictionary<string,Tab5CodexCatalog.Activity>()).Count==522,"confirmation independent of saved projects");
            Require(Tab5CodexCatalog.ReadCatalog(true,current,[],new Dictionary<string,Tab5CodexCatalog.Activity>{{live,new(1,true)}}).Count==522,"archived duplicate retained for identity");
            string longId=Guid.NewGuid().ToString(),longTitle=new('x',1500);Execute(current,Row(longId,longTitle));
            Require(Read(true).Single(t=>t.Id==longId).IdentityTitle==longTitle,"full identity title preserved");
            Execute(current,"DROP TABLE local_thread_catalog;");
            Require(Read(true).Count==0,"schema failure cannot fallback");
            long tick=0;var display=new Tab5CatalogDisplay<string>(()=>tick);
            display.Observe(["session"]);
            Require(display.Observe(null).Items.Count==1&&!display.Observe(null).Fresh,"failed scan preserves display without freshness");
            tick=1000;Require(display.Observe([]).Items.Count==1,"brief empty read preserves entry");
            tick=9000;Require(display.Observe([]).Items.Count==0,"confirmed empty eventually clears");
            display.Observe(["recovered"]);Require(display.Observe(null).Items.Single()=="recovered","recovery replaces old catalog");
            tick=39000;Require(display.Observe(null).Items.Count==0,"repeated faults do not retain obsolete entries indefinitely");
            display.Observe(["active"]);display.Clear();Require(display.Observe(null).Items.Count==0,"disabled catalog has no retained projection");
            File.WriteAllText(current,"damaged current catalog");
            Require(Read(true).Count==0&&Read(false).Count==0,"corrupt current cannot fallback");
            Require(Tab5CodexCatalog.TryReadCatalog(false,current,[new("p","Test",home)],new Dictionary<string,Tab5CodexCatalog.Activity>()) is null,"storage failure distinguishable from confirmed empty");
            string main=Path.Combine(home,"Main","AI-bot"),linked=Path.Combine(home,"Trees","different-name"),other=Path.Combine(home,"Other","AI-bot");
            string shared=Path.Combine(main,".git"),gitdir=Path.Combine(shared,"worktrees","case");
            Directory.CreateDirectory(gitdir);Directory.CreateDirectory(linked);Directory.CreateDirectory(Path.Combine(other,".git"));
            string dot=Path.Combine(linked,".git");
            File.WriteAllText(dot,"gitdir: "+gitdir+"\n");File.WriteAllText(Path.Combine(gitdir,"commondir"),"../..\n");File.WriteAllText(Path.Combine(gitdir,"gitdir"),dot+"\n");
            Tab5SavedProject[] roots=[new("main","AI-bot",main),new("linked","different-name",linked),new("other","AI-bot",other)];
            var a=Tab5CodexCatalog.MatchProject(main,roots);var b=Tab5CodexCatalog.MatchProject(Path.Combine(linked,"src"),roots);
            var c=Tab5CodexCatalog.MatchProject(other,roots);
            Require(a?.Id=="main"&&b?.Id==a.Id&&c?.Id=="other"&&c.Name!=a.Name,"verified worktree merges, separate same-name repo keeps distinct label");
            File.WriteAllText(Path.Combine(gitdir,"gitdir"),Path.Combine(other,".git")+"\n");
            Require(Tab5CodexCatalog.MatchProject(linked,roots)?.Id=="linked","bad backlink cannot merge configured worktree");
            Require(Tab5CodexCatalog.MatchProject(linked,[roots[0]]) is null,"unregistered invalid worktree cannot enter saved catalog");
            Console.WriteLine("Catalog migration, project identity and display stability self-tests passed.");
        }finally{Directory.Delete(home,true);}
    }
    [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)]private static extern int sqlite3_open_v2([MarshalAs(UnmanagedType.LPUTF8Str)]string path,out nint db,int flags,nint vfs);
    [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)]private static extern int sqlite3_exec(nint db,[MarshalAs(UnmanagedType.LPUTF8Str)]string sql,nint callback,nint arg,nint error);
    [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)]private static extern int sqlite3_close(nint db);
}
