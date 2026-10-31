# M3C multi-instance job test (M3 acceptance): a headless host and N clients join over Unity Relay, the host
# starts a Normal job with a seed, every bot puts its share of items back and rings the bells, and each peer
# prints a [M3C] SUMMARY line. A run passes when every peer reports ok=true and all peers agree on the ending,
# accepted rooms, pay and stars (no desync). Exits 1 if any run fails.
# Usage: powershell -ExecutionPolicy Bypass -File Tools\m3c-job-test.ps1 [-Runs 10] [-Clients 1] [-Seconds 55]
param([int]$Runs = 10, [int]$Clients = 1, [int]$Seconds = 55, [int]$FirstSeed = 1)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$exe = Get-ChildItem (Join-Path $root 'Builds') -Filter 'SpiritMovers.exe' -Recurse |
    Where-Object { $_.DirectoryName -like '*M3-TestHouse-*' } | Select-Object -First 1
if (-not $exe) { throw 'Build the test house first (TestRoomSetup.BuildTestHouse, without -nographics).' }
$logs = Join-Path $root 'Logs\m3c'
New-Item -ItemType Directory -Force -Path $logs | Out-Null

function Get-Summary([string]$log) {
    $line = Select-String -Path $log -Pattern '\[M3C\] SUMMARY' -ErrorAction SilentlyContinue | Select-Object -Last 1
    if (-not $line) { return $null }
    $fields = @{}
    foreach ($m in [regex]::Matches($line.Line, '(\w+)=(\S+)')) { $fields[$m.Groups[1].Value] = $m.Groups[2].Value }
    return $fields
}

$players = 1 + $Clients
$failed = 0
for ($run = 0; $run -lt $Runs; $run++) {
    $seed = $FirstSeed + $run
    $tag = "s$seed-p$players"
    $code = Join-Path $logs "code-$tag.txt"
    Remove-Item $code -ErrorAction SilentlyContinue
    $common = @('-batchmode', '-nographics', '-dev-codefile', $code, '-dev-scenario', 'job', '-dev-seed', "$seed", '-dev-players', "$players")
    $procs = @()
    $logFiles = @()
    $hostLog = Join-Path $logs "host-$tag.log"
    $procs += Start-Process $exe.FullName -PassThru -ArgumentList ($common + @('-logFile', $hostLog,
        '-dev-role', 'host', '-dev-profile', "m3ch$tag", '-dev-seconds', "$Seconds"))
    $logFiles += $hostLog
    Start-Sleep -Seconds 2
    for ($c = 1; $c -le $Clients; $c++) {
        $clientLog = Join-Path $logs "client$c-$tag.log"
        $procs += Start-Process $exe.FullName -PassThru -ArgumentList ($common + @('-logFile', $clientLog,
            '-dev-role', 'client', '-dev-profile', "m3cc$c$tag", '-dev-seconds', "$($Seconds - 3)"))
        $logFiles += $clientLog
    }
    foreach ($p in $procs) { $p.WaitForExit(($Seconds + 60) * 1000) | Out-Null }
    foreach ($p in $procs) { if (-not $p.HasExited) { Stop-Process -Id $p.Id -Force } }

    $summaries = $logFiles | ForEach-Object { Get-Summary $_ }
    $ok = ($summaries | Where-Object { $_ -eq $null }).Count -eq 0
    if ($ok) {
        $ok = ($summaries | Where-Object { $_.ok -ne 'true' }).Count -eq 0
        foreach ($key in 'ending', 'accepted', 'teamTotal', 'stars') {
            if (($summaries | ForEach-Object { $_[$key] } | Select-Object -Unique).Count -ne 1) { $ok = $false }
        }
    }
    $first = $summaries | Where-Object { $_ } | Select-Object -First 1
    $detail = if ($first) { "ending=$($first.ending) accepted=$($first.accepted) teamTotal=$($first.teamTotal) stars=$($first.stars) angerStage=$($first.angerStage)" } else { 'NO SUMMARY' }
    Write-Host ("seed {0,2} players {1}: {2}  {3}" -f $seed, $players, $(if ($ok) { 'PASS' } else { 'FAIL' }), $detail)
    if (-not $ok) {
        $failed++
        for ($i = 0; $i -lt $logFiles.Count; $i++) {
            $s = $summaries[$i]
            Write-Host ("    {0}: {1}" -f (Split-Path $logFiles[$i] -Leaf), $(if ($s) { ($s.GetEnumerator() | Sort-Object Name | ForEach-Object { "$($_.Name)=$($_.Value)" }) -join ' ' } else { 'NO SUMMARY' }))
        }
    }
}
Write-Host ("{0}/{1} runs passed" -f ($Runs - $failed), $Runs)
if ($failed -gt 0) { exit 1 }
