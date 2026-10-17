param(
    [ValidateSet("EditMode", "PlayMode")]
    [string]$Mode = "EditMode"
)

$ErrorActionPreference = "Stop"
$project = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$unity = "D:\UnityEditor\Unity 2022.3.62f3\Editor\Unity.exe"
$results = Join-Path $project "ci\logs\$($Mode.ToLowerInvariant())-results.xml"
$log = Join-Path $project "ci\logs\$($Mode.ToLowerInvariant())-unity.log"

New-Item -ItemType Directory -Force -Path (Split-Path $results) | Out-Null

# Do not pass -quit: it suppresses the test run in batch mode.
& $unity -batchmode -nographics `
    -projectPath $project `
    -runTests -testPlatform $Mode `
    -testResults $results `
    -logFile $log

if ($LASTEXITCODE -ne 0) {
    Write-Error "Unity $Mode tests failed (exit $LASTEXITCODE). See $log"
}

Write-Output "Unity $Mode tests: $results"
