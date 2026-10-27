param(
    [string]$FirmwareRoot = (Join-Path $PSScriptRoot '..\firmware')
)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path -LiteralPath $FirmwareRoot).Path
$csv = Join-Path $root 'partitions.csv'
$parts = @{}
foreach ($line in Get-Content -LiteralPath $csv) {
    if ($line.Trim().StartsWith('#') -or [string]::IsNullOrWhiteSpace($line)) { continue }
    $cols = @($line.Split(',') | ForEach-Object { $_.Trim() })
    if ($cols.Count -ne 5 -or $parts.ContainsKey($cols[0])) { throw "Invalid or duplicate partition: $line" }
    $parts[$cols[0]] = [pscustomobject]@{
        Name = $cols[0]; Type = $cols[1]; Subtype = $cols[2]
        Offset = [Convert]::ToInt64($cols[3].Replace('0x', ''), 16)
        Size = [Convert]::ToInt64($cols[4].Replace('0x', ''), 16)
    }
}
foreach ($name in @('nvs', 'phy_init', 'otadata', 'ota_0', 'ota_1', 'assets')) {
    if (!$parts.ContainsKey($name)) { throw "Missing partition: $name" }
}
if ($parts.Count -ne 6) { throw 'Unexpected partition added to migration layout' }
$expected = @{
    nvs = @(0x9000, 0x6000); phy_init = @(0xF000, 0x1000)
    otadata = @(0x10000, 0x2000); ota_0 = @(0x20000, 0x6e0000)
    ota_1 = @(0x700000, 0x6e0000); assets = @(0xE00000, 0x200000)
}
foreach ($name in $expected.Keys) {
    $p = $parts[$name]
    if ($p.Offset -ne $expected[$name][0] -or $p.Size -ne $expected[$name][1]) {
        throw "Partition $name moved; NVS/assets preservation or OTA slot geometry is no longer proven"
    }
}
if ($parts.otadata.Type -ne 'data' -or $parts.otadata.Subtype -ne 'ota' -or
    $parts.ota_0.Type -ne 'app' -or $parts.ota_0.Subtype -ne 'ota_0' -or
    $parts.ota_1.Type -ne 'app' -or $parts.ota_1.Subtype -ne 'ota_1') {
    throw 'OTA partition types are invalid'
}
$ordered = @($parts.Values | Sort-Object Offset)
for ($i = 0; $i -lt $ordered.Count; $i++) {
    if ($ordered[$i].Offset + $ordered[$i].Size -gt 0x1000000) { throw 'Partition exceeds 16 MB flash' }
    if ($i -gt 0 -and $ordered[$i - 1].Offset + $ordered[$i - 1].Size -gt $ordered[$i].Offset) {
        throw 'Partitions overlap'
    }
}
$image = Join-Path $root 'build\aibot_tab5.bin'
if (Test-Path -LiteralPath $image) {
    $bytes = (Get-Item -LiteralPath $image).Length
    if ($bytes -gt $parts.ota_0.Size) { throw "Application image $bytes bytes exceeds OTA slot" }
    Write-Output "OTA_LAYOUT_OK image=$bytes slot=$($parts.ota_0.Size) nvs=unchanged assets=requires_verified_copy"
} else {
    Write-Output 'OTA_LAYOUT_OK image=not_built nvs=unchanged assets=requires_verified_copy'
}
