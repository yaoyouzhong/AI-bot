param([ValidateSet('Painting','Calligraphy')][string]$Collection, [switch]$Download)
$ErrorActionPreference = 'Stop'
if (-not $Collection) {
    $choice = Read-Host 'Merge 1=Painting, 2=Calligraphy'
    if ($choice -eq '1') { $Collection = 'Painting' }
    elseif ($choice -eq '2') { $Collection = 'Calligraphy' }
    else { throw 'Select 1 or 2.' }
}
$baseDirectory = (Get-Location).Path
$manifest = [IO.File]::ReadAllText((Join-Path $baseDirectory 'HD-Gallery-parts.json')) | ConvertFrom-Json
if ($manifest.schema -ne 1) { throw 'Unsupported manifest schema.' }
$archive = @($manifest.archives | Where-Object { $_.name -like "AI-bot-Gallery-HD-$Collection-*.zip" -or $_.name -like "AI-bot-gallery-$Collection-hd-*.zip" })
if ($archive.Count -ne 1) { throw 'Collection missing or ambiguous.' }
$archive = $archive[0]
function SafePath([string]$name) {
    if ($name -notmatch '^[A-Za-z0-9][A-Za-z0-9_.-]*$') { throw 'Unsafe filename.' }
    return Join-Path $baseDirectory $name
}
function StreamHash($stream, [long]$length) {
    $sha = [Security.Cryptography.SHA256]::Create()
    $buffer = New-Object byte[] 1048576
    try {
        while ($length -gt 0) {
            $read = $stream.Read($buffer, 0, [int][Math]::Min([long]$buffer.Length, $length))
            if ($read -eq 0) { throw 'Unexpected end of file.' }
            [void]$sha.TransformBlock($buffer, 0, $read, $null, 0)
            $length -= $read
        }
        [void]$sha.TransformFinalBlock($buffer, 0, 0)
        return [BitConverter]::ToString($sha.Hash).Replace('-', '').ToLowerInvariant()
    } finally { $sha.Dispose() }
}
function FileHash([string]$path) {
    $input = [IO.File]::OpenRead($path)
    try { return StreamHash $input $input.Length } finally { $input.Dispose() }
}
function DownloadPart($part) {
    $path = SafePath $part.name
    if ([IO.File]::Exists($path)) { return } # Verified below; never overwrite a different existing part.
    $temporary = "$path.download"
    $baseUrl = 'https://github.com/yaoyouzhong/AI-bot/releases/download/tab5-v0.2.205-ui/'
    for ($attempt = 1; $attempt -le 4; $attempt++) {
        try {
            [long]$offset = if ([IO.File]::Exists($temporary)) { ([IO.FileInfo]$temporary).Length } else { 0 }
            if ($offset -gt $part.bytes) { throw 'Existing partial download is too large; preserved.' }
            if ($offset -lt $part.bytes) {
                $request = [Net.HttpWebRequest]::Create($baseUrl + $part.name)
                $request.UserAgent = 'AI-bot-HD-Gallery/1'
                $request.Timeout = 60000
                $request.ReadWriteTimeout = 60000
                if ($offset -gt 0) { $request.AddRange($offset) }
                $response = $request.GetResponse()
                try {
                    $append = $offset -gt 0 -and [int]$response.StatusCode -eq 206
                    if ($append -and $response.Headers['Content-Range'] -notlike "bytes $offset-*") { throw 'Unexpected range response.' }
                    $mode = if ($append) { [IO.FileMode]::Append } else { [IO.FileMode]::Create }
                    Write-Host "DOWNLOAD $($part.name) resume=$append offset=$offset"
                    $destination = [IO.File]::Open($temporary, $mode, [IO.FileAccess]::Write, [IO.FileShare]::None)
                    try { $response.GetResponseStream().CopyTo($destination); $destination.Flush($true) }
                    finally { $destination.Dispose() }
                } finally { $response.Dispose() }
            }
            if (([IO.FileInfo]$temporary).Length -ne $part.bytes -or (FileHash $temporary) -ne $part.sha256) { throw 'Partial download checksum mismatch; preserved.' }
            [IO.File]::Move($temporary, $path)
            return
        } catch {
            if ($attempt -eq 4) { throw }
            Write-Host "RETRY $($part.name) $attempt/4; completed parts are retained."
            Start-Sleep -Seconds (2 * $attempt)
        }
    }
}
$target = SafePath $archive.name
if ([IO.File]::Exists($target)) {
    if ((FileHash $target) -ne $archive.sha256) { throw 'Existing ZIP differs; it was not overwritten.' }
    Write-Host "ALREADY_VERIFIED $($archive.name)"
    exit 0
}
[long]$total = 0
foreach ($part in $archive.parts) {
    if ($Download) { DownloadPart $part }
    $path = SafePath $part.name
    if (-not [IO.File]::Exists($path)) { throw "Missing part: $($part.name)" }
    if (([IO.FileInfo]$path).Length -ne $part.bytes -or (FileHash $path) -ne $part.sha256) { throw "Bad part: $($part.name)" }
    $total += [long]$part.bytes
    Write-Host "PART_VERIFIED $($part.name)"
}
if ($total -ne $archive.bytes) { throw 'Manifest total mismatch.' }
$partial = "$target.partial"
$output = [IO.File]::Open($partial, [IO.FileMode]::OpenOrCreate, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
try {
    if ($output.Length -gt $total) { throw 'Existing partial file is too large.' }
    [long]$position = 0
    foreach ($part in $archive.parts) {
        if ($output.Length -ge $position + $part.bytes) {
            $output.Position = $position
            if ((StreamHash $output $part.bytes) -ne $part.sha256) { throw 'Existing partial differs; preserved for inspection.' }
            Write-Host "MERGE_RESUME_SKIP $($part.name)"
        } else {
            $output.SetLength($position)
            $output.Position = $position
            $input = [IO.File]::OpenRead((SafePath $part.name))
            try { $input.CopyTo($output) } finally { $input.Dispose() }
            $output.Flush($true)
            Write-Host "MERGED $($part.name)"
        }
        $position += [long]$part.bytes
    }
} finally { $output.Dispose() }
if ((FileHash $partial) -ne $archive.sha256) { throw 'Final ZIP checksum mismatch; partial preserved.' }
[IO.File]::Move($partial, $target)
Write-Host "ZIP_VERIFIED $($archive.name)"
Write-Host 'Import this ZIP in AI-bot. Original parts are retained.'
