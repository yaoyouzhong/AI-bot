#requires -Version 7.0
param(
    [Parameter(Mandatory)][string]$ImagePath,
    [string]$BridgeDll=(Join-Path $PSScriptRoot '../windows-app/AIBotBridge/bin/Release/net8.0-windows10.0.19041.0/AIBotBridge.dll'),
    [string]$OutputRoot=(Join-Path $PSScriptRoot '../artifacts/firmware/tab5')
)
$ErrorActionPreference='Stop'
$ImagePath=(Resolve-Path -LiteralPath $ImagePath).Path
$BridgeDll=(Resolve-Path -LiteralPath $BridgeDll).Path
$OutputRoot=[IO.Path]::GetFullPath($OutputRoot)
if(!(Test-Path -LiteralPath ($ImagePath+'.notes.json'))){throw 'A matching .bin.notes.json with formatted release notes is required.'}
# Reuse the production ESP image/checksum/chip/version/sidecar validator.
# This loads code only; it does not start the bridge or read pairing records.
$assembly=[Reflection.Assembly]::LoadFrom($BridgeDll)
$argsForLoad=[object[]]::new(1);$argsForLoad[0]=[string]$ImagePath
$package=$assembly.GetType('AIBotBridge.Tab5OtaPackage').GetMethod('Load',[Reflection.BindingFlags]'Public,NonPublic,Static').Invoke($null,$argsForLoad)
function PackageValue([string]$name){$package.GetType().GetProperty($name,[Reflection.BindingFlags]'Public,NonPublic,Instance').GetValue($package)}
$version=PackageValue 'Version';$sha=PackageValue 'Sha256';$notes=PackageValue 'Notes';$bytes=PackageValue 'Image'
if($version -notmatch '^\d+\.\d+\.\d+(?:[-.][A-Za-z0-9]+)*$'){throw 'Unsupported version for an archive directory.'}
if($notes -notmatch '(?m)^## .+' -or $notes -notmatch '(?m)^- .+'){
    throw 'Release notes require ## category headings and - list items; plain paragraphs cannot become the current firmware.'
}
$utf8=[Text.UTF8Encoding]::new($false)
$notesHash=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($utf8.GetBytes($notes))).ToLowerInvariant()
$archiveRelative='versions/'+$version+'/'+$sha.Substring(0,12)+'-'+$notesHash.Substring(0,8)
$archive=Join-Path $OutputRoot $archiveRelative
$latest=Join-Path $OutputRoot 'latest'
$sidecar=[ordered]@{version=$version;sha256=$sha;notes=$notes} | ConvertTo-Json
$manifest=[ordered]@{version=$version;sha256=$sha;elfSha256=(PackageValue 'ElfSha256');bytes=$bytes.Length;notesSha256=$notesHash;archive=$archiveRelative;scope='Local OTA candidate; no device write or public release'} | ConvertTo-Json
New-Item -ItemType Directory -Force -Path $OutputRoot | Out-Null
$lockPath=Join-Path $OutputRoot '.prepare.lock'
$guard=[IO.File]::Open($lockPath,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::None)
try {
    $textFiles=@{'aibot_tab5.bin.notes.json'=$sidecar;'manifest.json'=$manifest;'SHA256SUMS.txt'=($sha+'  aibot_tab5.bin'+"`n")}
    if(Test-Path -LiteralPath $archive){
        if((Get-FileHash -LiteralPath (Join-Path $archive 'aibot_tab5.bin')).Hash -ne $sha){throw 'Existing archive image mismatch; nothing replaced.'}
        foreach($name in $textFiles.Keys){if([IO.File]::ReadAllText((Join-Path $archive $name)) -cne $textFiles[$name]){throw 'Existing archive metadata mismatch; nothing replaced.'}}
    } else {
        New-Item -ItemType Directory -Path $archive -Force | Out-Null
        [IO.File]::WriteAllBytes((Join-Path $archive 'aibot_tab5.bin'),$bytes)
        foreach($name in $textFiles.Keys){[IO.File]::WriteAllText((Join-Path $archive $name),$textFiles[$name],$utf8)}
    }
    # Only known files are replaced; all prior published candidates remain in versions/.
    New-Item -ItemType Directory -Path $latest -Force | Out-Null
    foreach($name in @('aibot_tab5.bin','aibot_tab5.bin.notes.json','SHA256SUMS.txt','manifest.json')){
        $temp=Join-Path $latest ($name+'.'+[Guid]::NewGuid().ToString('N')+'.tmp')
        try{Copy-Item -LiteralPath (Join-Path $archive $name) -Destination $temp;[IO.File]::Move($temp,(Join-Path $latest $name),$true)}
        finally{if(Test-Path -LiteralPath $temp){Remove-Item -LiteralPath $temp}}
    }
    $argsForLoad[0]=[string](Join-Path $latest 'aibot_tab5.bin')
    $null=$assembly.GetType('AIBotBridge.Tab5OtaPackage').GetMethod('Load',[Reflection.BindingFlags]'Public,NonPublic,Static').Invoke($null,$argsForLoad)
    Write-Output ('TAB5_FIRMWARE_READY version='+$version+' image='+(Join-Path $latest 'aibot_tab5.bin'))
    Write-Output ('ARCHIVE='+$archive)
} finally {$guard.Dispose();Remove-Item -LiteralPath $lockPath}
