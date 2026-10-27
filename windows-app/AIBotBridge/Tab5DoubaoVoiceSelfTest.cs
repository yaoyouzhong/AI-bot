namespace AIBotBridge;

internal static class Tab5DoubaoVoiceSelfTest
{
    internal static void Run() {
        CheckIdleAfterFocusLoss();
        CheckOwnedStopAfterFocusLoss();
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
        state=1;Check(!voice.CanRestoreFocus);state=0; // never reclaim an unowned take
        focus=false;Check(!voice.Start()&&starts==0);focus=true;
        foreach(int existing in new[]{1,2,4,8,16,19,32,64,-1}) {
            state=existing;
            Check(!voice.IsIdle);
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
    private static void CheckIdleAfterFocusLoss() {
        int state=0,stops=0;bool focus=true;
        int Send(int command) {
            if(command==Tab5DoubaoVoice.Query)return state;
            if(command==Tab5DoubaoVoice.StartCapture){state=1;return 0;}
            if(command==Tab5DoubaoVoice.ShowCapture)return 0;
            if(command==Tab5DoubaoVoice.StopCapture){stops++;state=0;return 0;}
            throw new InvalidOperationException("Unexpected command");
        }
        var voice=new Tab5DoubaoVoice(Send,()=>focus);voice.Start();
        // A focus transition may make the IME stop itself before our timer runs.
        focus=false;state=0;
        if(!voice.Stop()||!voice.CaptureStopped||stops!=0)
            throw new InvalidOperationException("DOUBAO_IDLE_FOCUS_REGRESSION: acknowledged idle must complete without focus or another stop command");
    }
    private static void CheckOwnedStopAfterFocusLoss() {
        foreach(bool unchanged in new[]{true,false}) {
            int state=0,stops=0;bool focus=true;
            int Send(int command) {
                if(command==Tab5DoubaoVoice.Query)return state;
                if(command==Tab5DoubaoVoice.StartCapture){state=1;return 0;}
                if(command==Tab5DoubaoVoice.ShowCapture)return 0;
                if(command==Tab5DoubaoVoice.StopCapture){stops++;state=2;return 0;}
                throw new InvalidOperationException("Unexpected command");
            }
            var voice=new Tab5DoubaoVoice(Send,()=>focus,()=>unchanged);
            if(voice.Stop()||stops!=0)throw new InvalidOperationException("Unowned capture was stopped");
            voice.Start();focus=false;
            if(voice.Stop()!=unchanged||stops!=(unchanged?1:0))throw new InvalidOperationException("Owned stop did not respect desktop input boundary");
            if(unchanged) {
                if(voice.Stop()||stops!=1||voice.CaptureStopped)throw new InvalidOperationException("Duplicate stop or premature completion");
                state=0;if(!voice.CaptureStopped)throw new InvalidOperationException("Final idle not observed");
            }
        }
    }
}
