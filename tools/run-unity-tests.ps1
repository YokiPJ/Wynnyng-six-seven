param(
    [Parameter(Mandatory = $true)][string]$EditorPath,
    [string]$ProjectPath = (Split-Path -Parent $PSScriptRoot)
)
$ErrorActionPreference = 'Stop'
if (-not (Test-Path $EditorPath)) { throw "Unity Editor not found: $EditorPath" }
$resultPath = Join-Path $ProjectPath 'work/UnityTests'
New-Item -ItemType Directory -Path $resultPath -Force | Out-Null
# Close the interactive Editor first. PrepareBatch exits after the first TMP import.
& $EditorPath -batchmode -nographics -projectPath $ProjectPath `
    -executeMethod Wynnyng.Editor.ProjectPreparation.PrepareBatch `
    -logFile (Join-Path $resultPath 'prepare.log')
if ($LASTEXITCODE -ne 0) { throw "Preparation failed; see work/UnityTests/prepare.log" }
foreach ($platform in @('EditMode', 'PlayMode')) {
    & $EditorPath -batchmode -nographics -projectPath $ProjectPath `
        -runTests -testPlatform $platform `
        -testResults (Join-Path $resultPath "$platform.xml") `
        -logFile (Join-Path $resultPath "$platform.log")
    if ($LASTEXITCODE -ne 0) { throw "$platform tests failed; see work/UnityTests/$platform.log" }
}
Write-Host "Unity test results: $resultPath"
