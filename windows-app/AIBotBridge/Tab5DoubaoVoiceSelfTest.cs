namespace AIBotBridge;

internal static class Tab5DoubaoVoiceSelfTest
{
    internal static void Run() {
        Tab5DoubaoMicrophoneSelfTest.RunAsync().GetAwaiter().GetResult();
        static void Check(bool value){if(!value)throw new InvalidOperationException("Doubao control state test failed");}
        int state=0,starts=0,stops=0,shows=0;bool focus=true,queryLost=false,rejectStart=false;
        int Send(int message) {
            if(message==Tab5DoubaoVoice.Query)return queryLost&&starts>0?-1:state;
            if(message==Tab5DoubaoVoice.StartCapture){starts++;if(rejectStart)return -1;state=1;return 0;}
            if(message==Tab5DoubaoVoice.StopCapture){stops++;state=3;return 0;}
            if(message==Tab5DoubaoVoice.ShowCapture){Check(starts>0&&(state&1)!=0);shows++;return 0;}
            throw new InvalidOperationException("Unexpected IME command");
        }
        var voice=new Tab5DoubaoVoice(Send,()=>focus);
        focus=false;Check(!voice.Start()&&starts==0);focus=true;
        foreach(int existing in new[]{1,2,4,8,16,-1}) {
            state=existing;
            try{voice.Start();throw new Exception("Must refuse busy/unknown IME state");}catch(InvalidOperationException){}
            Check(starts==0&&stops==0);
        }
        state=0;Check(voice.Start()&&starts==1&&shows==1);
        focus=false;Check(!voice.Stop()&&stops==0);focus=true;
        Check(voice.Stop()&&stops==1);Check(!voice.Stop()&&stops==1); // never turn a duplicate stop into a start
        Check(!voice.CaptureStopped);state=2;Check(!voice.CaptureStopped);
        state=0;Check(voice.CaptureStopped); // delayed native worker confirmation
        state=0;Check(voice.Start());state=0;Check(voice.Stop()&&stops==1); // user already stopped
        state=0;rejectStart=true;
        try{voice.Start();throw new Exception("Rejected start reported ready");}catch(InvalidOperationException){}
        rejectStart=false;queryLost=true;state=0;starts=0;
        try{voice.Start();throw new Exception("Negative query reported ready");}catch(InvalidOperationException){}
        Check(stops==2&&state==3); // accepted start, lost confirmation: one explicit cleanup
        Console.WriteLine("TAB5_DOUBAO_CONTROL_OK focus, occupied/unknown state, explicit start/stop, duplicate stop and uncertain-start cleanup; synthetic only");
    }
}
