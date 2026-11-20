$ErrorActionPreference = 'Stop'
$sid = [Security.Principal.WindowsIdentity]::GetCurrent().User.Value
$instance = "Local\AIBotBridge.Instance.$sid"
$flasher = "Local\AIBotBridge.Flasher.$sid"
$exitSignal = "Local\AIBotBridge.Exit.$sid"

function Test-Mutex([string]$Name) {
    try {
        $mutex = [Threading.Mutex]::OpenExisting($Name, [Security.AccessControl.MutexRights]::Synchronize)
        $mutex.Dispose()
        return $true
    } catch [Threading.WaitHandleCannotBeOpenedException] {
        return $false
    }
}

try {
    if (Test-Mutex $flasher) { exit 2 }
    if (!(Test-Mutex $instance)) { exit 0 }

    $deadline = [Diagnostics.Stopwatch]::StartNew()
    $requested = $false
    while ($deadline.Elapsed.TotalSeconds -lt 45) {
        if (!(Test-Mutex $instance)) { exit 10 }
        if (!$requested) {
            try {
                $signal = [Threading.EventWaitHandle]::OpenExisting($exitSignal, [Security.AccessControl.EventWaitHandleRights]::Modify)
                try { $requested = $signal.Set() }
                finally { $signal.Dispose() }
            } catch [Threading.WaitHandleCannotBeOpenedException] {
                # The tray acquires its mutex before it starts listening.
            }
        }
        Start-Sleep -Milliseconds 250
    }
    exit 3
} catch {
    exit 4
}
