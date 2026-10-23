#requires -RunAsAdministrator
[CmdletBinding()]
param([ValidateSet('observe','cycle')][string]$Mode='observe')
$ErrorActionPreference = 'Stop'
$taskName = 'AIBot-LoginMethodProbe-20261022'
$installPath = Join-Path $env:ProgramFiles 'AI-bot-LoginMethodProbe-20261022'
if ($Mode -eq 'cycle') {
    $taskName = 'AIBot-LoginMethodSwitch-20261022'
    $installPath = Join-Path $env:ProgramFiles 'AI-bot-LoginMethodSwitch-20261022'
}
$session = (Get-Process -Id $PID).SessionId
if ($session -le 0) { throw 'Interactive desktop session required.' }
if (Test-Path -LiteralPath $installPath) { throw 'Existing observation retained; refusing overwrite.' }
if (Get-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue) { throw 'Existing task retained.' }
$required = @('LoginMethodProbe.exe','LoginMethodProbe.dll','LoginMethodProbe.deps.json','LoginMethodProbe.runtimeconfig.json')
$manifest = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'sha256.json') -Raw | ConvertFrom-Json
if (@($manifest.PSObject.Properties).Count -ne $required.Count) { throw 'Invalid manifest.' }
foreach ($file in $required) {
    if ((Get-FileHash -LiteralPath (Join-Path $PSScriptRoot $file) -Algorithm SHA256).Hash -ne $manifest.$file) { throw "Source hash mismatch: $file" }
}
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
    if ((Get-FileHash -LiteralPath (Join-Path $installPath $file) -Algorithm SHA256).Hash -ne $manifest.$file) { throw "Installed hash mismatch: $file" }
}
[ordered]@{ Tool=$taskName; Version=1; Session=$session; Files=$required; Hashes=$manifest; CreatedUtc=[DateTime]::UtcNow.ToString('O') } |
    ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $installPath 'receipt.json') -Encoding utf8
function Write-State([string]$stage,[int]$code=0) {
    [ordered]@{ Stage=$stage; Code=$code; Utc=[DateTime]::UtcNow.ToString('O') } |
        ConvertTo-Json | Set-Content -LiteralPath (Join-Path $installPath 'run-state.json') -Encoding utf8
}
$registered = $false
try {
    $arguments = "--launch $session"
    if ($Mode -eq 'cycle') { $arguments += ' cycle' }
    $action = New-ScheduledTaskAction -Execute (Join-Path $installPath 'LoginMethodProbe.exe') -Argument $arguments -WorkingDirectory $installPath
    $principal = New-ScheduledTaskPrincipal -UserId 'SYSTEM' -LogonType ServiceAccount -RunLevel Highest
    $settings = New-ScheduledTaskSettingsSet -ExecutionTimeLimit (New-TimeSpan -Minutes 5) -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries
    $description = 'Bounded login-method observation; no credentials, input, network or authentication settings.'
    if ($Mode -eq 'cycle') { $description = 'Bounded login-method selection experiment; no credential reads/submission, network or authentication settings.' }
    Register-ScheduledTask -TaskName $taskName -Action $action -Principal $principal -Settings $settings -Description $description | Out-Null
    $registered = $true
    Start-ScheduledTask -TaskName $taskName
    Write-State 'waiting-for-manual-lock'
    $deadline = [DateTime]::UtcNow.AddSeconds(275)
    do {
        Start-Sleep -Seconds 1
        $task = Get-ScheduledTask -TaskName $taskName
        if ($task.State -ne 'Running') { break }
    } while ([DateTime]::UtcNow -lt $deadline)
    $result = Get-ScheduledTaskInfo -TaskName $taskName
    Write-State 'task-ended' ([int]$result.LastTaskResult)
} catch {
    Write-State 'run-error' $_.Exception.HResult
    throw
} finally {
    if ($registered) {
        Stop-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue
        Unregister-ScheduledTask -TaskName $taskName -Confirm:$false
    }
    # Child watchdog is independent; retain files if it is still finishing.
    $running = @(Get-Process -Name LoginMethodProbe -ErrorAction SilentlyContinue | Where-Object Path -EQ (Join-Path $installPath 'LoginMethodProbe.exe'))
    if ($running.Count -eq 0) {
        foreach ($file in $required) {
            $target = Join-Path $installPath $file
            if ((Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash -ne $manifest.$file) { throw 'Cleanup hash mismatch; files retained.' }
        }
        foreach ($file in $required) { Remove-Item -LiteralPath (Join-Path $installPath $file) }
    }
}
