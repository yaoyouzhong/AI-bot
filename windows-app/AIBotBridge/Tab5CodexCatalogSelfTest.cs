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
            Require(Read(true).Count==522&&Read(false).Count==8,"confirmation unbounded, recent bounded");
            Require(Tab5CodexCatalog.ReadCatalog(true,current,[],new Dictionary<string,Tab5CodexCatalog.Activity>()).Count==522,"confirmation independent of saved projects");
            Require(Tab5CodexCatalog.ReadCatalog(true,current,[],new Dictionary<string,Tab5CodexCatalog.Activity>{{live,new(1,true)}}).Count==522,"archived duplicate retained for identity");
            string longId=Guid.NewGuid().ToString(),longTitle=new('x',1500);Execute(current,Row(longId,longTitle));
            Require(Read(true).Single(t=>t.Id==longId).IdentityTitle==longTitle,"full identity title preserved");
            Execute(current,"DROP TABLE local_thread_catalog;");
            Require(Read(true).Count==0,"schema failure cannot fallback");
            File.WriteAllText(current,"damaged current catalog");
            Require(Read(true).Count==0&&Read(false).Count==0,"corrupt current cannot fallback");
            Console.WriteLine("Catalog migration self-test passed (10 groups).");
        }finally{Directory.Delete(home,true);}
    }
    [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)]private static extern int sqlite3_open_v2([MarshalAs(UnmanagedType.LPUTF8Str)]string path,out nint db,int flags,nint vfs);
    [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)]private static extern int sqlite3_exec(nint db,[MarshalAs(UnmanagedType.LPUTF8Str)]string sql,nint callback,nint arg,nint error);
    [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)]private static extern int sqlite3_close(nint db);
}
