[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$summary = (Invoke-RestMethod -Uri 'http://127.0.0.1:18765/diagnostics/tab5' -TimeoutSec 5).summary
if ($summary -isnot [string]) { throw 'Missing bridge diagnostic summary.' }
$lines = $summary -split '\r?\n'
# Keep only the current transport summary, not old device-health reports or RPC
# successes. A historical HTTP 200 cannot prove the active connection is Wi-Fi.
$usb = @($lines | Where-Object { $_ -match '^USB：' }) | Select-Object -First 1
$wifi = @($lines | Where-Object { $_ -match '^Wi-Fi：' }) | Select-Object -First 1
$ble = @($lines | Where-Object { $_ -match '^蓝牙：' }) | Select-Object -First 1
$snapshot = [ordered]@{
    CapturedUtc=[DateTime]::UtcNow.ToString('O')
    Usb=$usb
    Wifi=$wifi
    Bluetooth=$ble
    TransportPrerequisitePassed=($usb -eq 'USB：未连接' -and $wifi -eq 'Wi-Fi：已联网 · 桥接正常' -and $ble -in @('蓝牙：未连接','蓝牙：电脑未检测到蓝牙适配器','蓝牙：请在 Windows 设置中打开蓝牙'))
    UnlockTested=$false
}
$snapshot | ConvertTo-Json
if (!$snapshot.TransportPrerequisitePassed) { exit 2 }
