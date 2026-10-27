$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
if (-not ('Tab5ShortPath' -as [type])) {
    Add-Type 'using System; using System.Text; using System.Runtime.InteropServices; public static class Tab5ShortPath { [DllImport("kernel32.dll", CharSet=CharSet.Unicode)] public static extern uint GetShortPathName(string p, StringBuilder b, uint n); }'
}
$shortBuffer = [System.Text.StringBuilder]::new(4096)
if ([Tab5ShortPath]::GetShortPathName($projectRoot, $shortBuffer, 4096) -eq 0) { throw 'Cannot resolve project path' }
$idfProjectRoot = $shortBuffer.ToString()
if ($idfProjectRoot.Contains(' ')) { throw 'ESP-IDF requires a path without spaces; Windows short paths are unavailable.' }
$env:IDF_PATH = Join-Path $idfProjectRoot '.tools\esp-idf'
# GCC locates its C++ multilib headers relative to its executable. Resolve the
# shared .tools junction first, then use the physical tools hierarchy for
# compiler-relative multilib header lookups.
$toolDirectory = Get-Item -LiteralPath (Join-Path $projectRoot '.tools') -Force
if ($toolDirectory.Attributes -band [IO.FileAttributes]::ReparsePoint) {
    $toolDirectory = $toolDirectory.ResolveLinkTarget($true)
}
$physicalToolsPath = Join-Path $toolDirectory.FullName 'idf-tools'
$shortBuffer.Clear() | Out-Null
if ([Tab5ShortPath]::GetShortPathName($physicalToolsPath, $shortBuffer, 4096) -eq 0) { throw 'Cannot resolve physical IDF tools path' }
$env:IDF_TOOLS_PATH = $shortBuffer.ToString()
function Get-Tab5ToolDirectory([string]$directory) {
    $toolBuffer = [System.Text.StringBuilder]::new(4096)
    if ([Tab5ShortPath]::GetShortPathName($directory, $toolBuffer, 4096) -eq 0) { throw 'Cannot resolve tool directory' }
    return $toolBuffer.ToString()
}
$env:IDF_TARGET = 'esp32p4'
$env:ESP_IDF_VERSION = '5.4'
