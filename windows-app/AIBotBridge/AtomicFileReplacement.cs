namespace AIBotBridge;

internal static class AtomicFileReplacement
{
    // A scanner or another reader can briefly prevent a Windows rename. Keep
    // the old file intact and retry only access/sharing errors, for at most 310ms.
    internal static void Move(string temporary,string destination)
    {
        for(int attempt=0;;attempt++)
        {
            try { File.Move(temporary,destination,true);return; }
            catch(Exception ex) when(attempt<5 && ex is IOException or UnauthorizedAccessException
                && (ex.HResult&0xffff) is 5 or 32 or 33)
            { Thread.Sleep(10<<attempt); }
        }
    }

    internal static void SelfTest(string directory)
    {
        Directory.CreateDirectory(directory);
        string target=Path.Combine(directory,"record.dat"),temporary=target+".tmp";
        File.WriteAllText(target,"old fixture");File.WriteAllText(temporary,"new fixture");
        using(var held=new FileStream(target,FileMode.Open,FileAccess.Read,FileShare.Read))
        {
            var release=Task.Run(async()=>{await Task.Delay(70);held.Dispose();});
            Move(temporary,target);release.GetAwaiter().GetResult();
        }
        if(File.ReadAllText(target)!="new fixture"||File.Exists(temporary))throw new InvalidOperationException("Transient rename did not recover atomically.");
        File.WriteAllText(temporary,"next fixture");bool failed=false;var clock=System.Diagnostics.Stopwatch.StartNew();
        using(var held=new FileStream(target,FileMode.Open,FileAccess.Read,FileShare.Read))
        {
            try {Move(temporary,target);}
            catch(Exception ex) when(ex is IOException or UnauthorizedAccessException){failed=true;}
            if(!failed||clock.ElapsedMilliseconds>2000||File.ReadAllText(target)!="new fixture"||File.ReadAllText(temporary)!="next fixture")
                throw new InvalidOperationException("Permanent rename failure lost data or exceeded its bound.");
        }
        Move(temporary,target);
        if(File.ReadAllText(target)!="next fixture")throw new InvalidOperationException("Explicit retry after releasing a file failed.");
        bool missing=false;
        try {Move(temporary,target);}catch(FileNotFoundException){missing=true;}
        if(!missing||File.ReadAllText(target)!="next fixture")throw new InvalidOperationException("Missing input overwrote an existing record.");
        Console.WriteLine("ATOMIC_REPLACEMENT_OK real reader contention, bounded permanent failure, original/temp preservation and explicit recovery");
    }
}
