param(
    [string]$EditorPath = $env:WYNNYNG_UNITY_EDITOR,
    [string]$ProjectPath = (Split-Path -Parent $PSScriptRoot),
    [switch]$SkipTests,
    [switch]$NoLaunch
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function ConvertTo-NativeArgument([string]$Value) {
    if ($Value.Contains('"')) { throw 'An argument contains an invalid quote character.' }
    # Paths passed by this script name files/directories below the project, not a drive root.
    return '"' + $Value.TrimEnd([char]'\') + '"'
}

function Find-UnityEditor([string]$RequestedPath, [string]$Version) {
    if (-not [string]::IsNullOrWhiteSpace($RequestedPath)) {
        if (-not (Test-Path -LiteralPath $RequestedPath -PathType Leaf)) {
            throw "Unity Editor nao encontrado: $RequestedPath"
        }
        return (Resolve-Path -LiteralPath $RequestedPath).Path
    }
    $candidates = New-Object 'System.Collections.Generic.List[string]'
    foreach ($programRoot in @($env:ProgramFiles, $env:ProgramW6432, ${env:ProgramFiles(x86)})) {
        if (-not [string]::IsNullOrWhiteSpace($programRoot)) {
            $candidates.Add((Join-Path $programRoot "Unity/Hub/Editor/$Version/Editor/Unity.exe"))
        }
    }
    foreach ($candidate in $candidates) {
        if (Test-Path -LiteralPath $candidate -PathType Leaf) {
            return (Resolve-Path -LiteralPath $candidate).Path
        }
    }
    if (-not [string]::IsNullOrWhiteSpace($env:APPDATA)) {
        $hubFile = Join-Path $env:APPDATA 'UnityHub/editors-v2.json'
        if (Test-Path -LiteralPath $hubFile) {
            try {
                $installed = Get-Content -LiteralPath $hubFile -Raw | ConvertFrom-Json
                if ($installed.PSObject.Properties.Name -contains 'data') {
                    foreach ($entry in @($installed.data)) {
                        if ($null -eq $entry -or $entry.PSObject.Properties.Name -notcontains 'version' -or $entry.version -ne $Version) { continue }
                        if ($entry.PSObject.Properties.Name -contains 'location') {
                            $location = [string]$entry.location
                            if (-not [string]::IsNullOrWhiteSpace($location)) {
                                $candidates.Add($location)
                                $candidates.Add((Join-Path $location 'Editor/Unity.exe'))
                            }
                        }
                    }
                }
            }
            catch { Write-Host '[Wynnyng] Cadastro de instalacoes do Hub indisponivel; use -EditorPath se necessario.' }
        }
    }
    foreach ($candidate in $candidates) {
        if (Test-Path -LiteralPath $candidate -PathType Leaf) {
            return (Resolve-Path -LiteralPath $candidate).Path
        }
    }
    throw "Unity $Version nao foi encontrado. Instale essa versao pelo Unity Hub ou informe -EditorPath 'C:/caminho/Editor/Unity.exe'."
}

function Get-UnityFailureDetails([string]$LogPath) {
    if (-not (Test-Path -LiteralPath $LogPath -PathType Leaf)) { return '' }
    $lines = @(Get-Content -LiteralPath $LogPath -Encoding UTF8 -Tail 300)
    $diagnostics = @($lines | Where-Object {
        $_ -match 'error CS\d+|Exception|Scripts have compiler errors|Aborting batchmode|No valid.*license|executeMethod.*(not found|could not)|failed|failure'
    } | Select-Object -Unique | Select-Object -First 12)
    if ($diagnostics.Count -eq 0) { $diagnostics = @($lines | Select-Object -Last 12) }
    return ($diagnostics -join [Environment]::NewLine)
}

function Invoke-UnityStep([string]$Name, [string[]]$ExtraArguments) {
    $logPath = Join-Path $resultsPath "$Name.log"
    $arguments = @('-batchmode', '-nographics', '-buildTarget', 'Win64',
        '-projectPath', $ProjectPath, '-logFile', $logPath) + $ExtraArguments
    $quoted = @($arguments | ForEach-Object { ConvertTo-NativeArgument $_ })
    Write-Host "[Wynnyng] $Name..."
    # Wait for this Editor process, not its tree: the licensing client can outlive the Editor.
    $process = Start-Process -FilePath $script:resolvedEditor -ArgumentList ($quoted -join ' ') -PassThru
    $elapsed = [Diagnostics.Stopwatch]::StartNew()
    $nextProgress = 20
    while (-not $process.WaitForExit(1000)) {
        if ($elapsed.Elapsed.TotalSeconds -ge $nextProgress) {
            Write-Host ("[Wynnyng] {0} em andamento ({1:0}s). Log: {2}" -f $Name, $elapsed.Elapsed.TotalSeconds, $logPath)
            $nextProgress += 20
        }
    }
    $elapsed.Stop()
    $process.Refresh()
    if ($process.ExitCode -ne 0) {
        $details = Get-UnityFailureDetails $logPath
        throw "Falha na etapa $Name (codigo $($process.ExitCode)).`n$details`nLog completo: $logPath"
    }
    Write-Host ("[Wynnyng] {0} concluido em {1:0}s." -f $Name, $elapsed.Elapsed.TotalSeconds)
}

function Assert-TestResult([string]$Path, [string]$Name) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "$Name nao produziu um resultado XML. Consulte o log dessa etapa."
    }
    [xml]$report = Get-Content -LiteralPath $Path -Raw
    $cases = @($report.SelectNodes('//*[local-name()="test-case"]'))
    $result = $report.DocumentElement.GetAttribute('result')
    $passed = @($cases | Where-Object { $_.GetAttribute('result') -in @('Passed', 'Success') })
    $failed = @($cases | Where-Object { $_.GetAttribute('result') -in @('Failed', 'Failure', 'Error') })
    if ($passed.Count -eq 0 -or $failed.Count -ne 0 -or $result -notin @('Passed', 'Success')) {
        throw "$Name nao passou (resultado: $result; casos: $($cases.Count)). Consulte $Path"
    }
    Write-Host "[Wynnyng] ${Name}: $($cases.Count) casos, $result."
}

if ($env:OS -ne 'Windows_NT') { throw 'Este comando gera o player usando o Unity instalado no Windows.' }
$ProjectPath = (Resolve-Path -LiteralPath $ProjectPath).Path
$versionFile = Join-Path $ProjectPath 'ProjectSettings/ProjectVersion.txt'
if (-not (Test-Path -LiteralPath $versionFile -PathType Leaf)) { throw 'Pasta do projeto Unity invalida.' }
$versionMatch = [regex]::Match((Get-Content -LiteralPath $versionFile -Raw), '(?m)^m_EditorVersion:\s*(\S+)')
if (-not $versionMatch.Success) { throw 'A versao do Unity nao esta definida no projeto.' }
$requiredVersion = $versionMatch.Groups[1].Value
$script:resolvedEditor = Find-UnityEditor $EditorPath $requiredVersion
$supportPath = Join-Path (Split-Path -Parent $script:resolvedEditor) 'Data/PlaybackEngines/windowsstandalonesupport'
if (-not (Test-Path -LiteralPath $supportPath -PathType Container)) {
    throw "Instale Windows Build Support (Mono) para Unity $requiredVersion em Unity Hub > Instalacoes > Adicionar modulos."
}
if (Test-Path -LiteralPath (Join-Path $ProjectPath 'Temp/UnityLockfile')) {
    throw 'Feche o Editor Unity deste projeto antes de gerar o executavel e execute este comando novamente.'
}
$resultsPath = Join-Path $ProjectPath 'work/WindowsBuild'
New-Item -ItemType Directory -Path $resultsPath -Force | Out-Null
$receiptPath = Join-Path $resultsPath 'build-result.json'
if (Test-Path -LiteralPath $receiptPath) { Remove-Item -LiteralPath $receiptPath }

Write-Host "[Wynnyng] Unity $requiredVersion"
Write-Host "[Wynnyng] Projeto: $ProjectPath"
Invoke-UnityStep 'prepare' @('-executeMethod', 'Wynnyng.Editor.ProjectPreparation.PrepareBatch')
if (-not $SkipTests) {
    foreach ($platform in @('EditMode', 'PlayMode')) {
        $xmlPath = Join-Path $resultsPath "$platform.xml"
        if (Test-Path -LiteralPath $xmlPath) { Remove-Item -LiteralPath $xmlPath }
        Invoke-UnityStep $platform @('-runTests', '-testPlatform', $platform, '-testResults', $xmlPath)
        Assert-TestResult $xmlPath $platform
    }
}
else { Write-Host '[Wynnyng] Testes ignorados por solicitacao explicita (-SkipTests).' }

$gamePath = Join-Path $ProjectPath 'Builds/Windows/Wynnyng.exe'
Invoke-UnityStep 'build' @('-executeMethod', 'Wynnyng.Editor.WindowsBuild.BuildBatch', '-wynnyngBuildPath', $gamePath)
if (-not (Test-Path -LiteralPath $receiptPath -PathType Leaf)) { throw 'O build nao produziu build-result.json.' }
$receipt = Get-Content -LiteralPath $receiptPath -Raw | ConvertFrom-Json
if (-not $receipt.succeeded -or -not (Test-Path -LiteralPath $gamePath -PathType Leaf)) {
    throw "O build nao concluiu um executavel valido. Consulte $receiptPath"
}
$dataPath = Join-Path (Split-Path -Parent $gamePath) 'Wynnyng_Data'
$playerDll = Join-Path (Split-Path -Parent $gamePath) 'UnityPlayer.dll'
if (-not (Test-Path -LiteralPath $dataPath -PathType Container) -or -not (Test-Path -LiteralPath $playerDll -PathType Leaf)) {
    throw 'A pasta do player esta incompleta. O EXE precisa da pasta Wynnyng_Data e de UnityPlayer.dll.'
}
Write-Host "[Wynnyng] Pronto: $gamePath"
try {
    $desktopPath = [Environment]::GetFolderPath('DesktopDirectory')
    if (-not [string]::IsNullOrWhiteSpace($desktopPath) -and (Test-Path -LiteralPath $desktopPath)) {
        $shell = New-Object -ComObject WScript.Shell
        $shortcut = $shell.CreateShortcut((Join-Path $desktopPath 'Jogar Wynnyng.lnk'))
        $shortcut.TargetPath = $gamePath
        $shortcut.WorkingDirectory = Split-Path -Parent $gamePath
        $shortcut.Description = 'Prototipo Wynnyng - campo e bola'
        $shortcut.Save()
    }
}
catch { Write-Host '[Wynnyng] O jogo foi gerado; o atalho da area de trabalho nao pode ser criado.' }
if (-not $NoLaunch) {
    Start-Process -FilePath $gamePath -WorkingDirectory (Split-Path -Parent $gamePath)
}
