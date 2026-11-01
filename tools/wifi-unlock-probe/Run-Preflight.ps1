#requires -RunAsAdministrator
[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$taskName = 'AIBot-WifiUnlockPreflight'
$installPath = Join-Path $env:ProgramFiles 'AI-bot-WifiUnlockProbe'
$session = (Get-Process -Id $PID).SessionId
if ($session -le 0) { throw 'Run from the interactive desktop.' }
if (Test-Path -LiteralPath $installPath) { throw "Existing path retained: $installPath. Review it before using Remove-Preflight.ps1." }
if (Get-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue) { throw 'Task already exists; it was not changed.' }
$manifest = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'sha256.json') -Raw | ConvertFrom-Json
$required = @('WifiUnlockProbe.exe','WifiUnlockProbe.dll','WifiUnlockProbe.deps.json','WifiUnlockProbe.runtimeconfig.json')
if (@($manifest.PSObject.Properties).Count -ne $required.Count) { throw 'Invalid manifest.' }
foreach ($file in $required) {
    $source = Join-Path $PSScriptRoot $file
    if ((Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash -ne $manifest.$file) { throw "Hash mismatch: $file" }
}
# All elevated executable content goes into a newly created protected directory.
New-Item -ItemType Directory -Path $installPath | Out-Null
$acl = [Security.AccessControl.DirectorySecurity]::new()
$acl.SetAccessRuleProtection($true, $false)
$acl.SetOwner([Security.Principal.SecurityIdentifier]::new('S-1-5-32-544'))
foreach ($sid in @('S-1-5-18','S-1-5-32-544')) {
    $acl.AddAccessRule([Security.AccessControl.FileSystemAccessRule]::new([Security.Principal.SecurityIdentifier]::new($sid), 'FullControl', 'ContainerInherit,ObjectInherit', 'None', 'Allow'))
}
$acl.AddAccessRule([Security.AccessControl.FileSystemAccessRule]::new([Security.Principal.SecurityIdentifier]::new('S-1-5-32-545'), 'ReadAndExecute', 'ContainerInherit,ObjectInherit', 'None', 'Allow'))
Set-Acl -LiteralPath $installPath -AclObject $acl
foreach ($file in $required) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot $file) -Destination (Join-Path $installPath $file)
    if ((Get-FileHash -LiteralPath (Join-Path $installPath $file) -Algorithm SHA256).Hash -ne $manifest.$file) { throw "Copied hash mismatch: $file" }
}
[ordered]@{ Tool='AIBot-WifiUnlockPreflight'; Version=1; CreatedUtc=[DateTime]::UtcNow.ToString('O'); Session=$session; Files=$required } |
    ConvertTo-Json | Set-Content -LiteralPath (Join-Path $installPath 'receipt.json') -Encoding utf8
$registered = $false
try {
    $action = New-ScheduledTaskAction -Execute (Join-Path $installPath 'WifiUnlockProbe.exe') -Argument "--system-launch $session" -WorkingDirectory $installPath
    $principal = New-ScheduledTaskPrincipal -UserId 'SYSTEM' -LogonType ServiceAccount -RunLevel Highest
    $settings = New-ScheduledTaskSettingsSet -ExecutionTimeLimit (New-TimeSpan -Minutes 3) -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries
    Register-ScheduledTask -TaskName $taskName -Action $action -Principal $principal -Settings $settings -Description 'One-shot read-only login UI structure probe. No credential reads or input.' | Out-Null
    $registered = $true
    Start-ScheduledTask -TaskName $taskName
    Write-Host 'Probe started. Within 120 seconds lock Windows manually and show the PIN entry screen. Do not enter a PIN for this prerequisite probe.'
    $deadline = [DateTime]::UtcNow.AddSeconds(155)
    $result = Join-Path $installPath 'result.json'
    while ([DateTime]::UtcNow -lt $deadline -and !(Test-Path -LiteralPath $result)) { Start-Sleep -Seconds 1 }
    if (Test-Path -LiteralPath $result) {
        # A short wait allows the bounded inspector to close its report and exit.
        Start-Sleep -Seconds 2
        Get-Content -LiteralPath $result -Raw
    } else { throw 'No report. Inspect LastTaskResult before removing the diagnostic files.' }
} finally {
    if ($registered) {
        Stop-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue
        Unregister-ScheduledTask -TaskName $taskName -Confirm:$false
    }
    Write-Host "Diagnostic files retained in $installPath. The scheduled task has no recurring trigger. Use Remove-Preflight.ps1 after preserving the report."
}
