using System.Diagnostics;
using System.Text;

namespace AIBotBridge;

internal static class DesktopUpdate
{
    internal static bool IsInstalled(string executable) =>
        Path.GetFileName(executable).Equals("AIBotBridge.exe",StringComparison.OrdinalIgnoreCase) &&
        Directory.EnumerateFiles(Path.GetDirectoryName(executable)!,"unins*.exe").Any();

    internal static void StartAfterExit(string installer,string executable)
    {
        // The old executable must release its own files before setup starts.
        // Keep paths in the child environment so they never become PowerShell source.
        const string script="""
            $old = Get-Process -Id ([int]$env:AIBOT_UPGRADE_PID) -ErrorAction SilentlyContinue
            if ($old -and !$old.WaitForExit(30000)) {
                Add-Type -AssemblyName PresentationFramework
                [System.Windows.MessageBox]::Show('AI-bot 未退出，请关闭正在操作的窗口后重试。','AI-bot') | Out-Null
                exit 1
            }
            try {
                $dir = Split-Path -LiteralPath $env:AIBOT_UPGRADE_APP
                $flags = '/SILENT /NORESTART /RESTARTEXITCODE=3010 /DIR="' + $dir + '"'
                $setup = Start-Process -FilePath $env:AIBOT_UPGRADE_SETUP -ArgumentList $flags -Wait -PassThru
                if ($setup.ExitCode -eq 3010) {
                    Add-Type -AssemblyName PresentationFramework
                    [System.Windows.MessageBox]::Show('安装完成。请重启 Windows 后打开 AI-bot。','AI-bot') | Out-Null
                    exit
                }
                if ($setup.ExitCode -notin @(0, 2, 5)) {
                    Add-Type -AssemblyName PresentationFramework
                    [System.Windows.MessageBox]::Show('安装未完成。正在尝试重新启动 AI-bot。','AI-bot') | Out-Null
                }
            } catch {
                Add-Type -AssemblyName PresentationFramework
                [System.Windows.MessageBox]::Show('无法启动 AI-bot 安装程序，请从软件固件页重试。','AI-bot') | Out-Null
            }
            Start-Process -FilePath $env:AIBOT_UPGRADE_APP
            """;
        var start=new ProcessStartInfo(Path.Combine(Environment.SystemDirectory,"WindowsPowerShell","v1.0","powershell.exe")){
            UseShellExecute=false,CreateNoWindow=true
        };
        foreach(string argument in new[]{"-NoProfile","-NonInteractive","-EncodedCommand",Convert.ToBase64String(Encoding.Unicode.GetBytes(script))})
            start.ArgumentList.Add(argument);
        start.Environment["AIBOT_UPGRADE_PID"]=Environment.ProcessId.ToString();
        start.Environment["AIBOT_UPGRADE_APP"]=executable;
        start.Environment["AIBOT_UPGRADE_SETUP"]=installer;
        using var relay=Process.Start(start)??throw new InvalidOperationException("无法启动安装程序。");
    }
}
