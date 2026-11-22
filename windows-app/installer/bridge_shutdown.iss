{ The bridge names its session-local mutexes and exit event with the current
  user's SID. Use the setup process token, as the tray does. }
function OpenProcessToken(Process: THandle; Access: Cardinal; var Token: THandle): BOOL;
  external 'OpenProcessToken@advapi32.dll stdcall setuponly';
function GetTokenInformation(Token: THandle; InfoClass: Integer; Buffer: THandle;
  Capacity: Cardinal; var Used: Cardinal): BOOL;
  external 'GetTokenInformation@advapi32.dll stdcall setuponly';
function ConvertSidToStringSid(Sid: THandle; var Text: THandle): BOOL;
  external 'ConvertSidToStringSidW@advapi32.dll stdcall setuponly';
function SidTextLength(Text: THandle): Integer;
  external 'lstrlenW@kernel32.dll stdcall setuponly';
function CopySidText(Destination: String; Source: THandle; Capacity: Integer): THandle;
  external 'lstrcpynW@kernel32.dll stdcall setuponly';
function AllocateTokenBuffer(Flags: Cardinal; Size: Cardinal): THandle;
  external 'LocalAlloc@kernel32.dll stdcall setuponly';
procedure ReadSidPointer(var Sid: THandle; Buffer: THandle; Count: Cardinal);
  external 'RtlMoveMemory@kernel32.dll stdcall setuponly';
function FreeBuffer(Buffer: THandle): THandle;
  external 'LocalFree@kernel32.dll stdcall setuponly';
function CloseKernelHandle(Handle: THandle): BOOL;
  external 'CloseHandle@kernel32.dll stdcall setuponly';
function OpenBridgeMutex(Access: Cardinal; Inherit: BOOL; Name: String): THandle;
  external 'OpenMutexW@kernel32.dll stdcall setuponly';
function OpenExitEvent(Access: Cardinal; Inherit: BOOL; Name: String): THandle;
  external 'OpenEventW@kernel32.dll stdcall setuponly';
function SignalExitEvent(Handle: THandle): BOOL;
  external 'SetEvent@kernel32.dll stdcall setuponly';
function BridgeTickCount: Int64;
  external 'GetTickCount64@kernel32.dll stdcall setuponly';

function CurrentBridgeSid: String;
var Token, Buffer, Sid, Text: THandle; Used: Cardinal; Count: Integer;
begin
  Result := '';
  if not OpenProcessToken(THandle(-1), 8 { TOKEN_QUERY }, Token) then Exit;
  try
    { TOKEN_USER contains a pointer into the same buffer. Keep its native
      address stable until ConvertSidToStringSid has copied the SID. }
    Buffer := AllocateTokenBuffer(0, 128); { TOKEN_USER + SECURITY_MAX_SID_SIZE }
    if Buffer = 0 then Exit;
    try
      if not GetTokenInformation(Token, 1 { TokenUser }, Buffer, 128, Used) then Exit;
      ReadSidPointer(Sid, Buffer, SizeOf(Sid));
      if not ConvertSidToStringSid(Sid, Text) then Exit;
    finally FreeBuffer(Buffer); end;
    try
      Count := SidTextLength(Text);
      if Count <= 0 then Exit;
      SetLength(Result, Count + 1);
      CopySidText(Result, Text, Count + 1);
      SetLength(Result, Count);
    finally FreeBuffer(Text); end;
  finally CloseKernelHandle(Token); end;
end;

function BridgeMutexExists(Name: String): Boolean;
var Handle: THandle;
begin
  Handle := OpenBridgeMutex($100000 { SYNCHRONIZE }, False, Name);
  Result := Handle <> 0;
  if Result then CloseKernelHandle(Handle)
  else if DLLGetLastError <> 2 { ERROR_FILE_NOT_FOUND } then
    RaiseException('Cannot inspect bridge mutex.');
end;

function RequestBridgeShutdown: Integer;
var Sid, Instance, Flasher: String; Signal: THandle;
  Requested, HadInstance, ShowingWait: Boolean; Started: Int64;
begin
  Result := 4; { Unexpected access or identity errors block installation. }
  try
    Sid := CurrentBridgeSid;
    if Pos('S-1-', Sid) <> 1 then Exit;
    Instance := 'Local\AIBotBridge.Instance.' + Sid;
    Flasher := 'Local\AIBotBridge.Flasher.' + Sid;
    Requested := False;
    HadInstance := False;
    Started := BridgeTickCount;
    ShowingWait := False;
    try
      repeat
        if BridgeMutexExists(Flasher) then begin Result := 2; Exit; end;
        if not BridgeMutexExists(Instance) then begin
          if HadInstance then Result := 10 else Result := 0;
          Exit;
        end;
        HadInstance := True;
        if not Requested then begin
          Signal := OpenExitEvent(2 { EVENT_MODIFY_STATE }, False, 'Local\AIBotBridge.Exit.' + Sid);
          if Signal <> 0 then begin
            try Requested := SignalExitEvent(Signal); finally CloseKernelHandle(Signal); end;
            if not Requested then Exit;
          end else if DLLGetLastError <> 2 then Exit;
        end;
        { Let an already visible wizard repaint during a slow shutdown. }
        if WizardForm.Visible and (BridgeTickCount - Started >= 1500) then begin
          if not ShowingWait then begin
            if BridgeWaitingPage = nil then
              BridgeWaitingPage := CreateOutputMarqueeProgressPage('正在等待 AI-bot 退出', '');
            BridgeWaitingPage.Show;
            ShowingWait := True;
          end;
          BridgeWaitingPage.Animate;
        end;
        Sleep(100);
      until BridgeTickCount - Started >= 45000;
      Result := 3;
    finally
      if ShowingWait then BridgeWaitingPage.Hide;
    end;
  except
    Log('Bridge shutdown check failed: ' + GetExceptionMessage);
  end;
end;
