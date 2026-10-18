param(
    [ValidateSet("EditMode", "PlayMode")]
    [string]$Mode = "EditMode",
    [string]$UnityPath = $env:UNITY_PATH
)

$ErrorActionPreference = "Stop"
$project = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
if (-not $UnityPath) {
    $candidates = @(
        "D:\UnityEditor\Unity 2022.3.62f3\Editor\Unity.exe",
        "D:\UnityEditor-backup\Unity 2022.3.62f3\Editor\Unity.exe",
        (Join-Path $env:ProgramFiles "Unity\Hub\Editor\2022.3.62f3\Editor\Unity.exe")
    )
    $UnityPath = $candidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
}
if (-not $UnityPath -or -not (Test-Path -LiteralPath $UnityPath)) {
    throw "Unity.exe not found. Pass -UnityPath or set UNITY_PATH."
}
$unity = $UnityPath
$results = Join-Path $project "ci\logs\$($Mode.ToLowerInvariant())-results.xml"
$log = Join-Path $project "ci\logs\$($Mode.ToLowerInvariant())-unity.log"

New-Item -ItemType Directory -Force -Path (Split-Path $results) | Out-Null
Remove-Item -LiteralPath $results, $log -ErrorAction SilentlyContinue

# Do not pass -quit: it suppresses the test run in batch mode.
# Piping forces PowerShell to wait for Unity.exe, which is a GUI-subsystem binary:
# without it the script returns before the run and $LASTEXITCODE is never set.
& $unity -batchmode -nographics `
    -projectPath $project `
    -runTests -testPlatform $Mode `
    -testResults $results `
    -logFile $log | Out-Host

if ($LASTEXITCODE -ne 0) {
    Write-Error "Unity $Mode tests failed (exit $LASTEXITCODE). See $log"
}

if (-not (Test-Path -LiteralPath $results)) {
    throw "Unity $Mode did not write a result file. See $log."
}

[xml]$resultXml = Get-Content -LiteralPath $results
$run = $resultXml.'test-run'
if ($null -eq $run -or [int]$run.total -le 0) {
    throw "Unity $Mode wrote an empty result file. See $log."
}

if ([int]$run.failed -ne 0) {
    throw "Unity $Mode reported $($run.failed) failed test(s). See $results."
}

Write-Output "Unity $Mode tests: $results"
