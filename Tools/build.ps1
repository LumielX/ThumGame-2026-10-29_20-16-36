# Builds the Windows 64-bit player and zips it for GitHub Releases.
# Usage: powershell -ExecutionPolicy Bypass -File Tools\build.ps1 [-NoZip] [-Development]
# -Development: Development build for the dev bots (Builds/SpiritMovers-v<ver>-Windows-Dev, never zipped or released).
param([switch]$NoZip, [switch]$Development)

. (Join-Path $PSScriptRoot 'unity-env.ps1')

if (Test-UnityProjectOpen) { throw 'The project is open in the Unity Editor. Close it before building from the command line.' }

$log = Join-Path $LogDir 'build.log'
Write-Host "Building... (log: $log)"
# No -nographics: without a graphics device, materials created by scripts render magenta in the player (M3B finding).
& $UnityExe -batchmode -quit -projectPath $ProjectRoot `
    -executeMethod "SpiritMovers.Editor.BuildScript.$(if ($Development) { 'BuildWindowsDev' } else { 'BuildWindows' })" -logFile $log | Out-Null
$code = $LASTEXITCODE

$resultLine = Select-String -Path $log -Pattern '^\[Build\] ' | Select-Object -Last 1
if ($resultLine) { Write-Host $resultLine.Line }
Select-String -Path $log -Pattern 'error CS\d+' | Select-Object -First 20 | ForEach-Object { Write-Host $_.Line }

if ($code -ne 0) { throw "Unity exited with code $code. See $log" }

$buildDir = Get-ChildItem (Join-Path $ProjectRoot 'Builds') -Directory -Filter "SpiritMovers-v*-Windows$(if ($Development) { '-Dev' })" |
    Sort-Object LastWriteTime -Descending | Select-Object -First 1
if (-not $buildDir) { throw 'Build folder not found under Builds/.' }

if (-not $NoZip -and -not $Development) {
    $zip = "$($buildDir.FullName).zip"
    if (Test-Path $zip) { Remove-Item $zip }
    Compress-Archive -Path (Join-Path $buildDir.FullName '*') -DestinationPath $zip
    Write-Host "Zipped: $zip"
}
Write-Host "Build OK: $($buildDir.FullName)"
