#ifndef AppVersion
  #error AppVersion is required
#endif
#ifndef StageDir
  #error StageDir is required
#endif
#ifndef DependencyDir
  #error DependencyDir is required
#endif
#ifndef OutputDirPath
  #error OutputDirPath is required
#endif
#ifndef DesktopUrl
  #error DesktopUrl is required
#endif
#ifndef DesktopSha256
  #error DesktopSha256 is required
#endif

[Setup]
AppId={{8B24B155-4218-4B40-AD4A-6185901D351B}
AppName=AI-bot
AppVersion={#AppVersion}
AppPublisher=AI-bot contributors
DefaultDirName={localappdata}\Programs\AI-bot
DefaultGroupName=AI-bot
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.19041
OutputDir={#OutputDirPath}
OutputBaseFilename=AI-bot-bridge-v{#AppVersion}-windows-x64-setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
DisableProgramGroupPage=yes
DisableWelcomePage=no
LicenseFile={#StageDir}\LICENSE
UninstallDisplayIcon={app}\AIBotBridge.exe
SetupIconFile=..\AIBotBridge\Assets\app-icon.ico
CloseApplications=no
RestartApplications=no
SetupLogging=yes
Uninstallable=yes

[Languages]
Name: "zhcn"; MessagesFile: "compiler:Default.isl"

[LangOptions]
LanguageName=简体中文
LanguageID=$0804
DialogFontName=Microsoft YaHei
DialogFontSize=9

[Messages]
SetupAppTitle=安装
SetupWindowTitle=安装 - %1
UninstallAppTitle=卸载
UninstallAppFullTitle=卸载 %1
InformationTitle=提示
ConfirmTitle=确认
ErrorTitle=错误
ButtonBack=< 上一步
ButtonNext=下一步 >
ButtonInstall=安装
ButtonOK=确定
ButtonCancel=取消
ButtonYes=是
ButtonNo=否
ButtonFinish=完成
ButtonBrowse=浏览…
ButtonWizardBrowse=浏览…
ClickNext=点击“下一步”继续，或点击“取消”退出。
WelcomeLabel1=欢迎安装 [name]
WizardLicense=许可协议
LicenseLabel=请阅读以下许可说明。
LicenseLabel3=继续安装前，请阅读并接受许可协议。第三方组件的许可随应用附带。
LicenseAccepted=我接受许可协议
LicenseNotAccepted=我不接受许可协议
WizardSelectDir=选择安装位置
SelectDirDesc=将 AI-bot 安装到哪个文件夹？
SelectDirLabel3=将安装到下方文件夹。通常保留默认位置即可。
WizardSelectTasks=快捷方式
SelectTasksDesc=选择需要的快捷方式
SelectTasksLabel2=保留默认选项即可，点击“下一步”继续。
WizardReady=准备安装
ReadyLabel1=准备好后点击“安装”，开始补齐运行环境并安装 AI-bot。
ReadyLabel2a=点击“安装”继续，或点击“上一步”修改选项。
ReadyMemoDir=安装位置：
ReadyMemoTasks=快捷方式：
WizardPreparing=正在准备
PreparingDesc=检查并安装缺少的运行环境
CannotContinue=安装暂时无法继续，请处理以下问题后重试。
WizardInstalling=正在安装
InstallingLabel=请等待文件安装完成。
StatusExtractFiles=正在复制文件…
StatusCreateIcons=正在创建快捷方式…
FinishedHeadingLabel=AI-bot 安装完成
FinishedRestartLabel=运行环境要求重启电脑。请保存工作后重启，再启动 AI-bot。
YesRadio=现在重启
NoRadio=稍后重启
ExitSetupTitle=退出安装
ExitSetupMessage=安装尚未完成，确定退出吗？%n%n以后可以重新运行安装程序。已安装的微软公共运行组件会保留。
SetupFileCorrupt=安装文件损坏，请重新下载安装包。
ConfirmUninstall=确定卸载 %1 吗？用户设置和微软运行组件会保留。
UninstallStatusLabel=正在卸载 AI-bot…
UninstalledAll=%1 已卸载，用户设置和公共运行组件已保留。
WelcomeLabel2=安装程序会补齐缺少的 .NET 8 桌面运行时和 WebView2，需要联网时会提示。%n%n首次安装会尝试配置 USB、蓝牙和 Wi-Fi 解锁，Windows 可能请求管理员许可。%n%n升级时，安装程序会自动关闭并在完成后重新启动正在运行的 AI-bot。用户设置、账号数据、解锁配置和设备固件都会保留。
FinishedLabel=AI-bot 已安装。启动后请在右下角托盘查找图标：左键打开镜像，右键打开菜单。

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; Flags: checkedonce

[UninstallRun]
Filename: "{app}\AIBotBridge.exe"; Parameters: "--remove-admin-bridge"; Flags: runhidden; RunOnceId: "Administrator-bridge-removal"
Filename: "{app}\AIBotBridge.exe"; Parameters: "--configure-login-helper uninstall"; Flags: runhidden; RunOnceId: "Login-method-helper-removal"
Filename: "{app}\AIBotBridge.exe"; Parameters: "--tightvnc-uninstall-prompt"; Flags: runhidden; RunOnceId: "TightVNC-component-removal"

[Files]
Source: "{#StageDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#DependencyDir}\webview2-bootstrapper.exe"; Flags: dontcopy
Source: "prepare_bridge_upgrade.ps1"; Flags: dontcopy

[Icons]
Name: "{userprograms}\AI-bot"; Filename: "{app}\AIBotBridge.exe"
Name: "{userprograms}\AI-bot 小屏刷机"; Filename: "{app}\AIBotBridge.exe"; Parameters: "--flash"; IconFilename: "{app}\Assets\flash-icon.ico"
Name: "{userdesktop}\AI-bot"; Filename: "{app}\AIBotBridge.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\AIBotBridge.exe"; Description: "启动 AI-bot（在右下角托盘运行）"; Flags: nowait postinstall skipifsilent; Check: OfferBridgeLaunch

[Code]
const
  DesktopKey = 'SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.WindowsDesktop.App';
  WebViewKey = 'SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}';
  UninstallKey = 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{8B24B155-4218-4B40-AD4A-6185901D351B}_is1';
var
  DependencyPage: TOutputMsgWizardPage;
  DownloadPage: TDownloadWizardPage;
  RuntimeRestartRequired: Boolean;
  PreserveUnlockSettings: Boolean;
  UnlockSetupIncomplete: Boolean;
  BridgeStoppedForInstall: Boolean;

function IsDesktop8Version(Value: String): Boolean;
begin
  { Reject preview/malformed versions and other major runtimes. }
  Result := (Pos('8.0.', Value) = 1) and (Pos('-', Value) = 0);
  if Result then Result := StrToIntDef(Copy(Value, 5, Length(Value)), -1) >= 0;
end;

function ValidWebViewVersion(Value: String): Boolean;
var V: Int64;
begin
  Result := StrToVersion(Value, V) and (V > 0);
end;

function RuntimeSucceeded(Code: Integer; Detected: Boolean; AllowRestart: Boolean): Boolean;
begin
  Result := Detected and ((Code = 0) or
    (AllowRestart and ((Code = 3010) or (Code = 1641))));
end;

function DesktopInstalled: Boolean;
var Names: TArrayOfString; I: Integer;
begin
  Result := False;
  { .NET's x64 host registration lives in the 32-bit registry view. }
  if RegGetValueNames(HKLM32, DesktopKey, Names) then
    for I := 0 to GetArrayLength(Names) - 1 do
      if IsDesktop8Version(Names[I]) then begin Result := True; Exit; end;
end;

function WebViewInstalled: Boolean;
var Value: String;
begin
  Result := (RegQueryStringValue(HKLM32, WebViewKey, 'pv', Value) and ValidWebViewVersion(Value));
  if not Result then
    Result := (RegQueryStringValue(HKCU32, WebViewKey, 'pv', Value) and ValidWebViewVersion(Value));
end;

function DependencyStatus: String;
begin
  if DesktopInstalled then Result := '.NET 8 桌面运行时：已安装，将跳过。'
  else Result := '.NET 8 桌面运行时：未安装，将从微软下载约 59 MB 后补装。';
  if WebViewInstalled then Result := Result + #13#10#13#10 + 'WebView2：已安装，将跳过。'
  else Result := Result + #13#10#13#10 + 'WebView2：未安装，将从微软联网下载并安装。';
  Result := Result + #13#10#13#10 + '点击“安装”后开始补装。取消权限请求或网络失败时会停止，可处理后重试。';
end;

procedure InitializeWizard;
begin
  DependencyPage := CreateOutputMsgPage(wpSelectTasks, '检查运行环境',
    '已具备的组件不会重复安装', DependencyStatus);
  DownloadPage := CreateDownloadPage('下载运行环境',
    '正在从微软下载 .NET 桌面运行时，请保持联网。完成后自动校验文件。', nil);
  DownloadPage.ShowBaseNameInsteadOfUrl := True;
end;

function VerifiedDesktopFile: Boolean;
var Path: String;
begin
  Path := ExpandConstant('{tmp}\dotnet-desktop.exe');
  Result := False;
  if FileExists(Path) then
    Result := CompareText(GetSHA256OfFile(Path), '{#DesktopSha256}') = 0;
end;

function DownloadDesktop: String;
begin
  Result := '';
  if VerifiedDesktopFile then Exit;
  DownloadPage.Clear;
  DownloadPage.Add('{#DesktopUrl}', 'dotnet-desktop.exe', '{#DesktopSha256}');
  if not WizardSilent then DownloadPage.Show;
  try
    try
      DownloadPage.Download;
      if not VerifiedDesktopFile then
        Result := '.NET 下载文件未通过校验，未运行。请重试或重新下载安装程序。';
    except
      if DownloadPage.AbortedByUser then
        Result := '已取消 .NET 下载。点击重试可重新下载，也可以退出安装。'
      else
        Result := '.NET 下载失败或文件校验未通过。请检查网络后重试。' + #13#10 + GetExceptionMessage;
    end;
  finally
    if not WizardSilent then DownloadPage.Hide;
  end;
end;

procedure CurPageChanged(CurPageID: Integer);
begin
  if CurPageID = DependencyPage.ID then DependencyPage.MsgLabel.Caption := DependencyStatus;
  if (CurPageID = wpFinished) and UnlockSetupIncomplete then begin
    WizardForm.FinishedHeadingLabel.Caption := 'AI-bot 已安装，解锁配置尚未完成';
    WizardForm.FinishedLabel.Caption := '请启动 AI-bot，在一键解锁页面点击保存完成配置。USB／蓝牙和 Wi-Fi 全部检查通过后才会显示设置完成。';
  end;
end;

function BridgeUpdateRequested: Boolean;
begin
  Result := ExpandConstant('{param:AIBOTUPDATE|}') = '1';
end;

function OfferBridgeLaunch: Boolean;
begin
  Result := not BridgeStoppedForInstall;
end;

function ExistingBridgeInstallation: Boolean;
var InstallDirectory: String;
begin
  Result := False;
  if RegQueryStringValue(HKCU64, UninstallKey, 'InstallLocation', InstallDirectory) then
    if InstallDirectory <> '' then
      Result := FileExists(AddBackslash(InstallDirectory) + 'AIBotBridge.exe');
end;

procedure CurStepChanged(CurStep: TSetupStep);
var Code: Integer; Started: Boolean;
begin
  { Inspect the previous installation before Setup updates its registration. }
  if CurStep = ssInstall then
    PreserveUnlockSettings := BridgeStoppedForInstall or BridgeUpdateRequested or ExistingBridgeInstallation;
  if CurStep = ssPostInstall then begin
    if PreserveUnlockSettings then
      Log('Preserving existing computer unlock configuration during bridge upgrade.')
    else begin
      Log('Configuring computer unlock components during first installation.');
      WizardForm.StatusLabel.Caption := '正在默认配置电脑解锁，请完成 Windows 管理员确认…';
      Started := ExecAsOriginalUser(ExpandConstant('{app}\AIBotBridge.exe'),
        '--complete-unlock-setup', ExpandConstant('{app}'), SW_HIDE, ewWaitUntilTerminated, Code);
      UnlockSetupIncomplete := not Started or (Code <> 0);
      if UnlockSetupIncomplete then Log('Computer unlock setup incomplete; do not report all transports ready.')
      else Log('Computer unlock setup completed.');
    end;
  end;
  if (CurStep = ssDone) and (BridgeStoppedForInstall or (WizardSilent and BridgeUpdateRequested)) and not RuntimeRestartRequired then begin
    if ExecAsOriginalUser(ExpandConstant('{app}\AIBotBridge.exe'), '', ExpandConstant('{app}'),
       SW_HIDE, ewNoWait, Code) then
      Log('Automatic bridge restart requested.')
    else
      RaiseException('AI-bot 已安装。请从开始菜单重新启动 AI-bot。');
  end;
end;

function InitializeSetup: Boolean;
var Report: String; Lines: TArrayOfString;
begin
#ifdef DownloadProbe
  { Compiled only by the isolated HTTP-fixture test, never the shipped setup. }
  Report := ExpandConstant('{param:DOWNLOADPROBE|}');
  if Report <> '' then begin
    SetArrayLength(Lines, 1);
    try
      DownloadTemporaryFile('{#DesktopUrl}', 'dotnet-desktop.exe', '{#DesktopSha256}', nil);
      if not VerifiedDesktopFile then RaiseException('Checksum validation failed');
      Lines[0] := 'DOWNLOAD_OK';
    except
      Lines[0] := 'DOWNLOAD_BLOCKED';
    end;
    if not SaveStringsToUTF8File(Report, Lines, False) then RaiseException('Cannot write probe report');
    Result := False; Exit;
  end;
#endif
  { Read-only diagnostics: no installation, shortcuts or runtime changes. }
  Report := ExpandConstant('{param:CHECKONLY|}');
  if Report <> '' then begin
    SetArrayLength(Lines, 3);
    Lines[0] := 'desktopInstalled=' + IntToStr(Ord(DesktopInstalled));
    Lines[1] := 'webViewInstalled=' + IntToStr(Ord(WebViewInstalled));
    Lines[2] := DependencyStatus;
    if not SaveStringsToUTF8File(Report, Lines, False) then
      RaiseException('Cannot write dependency report');
    Result := False; Exit;
  end;
  if ExpandConstant('{param:SELFTEST|}') <> '' then begin
    if not IsDesktop8Version('8.0.0') or not IsDesktop8Version('8.0.99') or
       IsDesktop8Version('9.0.0') or IsDesktop8Version('8.0.0-preview') or
       IsDesktop8Version('8.0.bad') or ValidWebViewVersion('0.0.0.0') or
       ValidWebViewVersion('bad') or not ValidWebViewVersion('130.0.2849.0') or
       RuntimeSucceeded(1603, True, True) or RuntimeSucceeded(0, False, True) or
       RuntimeSucceeded(1223, False, True) or not RuntimeSucceeded(3010, True, True) or
       RuntimeSucceeded(3010, True, False) or not RuntimeSucceeded(0, True, False) then
      RaiseException('Dependency version checks failed');
    if not SaveStringToFile(ExpandConstant('{param:SELFTEST|}'), 'INSTALLER_SELFTEST_OK', False) then
      RaiseException('Cannot write self-test report');
    Result := False; Exit;
  end;
  Result := True;
end;

function PrepareBridgeUpgrade: String;
var Code: Integer;
begin
  Result := '';
  WizardForm.StatusLabel.Caption := '正在检查 AI-bot 是否需要退出…';
  ExtractTemporaryFile('prepare_bridge_upgrade.ps1');
  if not Exec(ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe'),
    '-NoProfile -NonInteractive -ExecutionPolicy Bypass -File "' + ExpandConstant('{tmp}\prepare_bridge_upgrade.ps1') + '"',
    '', SW_HIDE, ewWaitUntilTerminated, Code) then
    Code := 4;
  Log('Bridge shutdown check returned ' + IntToStr(Code) + '.');
  case Code of
    0: ;
    10: BridgeStoppedForInstall := True;
    2: Result := '小屏刷机正在进行。请等刷机结束后重试安装。';
    3: Result := 'AI-bot 暂未完成退出。请处理 AI-bot 中的提示或等正在进行的操作结束，再重试安装。';
  else
    Result := '无法确认 AI-bot 已退出。请重试安装。';
  end;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var Code, ShowMode: Integer; RuntimeArgs: String;
begin
  Result := '';
  if not DesktopInstalled then begin
    Result := DownloadDesktop;
    if Result <> '' then Exit;
    WizardForm.StatusLabel.Caption := '正在安装 .NET 8 桌面运行时，请处理 Windows 权限提示…';
    RuntimeArgs := '/install /passive /norestart';
    ShowMode := SW_SHOWNORMAL;
    if WizardSilent and BridgeUpdateRequested then begin
      RuntimeArgs := '/install /quiet /norestart';
      ShowMode := SW_HIDE;
    end;
    if not ShellExec('runas', ExpandConstant('{tmp}\dotnet-desktop.exe'),
      RuntimeArgs, '', ShowMode, ewWaitUntilTerminated, Code) then begin
      Result := '.NET 安装未能启动或管理员许可已取消。请允许安装，或联系管理员后重试。'; Exit;
    end;
    if (Code <> 0) and (Code <> 3010) and (Code <> 1641) then begin
      Result := '.NET 安装失败（代码 ' + IntToStr(Code) + '）。请处理安装器提示后重试。'; Exit;
    end;
    RuntimeRestartRequired := (Code = 3010) or (Code = 1641);
    if not DesktopInstalled then begin
      NeedsRestart := RuntimeRestartRequired;
      Result := '.NET 安装后仍未检测到运行环境。若要求重启，请重启电脑后重新运行安装程序。'; Exit;
    end;
  end;
  if not WebViewInstalled then begin
    WizardForm.StatusLabel.Caption := '正在从微软下载并安装 WebView2，请保持联网…';
    ExtractTemporaryFile('webview2-bootstrapper.exe');
    if not Exec(ExpandConstant('{tmp}\webview2-bootstrapper.exe'), '/silent /install',
      '', SW_HIDE, ewWaitUntilTerminated, Code) then begin
      Result := '无法启动 WebView2 安装，请检查系统权限后重试。'; Exit;
    end;
    if not RuntimeSucceeded(Code, WebViewInstalled, False) then begin
      Result := 'WebView2 安装未完成（代码 ' + IntToStr(Code) + '）。请检查网络或单位安装策略，然后重试。'; Exit;
    end;
  end;
  Result := PrepareBridgeUpgrade;
end;

function NeedRestart: Boolean;
begin
  Result := RuntimeRestartRequired;
end;
