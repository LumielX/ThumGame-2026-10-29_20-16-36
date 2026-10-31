# Runs Unity tests from the command line and prints a summary.
# Usage: powershell -ExecutionPolicy Bypass -File Tools\test.ps1 [-Platform EditMode|PlayMode] [-Filter NameOrRegex]
param([ValidateSet('EditMode', 'PlayMode')][string]$Platform = 'EditMode', [string]$Filter)

. (Join-Path $PSScriptRoot 'unity-env.ps1')

if (Test-UnityProjectOpen) { throw 'The project is open in the Unity Editor. Close it before running tests from the command line.' }

$log = Join-Path $LogDir "test-$Platform.log"
$results = Join-Path $LogDir "test-$Platform.xml"
if (Test-Path $results) { Remove-Item $results }

Write-Host "Running $Platform tests... (log: $log)"
$filterArgs = if ($Filter) { @('-testFilter', $Filter) } else { @() }
& $UnityExe -batchmode -nographics -projectPath $ProjectRoot -runTests -testPlatform $Platform `
    -testResults $results -logFile $log @filterArgs | Out-Null
$code = $LASTEXITCODE

if (-not (Test-Path $results)) { throw "No test results produced (Unity exit code $code). See $log" }

[xml]$xml = Get-Content $results
$run = $xml.'test-run'
Write-Host ("Result: {0}  total={1} passed={2} failed={3} skipped={4}" -f $run.result, $run.total, $run.passed, $run.failed, $run.skipped)
$xml.SelectNodes("//test-case[@result='Failed']") | ForEach-Object {
    Write-Host "FAILED: $($_.fullname)"
    Write-Host "  $($_.failure.message.InnerText)"
}

if ($code -ne 0) { exit $code }
