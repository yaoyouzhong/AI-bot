param(
    [Parameter(Mandatory=$true)][string]$StageDirectory,
    [Parameter(Mandatory=$true)][string]$OutputDirectory,
    [Parameter(Mandatory=$true)][string]$CacheDirectory
)
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
$root = Split-Path $PSScriptRoot
$stage = [IO.Path]::GetFullPath($StageDirectory)
$output = [IO.Path]::GetFullPath($OutputDirectory)
$cache = [IO.Path]::GetFullPath($CacheDirectory)
$version = (Get-Content (Join-Path $root 'VERSION') -Raw).Trim()
if (!(Test-Path (Join-Path $stage 'AIBotBridge.exe'))) { throw 'Publish the complete application first' }
New-Item -ItemType Directory -Force -Path $cache,$output | Out-Null

function Get-Download([string]$Url, [string]$Path) {
    if (!(Test-Path -LiteralPath $Path)) {
        $partial = $Path + '.download'
        Invoke-WebRequest -UseBasicParsing -Uri $Url -OutFile $partial -TimeoutSec 600
        Move-Item -LiteralPath $partial -Destination $Path -Force
    }
}
function Assert-MicrosoftSignature([string]$Path) {
    $signature = Get-AuthenticodeSignature -LiteralPath $Path
    if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notmatch '(^|, )O=Microsoft Corporation(,|$)') {
        throw "Invalid Microsoft signature: $([IO.Path]::GetFileName($Path))"
    }
}

# Compiler is a pinned, unpacked build dependency; no global tool installation.
$tool = Join-Path $cache 'tools.innosetup.6.4.3.nupkg'
Get-Download 'https://api.nuget.org/v3-flatcontainer/tools.innosetup/6.4.3/tools.innosetup.6.4.3.nupkg' $tool
$toolHash = '9d67a2b1155ca5bc825475ceb907aa357c5feb42e474cfd5347eddeef5ad92a5'
if ((Get-FileHash $tool -Algorithm SHA256).Hash -ne $toolHash) { throw 'Inno Setup package hash mismatch' }
$compilerDir = Join-Path $cache ('inno-' + [Guid]::NewGuid().ToString('N'))
Add-Type -AssemblyName System.IO.Compression.FileSystem
[IO.Compression.ZipFile]::ExtractToDirectory($tool, $compilerDir)
$compiler = @(Get-ChildItem -LiteralPath $compilerDir -Recurse -Filter ISCC.exe)
if ($compiler.Count -ne 1) { throw 'Expected one Inno Setup compiler' }
python (Join-Path $root 'scripts\test_installer_download.py') --compiler $compiler[0].FullName
if ($LASTEXITCODE -ne 0) { throw 'Installer download boundary tests failed' }

# Pin the runtime to Microsoft's release metadata hash. Updating this runtime is
# an explicit source change, not an unreviewed download of an arbitrary executable.
$desktop = Join-Path $cache 'dotnet-desktop.exe'
$desktopUrl = 'https://builds.dotnet.microsoft.com/dotnet/WindowsDesktop/8.0.31/windowsdesktop-runtime-8.0.31-win-x64.exe'
$desktopHash = '605189223cf0a64bfb5453520b794d1d386a97c7e65e3944feaf9214db1a8b247f66c569265aef4204447baf5b5ff19559c323d887e1318f17deb28a5af0fa12'
Get-Download $desktopUrl $desktop
if ((Get-FileHash $desktop -Algorithm SHA512).Hash -ne $desktopHash) { throw '.NET runtime hash mismatch' }
Assert-MicrosoftSignature $desktop
$desktopSha256 = (Get-FileHash $desktop -Algorithm SHA256).Hash.ToLowerInvariant()
$webview = Join-Path $cache 'webview2-bootstrapper.exe'
$webviewUrl = 'https://go.microsoft.com/fwlink/p/?LinkId=2124703'
Get-Download $webviewUrl $webview
Assert-MicrosoftSignature $webview

$licenseDir = Join-Path $stage 'licenses\inno-setup'
New-Item -ItemType Directory -Force -Path $licenseDir | Out-Null
$license = Join-Path $compiler[0].DirectoryName 'license.txt'
if (!(Test-Path $license)) { throw 'Inno Setup license missing' }
Copy-Item -LiteralPath $license -Destination $licenseDir
@{
    innoSetup = @{ version='6.4.3'; packageSha256=$toolHash; source='https://www.nuget.org/packages/Tools.InnoSetup/6.4.3' }
    desktopRuntime = @{version='8.0.31'; url=$desktopUrl; sha512=$desktopHash; sha256=$desktopSha256; signature='Microsoft Corporation; Valid at build time'; bundled=$false; requiresInternet=$true}
    webView2Bootstrapper = @{url=$webviewUrl; sha256=(Get-FileHash $webview).Hash.ToLowerInvariant(); signature='Microsoft Corporation; Valid'; requiresInternet=$true}
} | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $stage 'INSTALLER_DEPENDENCIES.json') -Encoding UTF8

# Regenerate after adding installer notices so both ZIP and installed files have
# a complete manifest. Exclude the manifest itself, which cannot hash itself.
$manifest = @(Get-ChildItem -LiteralPath $stage -Recurse -File | Where-Object Name -ne 'FILES.sha256' | Sort-Object FullName | ForEach-Object {
    (Get-FileHash -LiteralPath $_.FullName).Hash.ToLowerInvariant() + '  ' + $_.FullName.Substring($stage.Length + 1).Replace('\','/')
})
$manifest | Set-Content -LiteralPath (Join-Path $stage 'FILES.sha256') -Encoding ascii
# Inno Setup still encounters MAX_PATH for dependency notices in nested audit
# directories. Compile a byte-verified copy at a short, private temporary path;
# keep the original stage and requested output as the authoritative artifacts.
$compileRoot = Join-Path ([IO.Path]::GetTempPath()) ('aib-' + [Guid]::NewGuid().ToString('N').Substring(0,12))
$compileStage = Join-Path $compileRoot 'stage'
$compileOutput = Join-Path $compileRoot 'out'
$copyScript = @'
import hashlib, pathlib, shutil, sys
sys.path.insert(0, sys.argv[1])
from collect_distribution_materials import filesystem_path
source, target = map(pathlib.Path, sys.argv[2:4])
target.mkdir(parents=True, exist_ok=False)
for original in filesystem_path(source).rglob('*'):
    if original.is_symlink():
        raise ValueError('Linked installer material')
    if original.is_file():
        destination = filesystem_path(target / original.relative_to(filesystem_path(source)))
        destination.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(original, destination)
        if hashlib.sha256(original.read_bytes()).digest() != hashlib.sha256(destination.read_bytes()).digest():
            raise ValueError('Installer staging byte mismatch')
print('INSTALLER_SHORT_STAGE_OK')
'@
python -c $copyScript (Join-Path $root 'scripts') $stage $compileStage
if ($LASTEXITCODE -ne 0) { throw 'Installer staging failed' }
& $compiler[0].FullName "/DAppVersion=$version" "/DStageDir=$compileStage" "/DDependencyDir=$cache" "/DOutputDirPath=$compileOutput" "/DDesktopUrl=$desktopUrl" "/DDesktopSha256=$desktopSha256" (Join-Path $root 'windows-app\installer\setup.iss')
if ($LASTEXITCODE -ne 0) { throw 'Windows installer compilation failed' }
$setup = Join-Path $output "AIBotBridge-$version-setup-win-x64.exe"
$compiledSetup = Join-Path $compileOutput ([IO.Path]::GetFileName($setup))
Copy-Item -LiteralPath $compiledSetup -Destination $setup
if ((Get-FileHash -LiteralPath $compiledSetup).Hash -ne (Get-FileHash -LiteralPath $setup).Hash) { throw 'Installer output byte mismatch' }
((Get-FileHash $setup).Hash.ToLowerInvariant() + '  ' + [IO.Path]::GetFileName($setup)) | Set-Content ($setup + '.sha256') -Encoding ascii

# Exercise the actual compiled Pascal version rules and this host's detection
# without installing anything or starting the resident application.
$test = Join-Path $output ('selftest-' + [Guid]::NewGuid().ToString('N') + '.txt')
$process = Start-Process -FilePath $setup -ArgumentList "/SELFTEST=`"$test`"",'/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART' -Wait -PassThru -WindowStyle Hidden
if (!(Test-Path $test) -or (Get-Content $test -Raw) -ne 'INSTALLER_SELFTEST_OK') { throw 'Compiled installer self-test failed' }
Remove-Item -LiteralPath $test
$check = Join-Path $output ('check-' + [Guid]::NewGuid().ToString('N') + '.txt')
Start-Process -FilePath $setup -ArgumentList "/CHECKONLY=`"$check`"",'/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART' -Wait -WindowStyle Hidden
if (!(Test-Path $check)) { throw 'Installer environment detection did not return a report' }
$detected = Get-Content $check -Encoding UTF8
# The build host just ran the net8 Windows app tests, so this is an independent
# check of actual registry-view detection, not only a version-parser unit test.
if ($detected[0] -ne 'desktopInstalled=1') { throw 'Installer missed the build host .NET 8 Desktop Runtime' }
Write-Output ("INSTALLER_ENVIRONMENT_OK " + $detected[0] + ' ' + $detected[1])
Remove-Item -LiteralPath $check
# Only remove this invocation's unique temporary directory after validation.
$tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\') + '\'
$resolvedCompileRoot = [IO.Path]::GetFullPath($compileRoot)
if (!$resolvedCompileRoot.StartsWith($tempRoot, [StringComparison]::OrdinalIgnoreCase) -or
    [IO.Path]::GetFileName($resolvedCompileRoot) -notmatch '^aib-[0-9a-f]{12}$') { throw 'Unexpected installer temporary path' }
Remove-Item -LiteralPath $resolvedCompileRoot -Recurse -Force
Write-Output "WINDOWS_INSTALLER_OK setup=$setup"
