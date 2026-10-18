# SPDX-License-Identifier: MIT
<#
.SYNOPSIS
    Runs the project's Unity tests headlessly and fails on a missing or failed run.

.DESCRIPTION
    Unity is resolved from -UnityPath, then UNITY_PATH, then the Unity Hub install
    root for the version in ProjectSettings/ProjectVersion.txt. Results and the log
    are written under ci/logs/ (gitignored).

    A run is accepted only when Unity wrote a result file with at least one passed
    test, no failed or inconclusive tests, and a "Passed" result.

.PARAMETER Mode
    EditMode (default) or PlayMode.

.PARAMETER UnityPath
    Full path to the editor executable for this project's version.

.EXAMPLE
    pwsh ci/run-tests.ps1
.EXAMPLE
    pwsh ci/run-tests.ps1 -Mode PlayMode -UnityPath "C:\Program Files\Unity\Hub\Editor\2022.3.62f3\Editor\Unity.exe"
#>
param(
    [ValidateSet("EditMode", "PlayMode")]
    [string]$Mode = "EditMode",
    [string]$UnityPath = $env:UNITY_PATH
)

$ErrorActionPreference = "Stop"
$project = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$versionFile = Join-Path $project "ProjectSettings" "ProjectVersion.txt"
$versionMatch = Select-String -LiteralPath $versionFile -Pattern '^m_EditorVersion:\s*(\S+)'
$editorVersion = $versionMatch.Matches[0].Groups[1].Value
if (-not $editorVersion) {
    throw "Cannot read m_EditorVersion from $versionFile."
}

$candidates = @()
if ($env:ProgramFiles) {
    $candidates += (Join-Path $env:ProgramFiles "Unity" "Hub" "Editor" $editorVersion "Editor" "Unity.exe")
}

if (-not $UnityPath) {
    $UnityPath = $candidates | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } |
        Select-Object -First 1
}
if (-not $UnityPath -or -not (Test-Path -LiteralPath $UnityPath -PathType Leaf)) {
    throw ("Unity $editorVersion not found. Pass -UnityPath or set UNITY_PATH. Tried:`n  " +
        (($candidates -join "`n  ")))
}

$results = Join-Path $project "ci" "logs" "$($Mode.ToLowerInvariant())-results.xml"
$log = Join-Path $project "ci" "logs" "$($Mode.ToLowerInvariant())-unity.log"

New-Item -ItemType Directory -Force -Path (Split-Path $results) | Out-Null
# Stale outputs would make a crash today look like a pass.
Remove-Item -LiteralPath $results, $log -ErrorAction SilentlyContinue

# Do not pass -quit: it suppresses the test run in batch mode.
# Piping forces PowerShell to wait for Unity.exe, a GUI-subsystem binary.
& $UnityPath -batchmode -nographics `
    -projectPath $project `
    -runTests -testPlatform $Mode `
    -testResults $results `
    -logFile $log | Out-Host

if (-not (Test-Path -LiteralPath $results)) {
    throw "Unity $Mode did not write a result file (exit $LASTEXITCODE). See $log."
}

[xml]$resultXml = Get-Content -LiteralPath $results
$run = $resultXml.'test-run'
if ($null -eq $run -or [int]$run.total -le 0) {
    throw "Unity $Mode ran 0 tests (exit $LASTEXITCODE). See $log."
}
if ([int]$run.passed -le 0) {
    throw "Unity $Mode wrote $($run.total) test(s) but none passed (exit $LASTEXITCODE). See $log."
}
if ([int]$run.failed -ne 0) {
    throw "Unity $Mode reported $($run.failed) failed test(s). See $results."
}
if ([int]$run.inconclusive -ne 0) {
    throw "Unity $Mode reported $($run.inconclusive) inconclusive test(s). See $results."
}
if ([string]$run.result -ne "Passed") {
    throw "Unity $Mode result is '$($run.result)'. See $results."
}
if ($LASTEXITCODE -ne 0) {
    throw "Unity $Mode exited $LASTEXITCODE although no test failed. See $log."
}

Write-Output ("Unity {0} tests: {1} passed, {2} skipped, 0 failed." -f $Mode, $run.passed, $run.skipped)
Write-Output "Results: $results"
