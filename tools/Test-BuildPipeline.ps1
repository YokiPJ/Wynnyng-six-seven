$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
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
    $log = Join-Path $temporary 'prepare.log'
    Set-Content -LiteralPath $log @(
        '[Licensing::Client] Successfully updated license',
        "Assets\Wynnyng\Editor\WindowsBuild.cs(108,30): error CS0266: Cannot implicitly convert type 'int' to 'uint'.",
        "Assets\Wynnyng\Editor\WindowsBuild.cs(108,30): error CS0266: Cannot implicitly convert type 'int' to 'uint'.",
        'Scripts have compiler errors.'
    )
    $diagnostic = Get-UnityFailureDetails $log
    if ($diagnostic -notmatch 'CS0266' -or $diagnostic -notmatch 'Scripts have compiler errors') {
        throw 'Compiler errors were not shown from the Unity log.'
    }
    if ([regex]::Matches($diagnostic, 'CS0266').Count -ne 1) { throw 'Repeated compiler errors were not deduplicated.' }
    if ($diagnostic -match 'Successfully updated license') { throw 'Success noise included in failure details.' }
    if ((Get-UnityFailureDetails (Join-Path $temporary 'missing.log')) -ne '') { throw 'Missing-log handling failed.' }
    # Exercise the runner's failure path without launching Unity or waiting for real processes.
    $script:resolvedEditor = 'fake-editor.exe'
    $script:resultsPath = $temporary
    $script:ProjectPath = $temporary
    function Start-Process {
        param($FilePath, $ArgumentList, [switch]$PassThru)
        $process = [pscustomobject]@{ ExitCode = 1; Polls = 0 }
        $process | Add-Member ScriptMethod WaitForExit { param($milliseconds) $this.Polls++; return $this.Polls -ge 2 }
        $process | Add-Member ScriptMethod Refresh { }
        return $process
    }
    try {
        $failure = ''
        try { Invoke-UnityStep 'prepare' @('-executeMethod', 'Fixture') }
        catch { $failure = $_.Exception.Message }
        if ($failure -notmatch 'CS0266' -or $failure -notmatch 'codigo 1') {
            throw 'The runner did not surface the native compiler failure.'
        }
    }
    finally { Remove-Item Function:\Start-Process }
    $result = Join-Path $temporary 'tests.xml'
    $passedCase = '<test-case fullname="Fixture.PassingCase" result="Passed"/>'
    $visualCase = @'
<test-case fullname="Wynnyng.Tests.PlayMode.Phase01AAcceptanceTests.GraphicalPlayerRendersGrassInsteadOfOnlyTheClearBackground" result="Skipped" label="Ignored">
  <reason><message><![CDATA[Pixel acceptance requires the graphical Editor with the Game view visible.]]></message></reason>
</test-case>
'@
    $passedCases = (1..13 | ForEach-Object { "<test-case fullname='Fixture.PassingCase$_' result='Passed'/>" }) -join ''
    # Matches Unity Test Framework 1.4.5 / ext.nunit 2.0.3: the ignored pixel case
    # promotes all ancestor suites, including test-run, to an ignored result.
    $batchReport = "<test-run result='Skipped:Ignored' total='14' passed='13' failed='0' skipped='1'><test-suite result='Skipped' label='Ignored'>$passedCases$visualCase</test-suite></test-run>"
    foreach ($validXml in @(
        "<test-run result='Passed'>$passedCase</test-run>",
        $batchReport,
        "<test-run result='Passed'>$passedCase$visualCase</test-run>"
    )) {
        Set-Content -LiteralPath $result $validXml
        Assert-TestResult $result 'PlayMode'
    }
    foreach ($badXml in @(
        '<test-run result="Failed"><test-case result="Failed"/></test-run>',
        '<test-run result="Passed"></test-run>',
        '<test-run result="Passed"><test-case result="Skipped"/></test-run>',
        "<test-run result='Skipped:Ignored'>$visualCase</test-run>",
        "<test-run result='Skipped:Ignored'>$passedCase</test-run>",
        "<test-run result='Passed'>$passedCase<test-case result='Failed'/></test-run>",
        "<test-run result='Passed'>$passedCase<test-case result='Inconclusive'/></test-run>",
        "<test-run result='Passed'>$passedCase<test-case/></test-run>",
        "<test-run result='Failed'>$passedCase$visualCase</test-run>",
        "<test-run result='Passed'><test-suite result='Failed'>$passedCase</test-suite></test-run>",
        "<test-run result='Skipped:Ignored'>$passedCase$visualCase<test-case result='Failed'/></test-run>",
        "<test-run result='Skipped:Ignored'>$passedCase$visualCase<test-case result='Skipped' label='Ignored'/></test-run>",
        "<test-run result='Skipped:Ignored'>$passedCase$visualCase<test-suite result='Skipped' label='Ignored'/></test-run>",
        $batchReport.Replace('Phase01AAcceptanceTests.GraphicalPlayerRendersGrassInsteadOfOnlyTheClearBackground', 'Phase01AAcceptanceTests.UnexpectedSkippedCase'),
        $batchReport.Replace('Pixel acceptance requires the graphical Editor with the Game view visible.', 'Unexpected ignore reason.'),
        "<unexpected-format result='Passed'>$passedCase</unexpected-format>"
    )) {
        Set-Content -LiteralPath $result $badXml
        $rejected = $false
        try { Assert-TestResult $result 'PlayMode' } catch { $rejected = $true }
        if (-not $rejected) { throw 'An invalid test report was accepted.' }
    }
    Set-Content -LiteralPath $result $batchReport
    $rejected = $false
    try { Assert-TestResult $result 'EditMode' } catch { $rejected = $true }
    if (-not $rejected) { throw 'The PlayMode-only visual exception was accepted for EditMode.' }
    Set-Content -LiteralPath $result "<test-run result='Failed'><test-case fullname='Fixture.BrokenCase' result='Failed'><failure><message>Physics fixture failed.</message></failure></test-case></test-run>"
    $failure = ''
    try { Assert-TestResult $result 'PlayMode' } catch { $failure = $_.Exception.Message }
    if ($failure -notmatch 'Fixture.BrokenCase' -or $failure -notmatch 'Physics fixture failed') {
        throw 'Test case names and failure reasons were not shown.'
    }
    $rejected = $false
    try { Find-UnityEditor (Join-Path $temporary 'missing.exe') '6000.0.62f1' | Out-Null } catch { $rejected = $true }
    if (-not $rejected) { throw 'Missing Editor was accepted.' }
}
finally { Remove-Item -LiteralPath $temporary -Recurse -Force }
Write-Host 'PASS: PowerShell syntax, quoting, Editor discovery, Unity error extraction and test-result gates (including the known graphical skip). No Unity build was executed.'
