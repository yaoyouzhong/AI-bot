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
OutputBaseFilename=AIBotBridge-{#AppVersion}-setup-win-x64
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
WelcomeLabel2=安装程序将检查并补齐 AI-bot 所需的运行环境，再安装应用。%n%n已有的 .NET 8 桌面运行时和 WebView2 会自动跳过。缺少组件时需要联网下载；安装 .NET 时 Windows 可能请求管理员许可。%n%n升级前请从右下角托盘正常退出 AI-bot。用户设置、账号数据和设备固件不会被清除。
FinishedLabel=AI-bot 已安装。启动后请在右下角托盘查找图标：左键打开镜像，右键打开菜单。

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; Flags: checkedonce

[Files]
Source: "{#StageDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#DependencyDir}\webview2-bootstrapper.exe"; Flags: dontcopy

[Icons]
Name: "{userprograms}\AI-bot"; Filename: "{app}\AIBotBridge.exe"
Name: "{userdesktop}\AI-bot"; Filename: "{app}\AIBotBridge.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\AIBotBridge.exe"; Description: "启动 AI-bot（在右下角托盘运行）"; Flags: nowait postinstall skipifsilent

[Code]
const
  DesktopKey = 'SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.WindowsDesktop.App';
  WebViewKey = 'SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}';
var
  DependencyPage: TOutputMsgWizardPage;
  DownloadPage: TDownloadWizardPage;
  RuntimeRestartRequired: Boolean;

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
  DownloadPage.Show;
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
    DownloadPage.Hide;
  end;
end;

procedure CurPageChanged(CurPageID: Integer);
begin
  if CurPageID = DependencyPage.ID then DependencyPage.MsgLabel.Caption := DependencyStatus;
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

function PrepareToInstall(var NeedsRestart: Boolean): String;
var Code: Integer;
begin
  Result := '';
  if not DesktopInstalled then begin
    Result := DownloadDesktop;
    if Result <> '' then Exit;
    WizardForm.StatusLabel.Caption := '正在安装 .NET 8 桌面运行时，请处理 Windows 权限提示…';
    if not ShellExec('runas', ExpandConstant('{tmp}\dotnet-desktop.exe'),
      '/install /passive /norestart', '', SW_SHOWNORMAL, ewWaitUntilTerminated, Code) then begin
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
end;

function NeedRestart: Boolean;
begin
  Result := RuntimeRestartRequired;
end;
