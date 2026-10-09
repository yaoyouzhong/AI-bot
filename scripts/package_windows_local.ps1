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
    if(Test-Path -LiteralPath (Join-Path $stage 'Assets/DailyArt')) { throw 'Public package must not contain local artwork resources' }
    @{edition='standard';optionalArt=$true;bundledArt=$false;version=(Get-Content (Join-Path $sourceRoot 'VERSION') -Raw).Trim()} |
        ConvertTo-Json | Set-Content (Join-Path $stage 'EDITION.json') -Encoding utf8
    python (Join-Path $sourceRoot 'scripts\collect_distribution_materials.py') windows --stage $stage
    if ($LASTEXITCODE -ne 0) { throw 'Distribution materials failed validation' }
    & (Join-Path $sourceRoot 'scripts\build_windows_installer.ps1') -StageDirectory $stage -OutputDirectory $sourceRoot -CacheDirectory (Join-Path $repoPath 'artifacts\installer-tools')
    $manifest = @(Get-ChildItem -LiteralPath $stage -Recurse -File | Where-Object Name -ne 'FILES.sha256' | Sort-Object FullName | ForEach-Object {
        $relative = $_.FullName.Substring($stage.Length + 1).Replace('\','/')
        (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant() + '  ' + $relative
    })
    $manifest | Set-Content -LiteralPath (Join-Path $stage 'FILES.sha256') -Encoding ascii
    $version = (Get-Content (Join-Path $sourceRoot 'VERSION') -Raw).Trim()
    $firmwareVersion = (Get-Content (Join-Path $sourceRoot 'firmware\VERSION') -Raw).Trim()
    $zip = Join-Path $sourceRoot "AIBotBridge-$version-local-candidate-win-x64.zip"
    # Python ZIP operations support long dependency-notice paths on Windows.
    # Compress-Archive in Windows PowerShell 5.1 fails after publish succeeds.
    python -c "import pathlib,sys,zipfile; sys.path.insert(0,sys.argv[1]); from collect_distribution_materials import filesystem_path; root=filesystem_path(pathlib.Path(sys.argv[2])); archive=zipfile.ZipFile(filesystem_path(pathlib.Path(sys.argv[3])), 'x', zipfile.ZIP_DEFLATED); [archive.write(p, p.relative_to(root).as_posix()) for p in sorted(root.rglob('*')) if p.is_file()]; archive.close()" $PSScriptRoot $stage $zip
    if ($LASTEXITCODE -ne 0) { throw 'Windows ZIP creation failed' }
    ((Get-FileHash -LiteralPath $zip).Hash.ToLowerInvariant() + '  ' + [IO.Path]::GetFileName($zip)) | Set-Content -LiteralPath ($zip + '.sha256') -Encoding ascii
    $extracted = Join-Path $sourceRoot 'artifacts\unpacked-check'
    python -c "import pathlib,sys,zipfile; sys.path.insert(0,sys.argv[1]); from collect_distribution_materials import filesystem_path; archive=zipfile.ZipFile(filesystem_path(pathlib.Path(sys.argv[2]))); archive.extractall(filesystem_path(pathlib.Path(sys.argv[3]))); archive.close()" $PSScriptRoot $zip $extracted
    if ($LASTEXITCODE -ne 0) { throw 'Windows ZIP extraction failed' }
    python -c "import hashlib,pathlib,sys; sys.path.insert(0,sys.argv[1]); from collect_distribution_materials import filesystem_path; root=filesystem_path(pathlib.Path(sys.argv[2])); entries=[line.split('  ',1) for line in (root/'FILES.sha256').read_text(encoding='ascii').splitlines()]; assert all(hashlib.sha256((root/name).read_bytes()).hexdigest()==expected for expected,name in entries), 'Extracted archive mismatch'; print('WINDOWS_ARCHIVE_BYTES_OK',len(entries))" $PSScriptRoot $extracted
    if ($LASTEXITCODE -ne 0) { throw 'Windows extracted archive verification failed' }
    Push-Location $extracted
    try {
        powershell -NoProfile -File (Join-Path $sourceRoot 'scripts\run_public_self_test.ps1') -BridgeDll .\AIBotBridge.dll
        if ($LASTEXITCODE -ne 0) { throw 'Extracted package regression failed' }
    } finally { Pop-Location }
    python (Join-Path $sourceRoot 'scripts\collect_distribution_materials.py') source --stage (Join-Path $sourceRoot "AI-bot-$version-source.zip")
    if ($LASTEXITCODE -ne 0) { throw 'Source archive failed validation' }
    if ($Firmware) {
        python (Join-Path $sourceRoot 'scripts\collect_distribution_materials.py') firmware --stage (Join-Path $sourceRoot "AI-bot-$firmwareVersion-firmware-materials")
        if ($LASTEXITCODE -ne 0) { throw 'Firmware materials failed validation' }
    }
    if ($OutputDirectory) {
        New-Item -ItemType Directory -Path $OutputDirectory | Out-Null
        $deliverables = @($zip, ($zip + '.sha256'),
            (Join-Path $sourceRoot "AIBotBridge-$version-setup-win-x64.exe"),
            (Join-Path $sourceRoot "AIBotBridge-$version-setup-win-x64.exe.sha256"),
            (Join-Path $sourceRoot "AI-bot-$version-source.zip"),
            (Join-Path $sourceRoot "AI-bot-$version-source.zip.sha256"))
        if ($Firmware) {
            $deliverables += (Join-Path $sourceRoot "AI-bot-$firmwareVersion-firmware-materials.zip")
            $deliverables += (Join-Path $sourceRoot "AI-bot-$firmwareVersion-firmware-materials.zip.sha256")
        }
        foreach ($file in $deliverables) { Copy-Item -LiteralPath $file -Destination $OutputDirectory }
    }
    Write-Output "LOCAL_WINDOWS_PACKAGE_OK zip=$zip files=$($manifest.Count)"
} finally { Pop-Location }
