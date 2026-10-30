# M2 voice prototype test: headless host + client over Relay and Vivox. The host feeds a WAV file as its
# microphone into the proximity channel; the client stands -Distances metres away (mic muted) and logs the
# energy it receives. Both send one Thai text message. Throwaway with the prototype.
# Usage: powershell -ExecutionPolicy Bypass -File Tools\proto-voice-test.ps1 -Wav C:\path\speech48k.wav [-Seconds 45]
param([Parameter(Mandatory)] [string]$Wav, [int]$Seconds = 45, [int[]]$Distances = @(0, 15, 40))

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$exe = Get-ChildItem (Join-Path $root 'Builds') -Filter 'SpiritMovers.exe' -Recurse |
    Where-Object { $_.DirectoryName -like '*Proto-NetSync-*' } | Select-Object -First 1
if (-not $exe) { throw 'Build the prototype first (NetSyncProtoSetup.CreateAndBuild).' }
$logs = Join-Path $root 'Logs\proto-voice'
New-Item -ItemType Directory -Force -Path $logs | Out-Null

foreach ($d in $Distances) {
    $code = Join-Path $logs "code-$d.txt"
    Remove-Item $code -ErrorAction SilentlyContinue
    $common = @('-batchmode', '-nographics', '-proto-codefile', $code, '-proto-props', '5')
    $h = Start-Process $exe.FullName -PassThru -ArgumentList ($common + @('-logFile', (Join-Path $logs "host-$d.log"),
        '-proto-role', 'host', '-proto-profile', "vh$d", '-proto-seconds', "$Seconds",
        '-proto-voice-inject', "`"$Wav`"", '-proto-chat', '"สวัสดี ปู่ฟื้นน้ำ"'))
    Start-Sleep -Seconds 2
    $c = Start-Process $exe.FullName -PassThru -ArgumentList ($common + @('-logFile', (Join-Path $logs "client-$d.log"),
        '-proto-role', 'client', '-proto-profile', "vc$d", '-proto-seconds', "$($Seconds - 5)", '-proto-bot-offset', "$d",
        '-proto-chat', '"ตอบทางวิทยุ ญาติผู้ใหญ่"'))
    $h.WaitForExit(($Seconds + 60) * 1000) | Out-Null
    $c.WaitForExit(30000) | Out-Null
    foreach ($p in @($h, $c)) { if (-not $p.HasExited) { Stop-Process -Id $p.Id -Force } }

    $clientLog = Join-Path $logs "client-$d.log"
    $peaks = Select-String -Path $clientLog -Pattern 'proximity:vh\d+=([\d.]+)' -AllMatches |
        ForEach-Object { $_.Matches } | ForEach-Object { [double]$_.Groups[1].Value }
    $peak = if ($peaks) { ($peaks | Measure-Object -Maximum).Maximum } else { 'n/a' }
    $texts = (Select-String -Path $clientLog -Pattern '\[Voice\] text from vh').Count
    Write-Host ("distance={0,3}m clientPeakProximityEnergy={1} hostTextReceived={2}" -f $d, $peak, $texts)
}
