param([switch]$Firmware, [string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
$repoPath = Split-Path $PSScriptRoot
Push-Location $repoPath
try {
    if ($OutputDirectory) {
        $OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
        if (Test-Path -LiteralPath $OutputDirectory) { throw 'Use a new output directory' }
    }
    # The verified source copy is also the source of the publish operation.
    $verifyArguments = @('-NoProfile', '-File', (Join-Path $PSScriptRoot 'verify_release_local.ps1'))
    if ($Firmware) { $verifyArguments += '-Firmware' }
    $result = & powershell @verifyArguments
    $code = $LASTEXITCODE
    $result | Write-Output
    if ($code -ne 0) { throw 'Local verification failed' }
    $line = @($result | Where-Object { $_ -like 'LOCAL_RELEASE_CHECK_OK evidence=*' })[-1]
    if ($line -notmatch '^LOCAL_RELEASE_CHECK_OK evidence=(.+) firmware=(True|False)$') { throw 'Verified source path missing' }
    $sourceRoot = $Matches[1]
    $stage = Join-Path $sourceRoot 'artifacts\windows-package'
    dotnet publish (Join-Path $sourceRoot 'windows-app\AIBotBridge\AIBotBridge.csproj') -c Release -r win-x64 --self-contained false -o $stage
    if ($LASTEXITCODE -ne 0) { throw 'Publish failed' }
    python (Join-Path $sourceRoot 'scripts\collect_distribution_materials.py') windows --stage $stage
    if ($LASTEXITCODE -ne 0) { throw 'Distribution materials failed validation' }
    $manifest = @(Get-ChildItem -LiteralPath $stage -Recurse -File | Sort-Object FullName | ForEach-Object {
        $relative = $_.FullName.Substring($stage.Length + 1).Replace('\','/')
        (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant() + '  ' + $relative
    })
    $manifest | Set-Content -LiteralPath (Join-Path $stage 'FILES.sha256') -Encoding ascii
    $version = (Get-Content (Join-Path $sourceRoot 'VERSION') -Raw).Trim()
    $zip = Join-Path $sourceRoot "AIBotBridge-$version-local-candidate-win-x64.zip"
    Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip
    ((Get-FileHash -LiteralPath $zip).Hash.ToLowerInvariant() + '  ' + [IO.Path]::GetFileName($zip)) | Set-Content -LiteralPath ($zip + '.sha256') -Encoding ascii
    $extracted = Join-Path $sourceRoot 'artifacts\unpacked-check'
    Expand-Archive -LiteralPath $zip -DestinationPath $extracted
    foreach ($entry in $manifest) {
        $hash,$relative = $entry -split '  ',2
        if ((Get-FileHash -LiteralPath (Join-Path $extracted $relative)).Hash -ne $hash) { throw "Archive mismatch: $relative" }
    }
    Push-Location $extracted
    try {
        dotnet .\AIBotBridge.dll --self-test-public
        if ($LASTEXITCODE -ne 0) { throw 'Extracted package regression failed' }
    } finally { Pop-Location }
    python (Join-Path $sourceRoot 'scripts\collect_distribution_materials.py') source --stage (Join-Path $sourceRoot "AI-bot-$version-source.zip")
    if ($LASTEXITCODE -ne 0) { throw 'Source archive failed validation' }
    if ($Firmware) {
        python (Join-Path $sourceRoot 'scripts\collect_distribution_materials.py') firmware --stage (Join-Path $sourceRoot "AI-bot-$version-firmware-materials")
        if ($LASTEXITCODE -ne 0) { throw 'Firmware materials failed validation' }
    }
    if ($OutputDirectory) {
        New-Item -ItemType Directory -Path $OutputDirectory | Out-Null
        $deliverables = @($zip, ($zip + '.sha256'),
            (Join-Path $sourceRoot "AI-bot-$version-source.zip"),
            (Join-Path $sourceRoot "AI-bot-$version-source.zip.sha256"))
        if ($Firmware) {
            $deliverables += (Join-Path $sourceRoot "AI-bot-$version-firmware-materials.zip")
            $deliverables += (Join-Path $sourceRoot "AI-bot-$version-firmware-materials.zip.sha256")
        }
        foreach ($file in $deliverables) { Copy-Item -LiteralPath $file -Destination $OutputDirectory }
    }
    Write-Output "LOCAL_WINDOWS_PACKAGE_OK zip=$zip files=$($manifest.Count)"
} finally { Pop-Location }
