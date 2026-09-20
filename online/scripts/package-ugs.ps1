param([string]$Dotnet = 'dotnet', [switch]$Maintenance)
$ErrorActionPreference = 'Stop'
$online = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$project = Join-Path $online 'src\Busara.Ugs\BusaraUgs.csproj'
$output = Join-Path $online '.local\ugs-package'
if ($Maintenance) { $output = Join-Path $output 'maintenance' }
New-Item -ItemType Directory -Force -Path $output | Out-Null
# A fresh directory prevents stale assemblies from a previous publish entering the module.
$publish = Join-Path $output ([Guid]::NewGuid().ToString('N'))
Push-Location $online
try {
    $properties = @()
    if ($Maintenance) { $properties += '-p:DefineConstants=BUSARA_MAINTENANCE' }
    & $Dotnet publish $project -c Release -r linux-x64 --self-contained false `
        -p:PublishReadyToRun=true -o $publish @properties
    if ($LASTEXITCODE -ne 0) { throw 'UGS module publish failed.' }
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = Join-Path $output 'BusaraUgs.ccm'
    if (Test-Path -LiteralPath $archive) { Remove-Item -LiteralPath $archive }
    [IO.Compression.ZipFile]::CreateFromDirectory($publish, $archive)
    if ((Get-Item -LiteralPath $archive).Length -gt 10MB) {
        throw 'Module exceeds the documented 10 MB Cloud Code archive limit.'
    }
    Write-Output "Module: $archive"
    Write-Output 'No deployment performed. Deploy this archive and the access policy to your development environment.'
} finally { Pop-Location }
