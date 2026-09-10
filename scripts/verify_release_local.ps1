param([switch]$Firmware)
$ErrorActionPreference = 'Stop'
$repoPath = Split-Path $PSScriptRoot
$auditPath = Join-Path $repoPath ('artifacts\release-check-' + [Guid]::NewGuid().ToString('N'))

# Never build over the resident bridge's files or send it an exit signal.
Push-Location $repoPath
try {
    python scripts/check_version.py
    if ($LASTEXITCODE -ne 0) { throw 'Version check failed' }
    python scripts/check_public_content.py
    if ($LASTEXITCODE -ne 0) { throw 'Public content guard failed' }
    python scripts/collect_distribution_materials.py verify
    if ($LASTEXITCODE -ne 0) { throw 'Distribution evidence failed' }
    python scripts/test_release_packaging.py
    if ($LASTEXITCODE -ne 0) { throw 'Packaging boundary tests failed' }
    $files = git -c "safe.directory=$($repoPath.Replace('\','/'))" ls-files --cached --others --exclude-standard
    if ($LASTEXITCODE -ne 0) { throw 'Source inventory failed' }
    New-Item -ItemType Directory -Path $auditPath | Out-Null
    foreach ($file in ($files | Sort-Object -Unique)) {
        $source = Join-Path $repoPath $file
        if (!(Test-Path -LiteralPath $source -PathType Leaf)) { continue }
        $target = [IO.Path]::GetFullPath((Join-Path $auditPath $file))
        if (!$target.StartsWith($auditPath + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Source path escaped audit directory' }
        New-Item -ItemType Directory -Force -Path (Split-Path $target) | Out-Null
        Copy-Item -LiteralPath $source -Destination $target
    }
    Push-Location $auditPath
    try {
        dotnet build windows-app/AIBotBridge/AIBotBridge.csproj -c Release --nologo
        if ($LASTEXITCODE -ne 0) { throw 'Isolated Windows build failed' }
        # Use the console host so PowerShell waits and preserves output/exit codes.
        powershell -NoProfile -File scripts/run_public_self_test.ps1 -BridgeDll .\windows-app\AIBotBridge\bin\Release\net8.0-windows10.0.19041.0\AIBotBridge.dll
        if ($LASTEXITCODE -ne 0) { throw 'Public self-test failed' }
        if ($Firmware) {
            python -m platformio run -d firmware
            if ($LASTEXITCODE -ne 0) { throw 'Isolated firmware build failed' }
        }
        $sourceManifest = @($files | Sort-Object -Unique | ForEach-Object {
            $copy = Join-Path $auditPath $_
            if (Test-Path -LiteralPath $copy -PathType Leaf) {
                (Get-FileHash -LiteralPath $copy -Algorithm SHA256).Hash.ToLowerInvariant() + '  ' + $_.Replace('\','/')
            }
        })
        $sourceManifest | Set-Content -LiteralPath (Join-Path $auditPath 'SOURCE_FILES.sha256') -Encoding utf8
    } finally { Pop-Location }
    Write-Output "LOCAL_RELEASE_CHECK_OK evidence=$auditPath firmware=$Firmware"
} finally { Pop-Location }
