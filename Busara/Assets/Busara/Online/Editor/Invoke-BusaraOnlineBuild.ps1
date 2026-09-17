param(
    [string]$UnityPath = 'C:\Program Files\Unity\Hub\Editor\6000.3.6f1\Editor\Unity.exe',
    [switch]$Development
)
$ErrorActionPreference = 'Stop'
$repository = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..\..')).Path
$project = Join-Path $repository 'Busara'
$expected = '6000.3.6f1'
if (-not (Test-Path -LiteralPath $UnityPath -PathType Leaf)) { throw 'The approved Unity editor executable is missing.' }
if ((Get-Item -LiteralPath $UnityPath).VersionInfo.ProductVersion -notlike "$expected*") {
    throw 'The executable is not the approved Unity 6000.3.6f1 editor.'
}
if (-not (Test-Path -LiteralPath (Join-Path (Split-Path $UnityPath) 'Data\PlaybackEngines\WebGLSupport'))) {
    throw 'The matching WebGL support module is missing.'
}
if (-not (Select-String -LiteralPath (Join-Path $project 'ProjectSettings\ProjectVersion.txt') -Pattern "^m_EditorVersion: $([regex]::Escape($expected))$" -Quiet)) {
    throw 'The worktree project version does not match the approved editor.'
}
$running = @(Get-CimInstance Win32_Process -Filter "Name = 'Unity.exe'")
if ($running.Count -gt 0) {
    throw 'A Unity editor process is running. Confirm its project and close it explicitly before this isolated batch build; this script never stops editors.'
}
$logs = Join-Path $repository 'online\.local\logs'
New-Item -ItemType Directory -Path $logs -Force | Out-Null
$log = Join-Path $logs ('unity-web-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '.log')
$oldDevelopment = $env:BUSARA_ONLINE_DEVELOPMENT
try {
    if ($Development) { $env:BUSARA_ONLINE_DEVELOPMENT = '1' }
    else { $env:BUSARA_ONLINE_DEVELOPMENT = '0' }
    Write-Output "Unity: $UnityPath"
    Write-Output "Project: $project"
    Write-Output "Log: $log"
    $process = Start-Process -FilePath $UnityPath -ArgumentList @(
        '-batchmode', '-nographics', '-quit', '-buildTarget', 'WebGL',
        '-projectPath', "`"$project`"", '-executeMethod', 'BusaraOnlineBuild.BuildWeb',
        '-logFile', "`"$log`""
    ) -Wait -PassThru
    if ($process.ExitCode -ne 0) { throw "Unity failed with exit code $($process.ExitCode). Inspect $log." }
    $index = Join-Path $repository 'online\web\index.html'
    if (-not (Test-Path -LiteralPath $index -PathType Leaf)) { throw 'Unity exited without the expected Web player.' }
    Write-Output "Built Unity OnlineMVP: $index"
}
finally {
    $env:BUSARA_ONLINE_DEVELOPMENT = $oldDevelopment
}
