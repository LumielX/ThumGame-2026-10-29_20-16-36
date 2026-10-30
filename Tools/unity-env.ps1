# Shared paths for the Unity command-line scripts. Override UNITY_EXE to use another Editor install.
$ErrorActionPreference = 'Stop'

$Script:ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$Script:UnityExe = if ($env:UNITY_EXE) { $env:UNITY_EXE } else { 'X:\TheUnity\Editors\6000.3.27f1\Editor\Unity.exe' }
$Script:LogDir = Join-Path $ProjectRoot 'Logs'

if (-not (Test-Path $UnityExe)) {
    throw "Unity Editor not found at '$UnityExe'. Set the UNITY_EXE environment variable."
}
New-Item -ItemType Directory -Force -Path $LogDir | Out-Null

function Test-UnityProjectOpen {
    # Unity holds Temp/UnityLockfile while the project is open in the Editor; batch mode then fails.
    $lock = Join-Path $ProjectRoot 'Temp\UnityLockfile'
    if (-not (Test-Path $lock)) { return $false }
    try { [IO.File]::Open($lock, 'Open', 'ReadWrite', 'None').Close(); return $false } catch { return $true }
}
