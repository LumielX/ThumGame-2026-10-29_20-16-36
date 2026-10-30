# M2 network prototype test matrix: runs a headless host + client over Unity Relay for each
# carry mode and simulated latency, then prints the [Proto] SUMMARY lines. Throwaway with the prototype.
# Usage: powershell -ExecutionPolicy Bypass -File Tools\proto-netsync-matrix.ps1 [-Seconds 70]
param([int]$Seconds = 70, [string[]]$Modes = @('A', 'B'), [int[]]$Latencies = @(0, 100, 200))

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$exe = Get-ChildItem (Join-Path $root 'Builds') -Filter 'SpiritMovers.exe' -Recurse |
    Where-Object { $_.DirectoryName -like '*Proto-NetSync-*' } | Select-Object -First 1
if (-not $exe) { throw 'Build the prototype first (NetSyncProtoSetup.CreateAndBuild).' }
$logs = Join-Path $root 'Logs\proto'
New-Item -ItemType Directory -Force -Path $logs | Out-Null

foreach ($mode in $Modes) {
    foreach ($ms in $Latencies) {
        $tag = "$mode-$ms"
        $code = Join-Path $logs "code-$tag.txt"
        Remove-Item $code -ErrorAction SilentlyContinue
        $common = @('-batchmode', '-nographics', '-proto-codefile', $code, '-proto-latency', "$ms")
        $h = Start-Process $exe.FullName -PassThru -ArgumentList ($common + @('-logFile', (Join-Path $logs "host-$tag.log"),
            '-proto-role', 'host', '-proto-profile', "h$tag", '-proto-props', '40', '-proto-seconds', "$Seconds"))
        Start-Sleep -Seconds 2
        $c = Start-Process $exe.FullName -PassThru -ArgumentList ($common + @('-logFile', (Join-Path $logs "client-$tag.log"),
            '-proto-role', 'client', '-proto-profile', "c$tag", '-proto-carry', $mode, '-proto-seconds', "$($Seconds - 5)"))
        $h.WaitForExit(($Seconds + 60) * 1000) | Out-Null
        $c.WaitForExit(30000) | Out-Null
        foreach ($p in @($h, $c)) { if (-not $p.HasExited) { Stop-Process -Id $p.Id -Force } }

        foreach ($role in 'host', 'client') {
            $line = Select-String -Path (Join-Path $logs "$role-$tag.log") -Pattern '\[Proto\] SUMMARY' | Select-Object -Last 1
            Write-Host ("mode={0} latency={1,3}ms {2,-6} {3}" -f $mode, $ms, $role, ($(if ($line) { $line.Line -replace '^.*SUMMARY ', '' } else { 'NO SUMMARY' })))
        }
    }
}
