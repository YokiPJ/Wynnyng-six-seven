$ErrorActionPreference = 'Stop'
$scriptPath = Join-Path $PSScriptRoot 'Build-and-Play.ps1'
$tokens = $null
$parseErrors = $null
$tree = [Management.Automation.Language.Parser]::ParseFile($scriptPath, [ref]$tokens, [ref]$parseErrors)
if ($parseErrors.Count -ne 0) { throw ($parseErrors | Out-String) }
# Load pure argument/discovery/result helpers without executing Windows or Unity commands.
$functions = $tree.FindAll({ param($node) $node -is [Management.Automation.Language.FunctionDefinitionAst] }, $false)
foreach ($function in $functions) { Invoke-Expression $function.Extent.Text }
if ((ConvertTo-NativeArgument 'C:\Project Folder\sample') -ne '"C:\Project Folder\sample"') { throw 'Path quoting failed.' }
$rejected = $false
try { ConvertTo-NativeArgument 'bad"path' | Out-Null } catch { $rejected = $true }
if (-not $rejected) { throw 'Invalid quote was not rejected.' }
$temporary = Join-Path ([IO.Path]::GetTempPath()) ('WynnyngBuildTest-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $temporary | Out-Null
try {
    $result = Join-Path $temporary 'tests.xml'
    Set-Content -LiteralPath $result '<test-run result="Passed"><test-case result="Passed"/><test-case result="Skipped"/></test-run>'
    Assert-TestResult $result 'valid-result'
    foreach ($badXml in @(
        '<test-run result="Failed"><test-case result="Failed"/></test-run>',
        '<test-run result="Passed"></test-run>',
        '<test-run result="Passed"><test-case result="Skipped"/></test-run>',
        '<test-run result="Passed"><test-case result="Passed"/><test-case result="Failed"/></test-run>'
    )) {
        Set-Content -LiteralPath $result $badXml
        $rejected = $false
        try { Assert-TestResult $result 'invalid-result' } catch { $rejected = $true }
        if (-not $rejected) { throw 'An invalid test report was accepted.' }
    }
    $rejected = $false
    try { Find-UnityEditor (Join-Path $temporary 'missing.exe') '6000.0.62f1' | Out-Null } catch { $rejected = $true }
    if (-not $rejected) { throw 'Missing Editor was accepted.' }
}
finally { Remove-Item -LiteralPath $temporary -Recurse -Force }
Write-Host 'PASS: PowerShell syntax, argument quoting, Editor discovery and test-result gates. No Unity build was executed.'
