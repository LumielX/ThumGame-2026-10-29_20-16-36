# M4C voice and comms test over Unity Relay + Vivox with the real game (Development build, DevVoiceBot).
# The host speaks from a generated test WAV (a tone that rises and falls like speech — no outside audio file); the
# client keeps its microphone shut and measures what reaches it. Phases: van (group voice + Thai chat both ways),
# house near (heard) and 40 m away (not heard), radio through both walkie-talkies (heard through the tap, silent once
# dropped), the host's microphone reaching the ghost (at most 4 a second), and a ping + quick message from the client.
# -VoiceFail: the client joins with voice refused and must still reach the job; pings still arrive.
# Usage: powershell -ExecutionPolicy Bypass -File Tools\m4c-voice-test.ps1 [-Runs 3] [-LatencyMs 0] [-Seconds 150] [-VoiceFail]
param([int]$Runs = 3, [int]$LatencyMs = 0, [int]$Seconds = 150, [switch]$VoiceFail)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$exe = Get-ChildItem (Join-Path $root 'Builds') -Filter 'SpiritMovers.exe' -Recurse |
    Where-Object { $_.DirectoryName -like '*SpiritMovers-v*-Windows-Dev' } | Sort-Object LastWriteTime -Descending | Select-Object -First 1
if (-not $exe) { throw 'Build the game first: Tools\build.ps1 -Development (no -nographics).' }
$logs = Join-Path $root 'Logs\m4c'
New-Item -ItemType Directory -Force -Path $logs | Out-Null

# 16-bit mono 48 kHz, 200 s: two tones under a 3 Hz "syllable" envelope, loud enough for Vivox to call it speech.
$wav = Join-Path $logs 'voice-test-48k.wav'
if (-not (Test-Path $wav)) {
    $rate = 48000; $seconds = 200; $count = $rate * $seconds
    $data = New-Object byte[] ($count * 2)
    for ($i = 0; $i -lt $count; $i++) {
        $t = $i / $rate
        $envelope = [Math]::Abs([Math]::Sin(2 * [Math]::PI * 3 * $t))
        $sample = 0.7 * $envelope * (0.6 * [Math]::Sin(2 * [Math]::PI * 220 * $t) + 0.4 * [Math]::Sin(2 * [Math]::PI * 660 * $t))
        $value = [int16][Math]::Round($sample * 32767)
        $data[2 * $i] = [byte]($value -band 0xFF)
        $data[2 * $i + 1] = [byte](($value -shr 8) -band 0xFF)
    }
    $stream = [IO.File]::Create($wav)
    $writer = New-Object IO.BinaryWriter($stream)
    $writer.Write([Text.Encoding]::ASCII.GetBytes('RIFF')); $writer.Write([int](36 + $data.Length))
    $writer.Write([Text.Encoding]::ASCII.GetBytes('WAVEfmt ')); $writer.Write([int]16); $writer.Write([int16]1); $writer.Write([int16]1)
    $writer.Write([int]$rate); $writer.Write([int]($rate * 2)); $writer.Write([int16]2); $writer.Write([int16]16)
    $writer.Write([Text.Encoding]::ASCII.GetBytes('data')); $writer.Write([int]$data.Length); $writer.Write($data)
    $writer.Close()
}

function Get-Summary([string]$log) {
    $line = Select-String -Path $log -Pattern '\[M4C\] SUMMARY' -ErrorAction SilentlyContinue | Select-Object -Last 1
    if (-not $line) { return $null }
    $fields = @{}
    foreach ($m in [regex]::Matches($line.Line, '(\w+)=(\S+)')) { $fields[$m.Groups[1].Value] = $m.Groups[2].Value }
    return $fields
}

function Start-Bot([string]$role, [string]$tag, [string]$code, [string[]]$extra) {
    $log = Join-Path $logs "$role-$tag.log"
    $argList = @('-batchmode', '-nographics', '-logFile', $log, '-dev-voice', $role, '-dev-codefile', $code,
        '-dev-profile', "m4c$role$tag", '-dev-seconds', "$Seconds", '-dev-latency', "$LatencyMs", '-dev-name', "Bot$role") + $extra
    $process = Start-Process $exe.FullName -PassThru -ArgumentList $argList
    return @{ Process = $process; Log = $log; Role = $role }
}

function Num([string]$s) { [double]::Parse($s, [Globalization.CultureInfo]::InvariantCulture) }

$failed = 0
for ($run = 1; $run -le $Runs; $run++) {
    $tag = if ($VoiceFail) { "f$run" } else { "r$run-l$LatencyMs" }
    $code = Join-Path $logs "code-$tag.txt"
    Remove-Item $code, "$code.phase", (Join-Path $logs "*-$tag.log") -ErrorAction SilentlyContinue
    $bots = @()
    $bots += Start-Bot 'host' $tag $code @('-dev-wav', $wav)
    Start-Sleep -Seconds 2
    $bots += Start-Bot $(if ($VoiceFail) { 'client-fail' } else { 'client' }) $tag $code @()
    foreach ($b in $bots) { $b.Process.WaitForExit(($Seconds + 60) * 1000) | Out-Null }
    foreach ($b in $bots) { if (-not $b.Process.HasExited) { Stop-Process -Id $b.Process.Id -Force } }

    $h = Get-Summary $bots[0].Log
    $c = Get-Summary $bots[1].Log
    $problems = @()
    if (-not $h) { $problems += 'host: no summary' }
    if (-not $c) { $problems += 'client: no summary' }
    if ($h -and $c) {
        if ($h.failed -ne 'none') { $problems += "host failed at $($h.failed)" }
        if ($c.failed -ne 'none') { $problems += "client failed at $($c.failed)" }
        if ($c.job -ne 'true') { $problems += 'client never reached the job' }
        if ($h.ping -ne 'true') { $problems += 'ping did not reach the host' }
        if ($h.quick -ne 'true') { $problems += 'quick message did not reach the host' }
        if ($VoiceFail) {
            if ($c.voice -eq 'On') { $problems += 'client voice should have failed' }
        } else {
            if ($h.chat -ne 'true' -or $c.chat -ne 'true') { $problems += 'Thai chat not both ways' }
            if ((Num $c.van) -lt 0.3) { $problems += "van not heard ($($c.van))" }
            if ((Num $c.near) -lt 0.3) { $problems += "near not heard ($($c.near))" }
            if ((Num $c.far) -ge 0.1) { $problems += "40 m heard ($($c.far))" }
            if ((Num $c.radio) -lt 0.3) { $problems += "radio not heard ($($c.radio))" }
            if ($c.droppedMuted -ne 'true') { $problems += 'radio still heard after dropping the walkie' }
            if ([int]$h.noises -lt 1) { $problems += 'the ghost never heard the host microphone' }
            if ([int]$h.noisesPerSec -gt 4) { $problems += "ghost heard the mic $($h.noisesPerSec) times in a second" }
        }
    }
    $ok = $problems.Count -eq 0
    $detail = if ($h -and $c) { "van=$($c.van) near=$($c.near) far=$($c.far) radio=$($c.radio) dropped=$($c.droppedMuted) chat=$($h.chat)/$($c.chat) noises=$($h.noises)@$($h.noisesPerSec)/s ping=$($h.ping) quick=$($h.quick) clientVoice=$($c.voice)" } else { '' }
    Write-Host ("run {0} ({1}): {2}  {3}" -f $run, $tag, $(if ($ok) { 'PASS' } else { 'FAIL' }), $detail)
    if (-not $ok) { $failed++; $problems | ForEach-Object { Write-Host "    $_" } }
}
Write-Host ("{0}/{1} runs passed" -f ($Runs - $failed), $Runs)
if ($failed -gt 0) { exit 1 }
