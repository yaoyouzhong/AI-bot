using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;

namespace AIBotBridge;

internal static class DesktopUpdate
{
    private const string UninstallKey=@"Software\Microsoft\Windows\CurrentVersion\Uninstall\{8B24B155-4218-4B40-AD4A-6185901D351B}_is1";

    internal static bool IsInstalled(string executable)
    {
        if(!BridgeInstallation.TrustedExecutable(executable,BridgeElevation.Owner))return false;
        try {
            using var registration=Registry.CurrentUser.OpenSubKey(UninstallKey);
            if(registration?.GetValue("InstallLocation") is not string directory||!Path.IsPathFullyQualified(directory)||
                !File.Exists(Path.Combine(directory,"AIBotBridge.exe"))||
                !Directory.EnumerateFiles(directory,"unins*.exe").Any(file=>File.Exists(Path.ChangeExtension(file,".dat"))))
                return false;

            // The installed launcher and the scheduled tray run from different
            // directories. The bridge DLL binds the registration to this build.
            using var installed=File.OpenRead(Path.Combine(directory,"AIBotBridge.dll"));
            using var running=File.OpenRead(Path.ChangeExtension(executable,".dll"));
            return SHA256.HashData(installed).AsSpan().SequenceEqual(SHA256.HashData(running));
        }catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or ArgumentException or System.Security.SecurityException) {
            return false;
        }
    }

    internal static void StartAfterExit(PreparedUpdate package,string executable)
    {
        // Only the verified setup runs elevated, from an administrator-owned copy.
        string installer=BridgeInstallation.StageDesktopUpdate(package,executable);
        const string script="""
            $ErrorActionPreference = 'Stop'
            function Show-Status($message) {
                Add-Type -AssemblyName PresentationFramework
                [System.Windows.MessageBox]::Show($message,'AI-bot') | Out-Null
            }
            $failure = '安装未完成。正在尝试重新启动 AI-bot。'
            try {
                $old = Get-Process -Id ([int]$env:AIBOT_UPGRADE_PID) -ErrorAction SilentlyContinue
                if ($old -and !$old.WaitForExit(30000)) {
                    Show-Status 'AI-bot 未退出，请关闭正在操作的窗口后重试。'
                    exit 1
                }
                $setup = Start-Process -FilePath $env:AIBOT_UPGRADE_SETUP -ArgumentList '/SILENT /NORESTART /RESTARTEXITCODE=3010 /AIBOTUPDATE=1' -PassThru
                $setup.WaitForExit()
                if ($setup.ExitCode -eq 0) { exit 0 }
                if ($setup.ExitCode -eq 3010) {
                    Show-Status '安装完成。请重启 Windows 后打开 AI-bot。'
                    exit
                }
            } catch {
                $failure = '无法启动 AI-bot 安装程序，请从软件固件页重试。'
            } finally {
                try {
                    [IO.File]::Delete($env:AIBOT_UPGRADE_SETUP)
                    [IO.Directory]::Delete([IO.Path]::GetDirectoryName($env:AIBOT_UPGRADE_SETUP))
                } catch { Write-Warning $_.Exception.Message }
            }
            Start-Process -FilePath $env:AIBOT_UPGRADE_APP
            Show-Status $failure
            """;
        var start=new ProcessStartInfo(Path.Combine(Environment.SystemDirectory,"WindowsPowerShell","v1.0","powershell.exe")){
            UseShellExecute=false,CreateNoWindow=true
        };
        foreach(string argument in new[]{"-NoProfile","-NonInteractive","-EncodedCommand",Convert.ToBase64String(Encoding.Unicode.GetBytes(script))})
            start.ArgumentList.Add(argument);
        start.Environment["AIBOT_UPGRADE_PID"]=Environment.ProcessId.ToString();
        start.Environment["AIBOT_UPGRADE_APP"]=executable;
        start.Environment["AIBOT_UPGRADE_SETUP"]=installer;
        try {
            using var relay=Process.Start(start)??throw new InvalidOperationException("无法启动安装程序。");
        }catch{
            File.Delete(installer);
            Directory.Delete(Path.GetDirectoryName(installer)!);
            throw;
        }
    }
}
