#requires -RunAsAdministrator
[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath($env:ProgramFiles).TrimEnd('\')
$expected = Join-Path $root 'AI-bot-WifiUnlockTrial'
if (!(Test-Path -LiteralPath $expected)) { Write-Host 'Probe directory is absent.'; return }
$item = Get-Item -LiteralPath $expected
if ($item.FullName -ne $expected -or $item.Attributes.HasFlag([IO.FileAttributes]::ReparsePoint)) { throw 'Unexpected or redirected target; nothing removed.' }
$receipt = Get-Content -LiteralPath (Join-Path $expected 'receipt.json') -Raw | ConvertFrom-Json
if ($receipt.Tool -ne 'AIBot-WifiUnlockTrial' -or $receipt.Version -ne 1) { throw 'Ownership receipt is missing or invalid.' }
$task = Get-ScheduledTask -TaskName 'AIBot-WifiUnlockTrial' -ErrorAction SilentlyContinue
if ($task) {
    if (@($task.Actions).Count -ne 1 -or $task.Actions[0].Execute -ne (Join-Path $expected 'WifiUnlockTrial.exe')) { throw 'Task ownership mismatch.' }
    Stop-ScheduledTask -TaskName $task.TaskName
    Unregister-ScheduledTask -TaskName $task.TaskName -Confirm:$false
}
$allowed = @('WifiUnlockTrial.exe','WifiUnlockTrial.dll','WifiUnlockTrial.deps.json','WifiUnlockTrial.runtimeconfig.json','receipt.json','result.json')
$children = @(Get-ChildItem -LiteralPath $expected -Force)
foreach ($child in $children) {
    if ($child.PSIsContainer -or $child.Attributes.HasFlag([IO.FileAttributes]::ReparsePoint) -or $child.Name -notin $allowed) { throw 'Unexpected content retained; nothing removed.' }
}
if (Get-Process WifiUnlockTrial -ErrorAction SilentlyContinue | Where-Object Path -eq (Join-Path $expected 'WifiUnlockTrial.exe')) { throw 'Probe is still exiting; retry cleanup after 16 seconds.' }
# Exact nonrecursive allowlist. Preserve the report and its ownership receipt.
$hasReport = Test-Path -LiteralPath (Join-Path $expected 'result.json')
foreach ($child in $children) {
    if ($hasReport -and $child.Name -in @('result.json','receipt.json')) { continue }
    Remove-Item -LiteralPath $child.FullName
}
if (!$hasReport) { Remove-Item -LiteralPath $expected }
Write-Host 'Temporary executable and task removed. Any report/receipt remain for review.'
