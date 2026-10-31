# M3B two-instance carry test: a headless host and client join over Unity Relay and run bot scenarios
# in the M3 test room (see DevBot), then each prints a [M3B] SUMMARY line. -LatencyMs adds simulated delay
# on both peers. Exits 1 if any scenario fails.
# Usage: powershell -ExecutionPolicy Bypass -File Tools\m3b-carry-test.ps1 [-Seconds 40] [-LatencyMs 0] [-Scenarios carry,shared]
param([int]$Seconds = 40, [int]$LatencyMs = 0,
    [string[]]$Scenarios = @('carry', 'shared', 'split', 'remote', 'disconnect', 'crash'))

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$exe = Get-ChildItem (Join-Path $root 'Builds') -Filter 'SpiritMovers.exe' -Recurse |
    Where-Object { $_.DirectoryName -like '*M3-TestRoom-*' } | Select-Object -First 1
if (-not $exe) { throw 'Build the test room first (TestRoomSetup.BuildTestRoom).' }
$logs = Join-Path $root 'Logs\m3b'
New-Item -ItemType Directory -Force -Path $logs | Out-Null

function Get-Summary([string]$log) {
    $line = Select-String -Path $log -Pattern '\[M3B\] SUMMARY' -ErrorAction SilentlyContinue | Select-Object -Last 1
    if (-not $line) { return $null }
    $fields = @{}
    foreach ($m in [regex]::Matches($line.Line, '(\w+)=(\([^)]*\)|\S+)')) { $fields[$m.Groups[1].Value] = $m.Groups[2].Value }
    return $fields
}

function Get-Position([string]$text) {
    $parts = $text.Trim('(', ')').Split(',') | ForEach-Object { [double]::Parse($_, [Globalization.CultureInfo]::InvariantCulture) }
    return , $parts
}

$failed = $false
foreach ($scenario in $Scenarios) {
    $code = Join-Path $logs "code-$scenario.txt"
    Remove-Item $code -ErrorAction SilentlyContinue
    $hostLog = Join-Path $logs "host-$scenario.log"
    $clientLog = Join-Path $logs "client-$scenario.log"
    $common = @('-batchmode', '-nographics', '-dev-codefile', $code, '-dev-scenario', $scenario, '-dev-latency', "$LatencyMs")
    # A crashed client is only noticed when the transport times out (30 s), so the host waits longer.
    $hostSeconds = if ($scenario -eq 'crash') { $Seconds + 40 } else { $Seconds }
    $h = Start-Process $exe.FullName -PassThru -ArgumentList ($common + @('-logFile', $hostLog,
        '-dev-role', 'host', '-dev-profile', "m3bh$scenario", '-dev-seconds', "$hostSeconds"))
    Start-Sleep -Seconds 2
    $c = Start-Process $exe.FullName -PassThru -ArgumentList ($common + @('-logFile', $clientLog,
        '-dev-role', 'client', '-dev-profile', "m3bc$scenario", '-dev-seconds', "$($Seconds - 5)"))
    $h.WaitForExit(($hostSeconds + 60) * 1000) | Out-Null
    $c.WaitForExit(30000) | Out-Null
    foreach ($p in @($h, $c)) { if (-not $p.HasExited) { Stop-Process -Id $p.Id -Force } }

    $hs = Get-Summary $hostLog
    $cs = Get-Summary $clientLog
    # The crashed client never writes a summary; only the host's view counts there.
    $ok = if ($scenario -eq 'crash') { $hs -and $hs.ok -eq 'true' } else { $hs -and $cs -and $hs.ok -eq 'true' -and $cs.ok -eq 'true' }
    $note = ''
    if ($ok -and $scenario -eq 'carry') {
        $a = Get-Position $hs.itemPos; $b = Get-Position $cs.itemPos
        $gap = [math]::Sqrt([math]::Pow($a[0] - $b[0], 2) + [math]::Pow($a[1] - $b[1], 2) + [math]::Pow($a[2] - $b[2], 2))
        $note = ' hostClientGap={0:F3}m' -f $gap
        if ($gap -ge 0.15) { $ok = $false }
    }
    foreach ($pair in @(@('host', $hs), @('client', $cs))) {
        $s = $pair[1]
        $text = if ($s) { ($s.GetEnumerator() | Sort-Object Name | ForEach-Object { "$($_.Name)=$($_.Value)" }) -join ' ' } else { 'NO SUMMARY' }
        Write-Host ("{0,-10} {1,-6} {2}" -f $scenario, $pair[0], $text)
    }
    Write-Host ("{0,-10} => {1}{2}" -f $scenario, $(if ($ok) { 'PASS' } else { 'FAIL' }), $note)
    if (-not $ok) { $failed = $true }
}
if ($failed) { exit 1 }
