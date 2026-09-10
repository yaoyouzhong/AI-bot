param([Parameter(Mandatory=$true)][string]$Backup,[Parameter(Mandatory=$true)][string]$OutputDirectory)
$ErrorActionPreference='Stop'
if((Get-FileHash -LiteralPath $Backup).Hash -ne '7EBDD0F8F9EEBB6805111A96805D6564E66BB049E0096422D9B88682FF5C1ACD'){throw 'Unexpected backup; refusing assumed flash layout'}
if(Test-Path -LiteralPath $OutputDirectory){throw 'Use a new output directory'}
New-Item -ItemType Directory -Path $OutputDirectory | Out-Null
# Mechanical binary extraction; source backup stays untouched. eagle.flash.4m1m.ld.
$data=[IO.File]::ReadAllBytes($Backup)
$slice=New-Object byte[] 1024000
[Array]::Copy($data,0x300000,$slice,0,1024000)
[IO.File]::WriteAllBytes((Join-Path $OutputDirectory 'filesystem.bin'),$slice)
