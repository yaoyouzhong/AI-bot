namespace AIBotBridge;

internal static class Tab5DoubaoVoiceSelfTest
{
    internal static void Run() {
        Tab5DoubaoMicrophoneSelfTest.RunAsync().GetAwaiter().GetResult();
        static void Check(bool value){if(!value)throw new InvalidOperationException("Doubao control state test failed");}
        var hashes=new Dictionary<string,string>{
            ["rpc.dll"]="0be0cb35d864d06b2c8b5267d9f0669a1383493f557a45c6a1ebbfa203e85e53",
            ["tsf-oime-core.dll"]="8544bfb87d8d2cc847b13e2ccc9bbd2220b20bceafdd1fc88ad5eaff5a28bb02",
            ["ImeService.exe"]="94ace7e504e6aa70c15095d5219604aee93e17247eb85429a046c7a4fdb95e90"};
        Check(Tab5DoubaoVoice.MatchesBuild(name=>hashes[name]));
        hashes["rpc.dll"]="A3EAD1A55850257BAC01A878C899F42291A1F41BD2B834E0CAA5C5B66A674E02";
        Check(!Tab5DoubaoVoice.MatchesBuild(name=>hashes[name])); // mixed versions must fail closed
        Check(!Tab5DoubaoVoice.MatchesBuild(_=>null));
        int state=0,starts=0,stops=0,shows=0;bool focus=true,queryLost=false,rejectStart=false;
        int Send(int message) {
            if(message==Tab5DoubaoVoice.Query)return queryLost&&starts>0?-1:state;
            if(message==Tab5DoubaoVoice.StartCapture){starts++;if(rejectStart)return -1;state=1;return 0;}
            if(message==Tab5DoubaoVoice.StopCapture){stops++;state=3;return 0;}
            if(message==Tab5DoubaoVoice.ShowCapture){Check(starts>0&&(state&1)!=0);shows++;return 0;}
            throw new InvalidOperationException("Unexpected IME command");
        }
        var voice=new Tab5DoubaoVoice(Send,()=>focus);
        state=1;Check(!voice.CanRestoreFocus);state=0; // never reclaim an unowned take
        focus=false;Check(!voice.Start()&&starts==0);focus=true;
        foreach(int existing in new[]{1,2,4,8,16,-1}) {
            state=existing;
            try{voice.Start();throw new Exception("Must refuse busy/unknown IME state");}catch(InvalidOperationException){}
            Check(starts==0&&stops==0);
        }
        state=0;Check(voice.Start()&&starts==1&&shows==1);
        focus=false;Check(voice.CanRestoreFocus&&starts==1&&stops==0);
        queryLost=true;Check(!voice.CanRestoreFocus);queryLost=false;
        state=0;Check(!voice.CanRestoreFocus);state=1;
        focus=false;Check(!voice.Stop()&&stops==0);focus=true;
        Check(voice.Stop()&&stops==1);Check(!voice.Stop()&&stops==1); // never turn a duplicate stop into a start
        Check(!voice.CanRestoreFocus); // pending stop must not reclaim focus
        Check(!voice.CaptureStopped);state=2;Check(!voice.CaptureStopped);
        state=0;Check(voice.CaptureStopped); // delayed native worker confirmation
        state=0;Check(voice.Start());state=0;Check(voice.Stop()&&stops==1); // user already stopped
        state=0;rejectStart=true;
        try{voice.Start();throw new Exception("Rejected start reported ready");}catch(InvalidOperationException){}
        rejectStart=false;queryLost=true;state=0;starts=0;
        try{voice.Start();throw new Exception("Negative query reported ready");}catch(InvalidOperationException){}
        Check(stops==2&&state==3); // accepted start, lost confirmation: one explicit cleanup
        queryLost=false;state=0;Check(voice.Start());focus=false;state=2;
        Check(!voice.CaptureStopped);state=0;
        Check(voice.CaptureStopped&&stops==2); // external stop observed without toggling or stealing focus
        state=1;focus=false;Check(!voice.Stop()&&stops==2); // no ownership: never stop somebody else's capture
        Console.WriteLine("TAB5_DOUBAO_CONTROL_OK focus, occupied/unknown state, explicit start/stop, duplicate stop and uncertain-start cleanup; synthetic only");
    }
}
