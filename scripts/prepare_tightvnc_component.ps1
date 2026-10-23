param([Parameter(Mandatory)][string]$StageDirectory,[Parameter(Mandatory)][string]$CacheDirectory)
$ErrorActionPreference='Stop'
$ProgressPreference='SilentlyContinue'
$stage=[IO.Path]::GetFullPath($StageDirectory)
$cache=[IO.Path]::GetFullPath($CacheDirectory)
$destination=Join-Path $stage 'components/tightvnc'
New-Item -ItemType Directory -Force $cache,$destination|Out-Null
$items=@(
    @{Name='tightvnc-2.8.90-gpl-setup-64bit.msi';Sha256='9693cb4cdc2e7e482e8e3f5890415b57ef87de3369fa5c337db516218a1170ce'},
    @{Name='tightvnc-2.8.90-src-gpl.zip';Sha256='f3f76393a5865e1f1919c78c3eb43b9c937cf738927c2bfc920ecbd3913b061c'}
)
foreach($item in $items) {
    $path=Join-Path $cache $item.Name
    if(!(Test-Path -LiteralPath $path)) {
        $partial=$path+'.'+[Guid]::NewGuid().ToString('N')+'.download'
        try {
            Invoke-WebRequest -UseBasicParsing -Uri ('https://www.tightvnc.com/download/2.8.90/'+$item.Name) -OutFile $partial -TimeoutSec 120
            if((Get-FileHash $partial).Hash -ne $item.Sha256){throw 'TightVNC download hash mismatch'}
            Move-Item -LiteralPath $partial -Destination $path
        }finally{if(Test-Path -LiteralPath $partial){Remove-Item -LiteralPath $partial}}
    }
    if((Get-FileHash $path).Hash -ne $item.Sha256){throw 'TightVNC cached payload hash mismatch'}
    if($item.Name.EndsWith('.msi')) {
        $signature=Get-AuthenticodeSignature -LiteralPath $path
        if($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notmatch '(^|, )CN=OOO GlavSoft(,|$)'){throw 'TightVNC installer signature mismatch'}
    }
    Copy-Item -LiteralPath $path -Destination (Join-Path $destination $item.Name) -Force
    if((Get-FileHash (Join-Path $destination $item.Name)).Hash -ne $item.Sha256){throw 'TightVNC staged payload mismatch'}
}
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive=[IO.Compression.ZipFile]::OpenRead((Join-Path $destination $items[1].Name))
try {
    $entry=$archive.GetEntry('tightvnc-2.8.90-gpl/wix-installer/LICENSE.txt')
    if($null -eq $entry){throw 'GPL license missing from official source'}
    [IO.Compression.ZipFileExtensions]::ExtractToFile($entry,(Join-Path $destination 'LICENSE.txt'),$true)
}finally{$archive.Dispose()}
Copy-Item -LiteralPath (Join-Path (Split-Path $PSScriptRoot) 'licenses/tightvnc/ORIGIN.md') -Destination (Join-Path $destination 'ORIGIN.md') -Force
[ordered]@{component='TightVNC Server';version='2.8.90.0';optional=$true;bundled=$true;offline=$true;source='https://www.tightvnc.com/download/';license='GPL-2.0';artifacts=$items;productCode='{AFF73242-938E-4D86-B133-6A287F7E4A04}';configuration='Authenticated loopback-only service; no Viewer or firewall exception'}|ConvertTo-Json -Depth 6|Set-Content (Join-Path $destination 'ORIGIN.json') -Encoding UTF8
'TIGHTVNC_OFFLINE_COMPONENT_OK signed official MSI, matching source and license'
