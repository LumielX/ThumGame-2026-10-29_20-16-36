# M4A session-flow test over Unity Relay with the real menus (Development build of the game, DevFlowBot).
# Normal run: a headless host creates a room, N clients join by code, everyone readies, two jobs (the first ends at the
# van, the host quits during the second); one extra "late" client tries to join during the first job and must be refused.
# Passes when the host did 2 jobs, every client did 2 jobs and landed on the menu with "session.host_left_job" and the
# same pay, and the late client got "error.in_job". M4B: everyone sees the names in the van, each bot saves in its own
# profile folder, and the saved money grew by exactly this player's pay (host: job 1; clients: job 1 + host-left pay).
# -VersionMismatch: a host and one client pretending to be v0.0.0; passes when the client gets "error.version" and the
# host's van stays with one player.
# Usage: powershell -ExecutionPolicy Bypass -File Tools\m4a-flow-test.ps1 [-Runs 3] [-Clients 1] [-LatencyMs 0] [-Seconds 150] [-VersionMismatch]
param([int]$Runs = 3, [int]$Clients = 1, [int]$LatencyMs = 0, [int]$Seconds = 150, [switch]$VersionMismatch)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$exe = Get-ChildItem (Join-Path $root 'Builds') -Filter 'SpiritMovers.exe' -Recurse |
    Where-Object { $_.DirectoryName -like '*SpiritMovers-v*-Windows-Dev' } | Sort-Object LastWriteTime -Descending | Select-Object -First 1
if (-not $exe) { throw 'Build the game first: Tools\build.ps1 -Development (no -nographics).' }
$logs = Join-Path $root 'Logs\m4a'
New-Item -ItemType Directory -Force -Path $logs | Out-Null

function Get-Summary([string]$log) {
    $line = Select-String -Path $log -Pattern '\[M4A\] SUMMARY' -ErrorAction SilentlyContinue | Select-Object -Last 1
    if (-not $line) { return $null }
    $fields = @{}
    foreach ($m in [regex]::Matches($line.Line, '(\w+)=(\S+)')) { $fields[$m.Groups[1].Value] = $m.Groups[2].Value }
    return $fields
}

function Get-BotName([string]$role) { 'Bot' + (Get-Culture).TextInfo.ToTitleCase(($role -replace '-', '')) }

function Start-Bot([string]$role, [string]$tag, [string]$code, [string[]]$extra) {
    $log = Join-Path $logs "$role-$tag.log"
    # Plain ASCII names so the log check does not depend on how PowerShell decodes the log (Thai names: PlayMode tests).
    $argList = @('-batchmode', '-nographics', '-logFile', $log, '-dev-flow', ($role -replace '\d+$', ''), '-dev-codefile', $code,
        '-dev-profile', "m4a$role$tag", '-dev-seconds', "$Seconds", '-dev-latency', "$LatencyMs", '-dev-name', (Get-BotName $role)) + $extra
    $process = Start-Process $exe.FullName -PassThru -ArgumentList $argList
    return @{ Process = $process; Log = $log; Role = $role }
}

$failed = 0
for ($run = 1; $run -le $Runs; $run++) {
    $tag = if ($VersionMismatch) { "v$run" } else { "r$run-c$Clients" }
    $code = Join-Path $logs "code-$tag.txt"
    Remove-Item $code, "$code.job", (Join-Path $logs "*-$tag.log") -ErrorAction SilentlyContinue
    $bots = @()
    if ($VersionMismatch) {
        $bots += Start-Bot 'host-idle' $tag $code @('-dev-players', '1')
        Start-Sleep -Seconds 2
        $bots += Start-Bot 'client' $tag $code @('-dev-version', '0.0.0')
    } else {
        $players = 1 + $Clients
        $bots += Start-Bot 'host' $tag $code @('-dev-players', "$players")
        Start-Sleep -Seconds 2
        for ($c = 1; $c -le $Clients; $c++) { $bots += Start-Bot "client$c" $tag $code @() }
        $bots += Start-Bot 'late' $tag $code @()
    }
    foreach ($b in $bots) { $b.Process.WaitForExit(($Seconds + 60) * 1000) | Out-Null }
    foreach ($b in $bots) { if (-not $b.Process.HasExited) { Stop-Process -Id $b.Process.Id -Force } }

    $summaries = @($bots | ForEach-Object { Get-Summary $_.Log })
    $ok = @($summaries | Where-Object { $_ -eq $null }).Count -eq 0
    if ($ok) {
        $ok = @($summaries | Where-Object { $_.ok -ne 'true' }).Count -eq 0
        $clientSummaries = @($summaries | Where-Object { $_.role -eq 'client' })
        if (-not $VersionMismatch -and @($clientSummaries | ForEach-Object { "$($_.message)/$($_.pay)/$($_.jobs)" } | Select-Object -Unique).Count -ne 1) { $ok = $false }
        # Names (M4B): every player in the van sees the host and itself; the host sees every client.
        if (-not $VersionMismatch) {
            $hostSummary = @($summaries | Where-Object { $_.role -eq 'host' })[0]
            foreach ($s in $clientSummaries) {
                $seen = @($s.names -split ',')
                if ($seen -notcontains 'BotHost' -or $seen -notcontains $s.name) { $ok = $false }
                if (@($hostSummary.names -split ',') -notcontains $s.name) { $ok = $false }
            }
        }
        # Saved money (M4B): the change must be exactly what this player was paid, saved in its own profile folder.
        foreach ($s in $summaries) {
            $gain = [int]$s.moneyAfter - [int]$s.moneyBefore
            $jobPay = [Math]::Max(0, [int]$s.jobpay)
            $expected = switch ($s.role) {
                'host' { $jobPay }
                'client' { if ($VersionMismatch) { 0 } else { $jobPay + [Math]::Max(0, [int]$s.pay) } }
                default { 0 }
            }
            if ($s.moneyBefore -eq $null -or $gain -ne $expected) { $ok = $false }
            if ($s.saved -ne 'true') { $ok = $false }
        }
    }
    $detail = ($bots | ForEach-Object { $s = Get-Summary $_.Log; if ($s) { "$($_.Role): jobs=$($s.jobs) refused=$($s.refused) message=$($s.message) pay=$($s.pay) names=$($s.names) money=$($s.moneyBefore)->$($s.moneyAfter) jobpay=$($s.jobpay)" } else { "$($_.Role): NO SUMMARY" } }) -join ' | '
    Write-Host ("run {0} ({1}): {2}  {3}" -f $run, $tag, $(if ($ok) { 'PASS' } else { 'FAIL' }), $detail)
    if (-not $ok) {
        $failed++
        foreach ($b in $bots) {
            $s = Get-Summary $b.Log
            Write-Host ("    {0}: {1}" -f (Split-Path $b.Log -Leaf), $(if ($s) { ($s.GetEnumerator() | Sort-Object Name | ForEach-Object { "$($_.Name)=$($_.Value)" }) -join ' ' } else { 'NO SUMMARY' }))
        }
    }
}
Write-Host ("{0}/{1} runs passed" -f ($Runs - $failed), $Runs)
if ($failed -gt 0) { exit 1 }
