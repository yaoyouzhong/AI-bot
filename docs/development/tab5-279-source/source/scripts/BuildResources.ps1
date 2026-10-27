# Process-local build policy. Nothing is installed or persisted in Windows.
function Get-Tab5BuildCpuPolicy {
    param([object[]]$CpuSets, [long]$AllowedMask)
    $available = @($CpuSets | Where-Object {
        $_.Group -eq 0 -and $_.LogicalProcessor -lt 64 -and
        ($AllowedMask -band ([long]1 -shl $_.LogicalProcessor)) -ne 0
    })
    if (-not $available.Count) { throw 'No allowed CPU is available for the TAB5 build.' }
    $classes = @($CpuSets | Where-Object Group -eq 0 | Select-Object -ExpandProperty EfficiencyClass -Unique)
    $efficient = @()
    if ($classes.Count -gt 1) {
        $lowest = ($classes | Measure-Object -Minimum).Minimum
        $efficient = @($available | Where-Object EfficiencyClass -eq $lowest)
    }
    # Use up to two performance cores plus efficiency cores, eight in total.
    # Keep physical cores available for the desktop; never count SMT twice.
    $seen = @{}
    $physical = @($available | Sort-Object LogicalProcessor -Descending | Where-Object {
        if ($seen.ContainsKey([string]$_.Core)) { $false }
        else { $seen[[string]$_.Core] = $true; $true }
    })
    if ($efficient.Count) {
        $performance = @($physical | Where-Object EfficiencyClass -ne $lowest | Select-Object -First 2)
        $efficiency = @($physical | Where-Object EfficiencyClass -eq $lowest | Select-Object -First (8 - $performance.Count))
        $selected = @(@($performance) + @($efficiency) | Sort-Object LogicalProcessor)
    } else {
        $budget = [Math]::Min(8, [Math]::Max(1, $physical.Count - 1))
        $selected = @($physical | Select-Object -First $budget | Sort-Object LogicalProcessor)
    }
    [long]$mask = 0
    foreach ($cpu in $selected) { $mask = $mask -bor ([long]1 -shl $cpu.LogicalProcessor) }
    [pscustomobject]@{
        AffinityMask = $mask
        LogicalProcessors = @($selected.LogicalProcessor)
        UsesEfficiencyCores = ($efficient.Count -gt 0)
        WorkerCount = $selected.Count
    }
}

function Get-Tab5BuildCpuSets {
    if (-not ('Tab5.BuildCpuTopology' -as [type])) {
        Add-Type @'
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
namespace Tab5 {
    public sealed class BuildCpuSet {
        public int Group, LogicalProcessor, Core, EfficiencyClass;
    }
    public static class BuildCpuTopology {
        [DllImport("kernel32.dll", SetLastError=true)]
        static extern bool GetSystemCpuSetInformation(IntPtr data, uint length,
            out uint returned, IntPtr process, uint flags);
        public static BuildCpuSet[] Read() {
            uint size;
            GetSystemCpuSetInformation(IntPtr.Zero, 0, out size, IntPtr.Zero, 0);
            if (size == 0) throw new Win32Exception();
            var buffer = Marshal.AllocHGlobal(checked((int)size));
            try {
                if (!GetSystemCpuSetInformation(buffer, size, out size, IntPtr.Zero, 0))
                    throw new Win32Exception();
                var result = new List<BuildCpuSet>();
                for (int offset = 0; offset < size;) {
                    var item = IntPtr.Add(buffer, offset);
                    int length = Marshal.ReadInt32(item);
                    if (length < 8 || offset + length > size) throw new InvalidOperationException("Invalid CPU topology record.");
                    if (Marshal.ReadInt32(item, 4) == 0 && length >= 32)
                        result.Add(new BuildCpuSet {
                            Group = (ushort)Marshal.ReadInt16(item, 12),
                            LogicalProcessor = Marshal.ReadByte(item, 14),
                            Core = Marshal.ReadByte(item, 15),
                            EfficiencyClass = Marshal.ReadByte(item, 18)
                        });
                    offset += length;
                }
                return result.ToArray();
            } finally { Marshal.FreeHGlobal(buffer); }
        }
    }
}
'@
    }
    [Tab5.BuildCpuTopology]::Read()
}

function Enter-Tab5BuildResourceScope {
    if (-not [OperatingSystem]::IsWindows()) { throw 'The TAB5 build resource policy requires Windows.' }
    $process = [Diagnostics.Process]::GetCurrentProcess()
    $originalMask = $process.ProcessorAffinity.ToInt64()
    $originalPriority = $process.PriorityClass
    try {
        try { $sets = @(Get-Tab5BuildCpuSets) }
        catch {
            Write-Warning "CPU topology unavailable; limiting the build to at most eight allowed logical CPUs: $_"
            $sets = @(0..63 | ForEach-Object {
                [pscustomobject]@{Group=0; LogicalProcessor=$_; Core=$_; EfficiencyClass=0}
            })
        }
        $policy = Get-Tab5BuildCpuPolicy -CpuSets $sets -AllowedMask $originalMask
        if (-not $policy.UsesEfficiencyCores) {
            Write-Warning 'No identifiable allowed efficiency cores; using at most eight allowed physical cores.'
        }
        $process.ProcessorAffinity = [IntPtr]$policy.AffinityMask
        $process.PriorityClass = [Diagnostics.ProcessPriorityClass]::BelowNormal
        Write-Host ('[TAB5 build] CPUs=' + ($policy.LogicalProcessors -join ',') +
            ' workers=' + $policy.WorkerCount + ' priority=BelowNormal efficiencyCores=' + $policy.UsesEfficiencyCores)
        [pscustomobject]@{Process=$process; OriginalMask=$originalMask; OriginalPriority=$originalPriority; Policy=$policy}
    } catch {
        try { $process.ProcessorAffinity = [IntPtr]$originalMask; $process.PriorityClass = $originalPriority }
        finally { $process.Dispose() }
        throw
    }
}

function Exit-Tab5BuildResourceScope {
    param([Parameter(Mandatory)]$Scope)
    try { $Scope.Process.ProcessorAffinity = [IntPtr]$Scope.OriginalMask }
    finally {
        try { $Scope.Process.PriorityClass = $Scope.OriginalPriority }
        finally { $Scope.Process.Dispose() }
    }
}
