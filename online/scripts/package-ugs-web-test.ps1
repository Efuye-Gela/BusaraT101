param()
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
$online = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$web = Join-Path $online 'web'
$config = & (Join-Path $PSScriptRoot 'Get-UgsWebTestConfiguration.ps1') -WebRoot $web
$files = [ordered]@{}
$buildFiles = & (Join-Path $PSScriptRoot 'Get-UnityWebBuildFiles.ps1') -WebRoot $web
foreach ($entry in $buildFiles.GetEnumerator()) {
    $files["web/$($entry.Key)"] = $entry.Value
}
$files['web/busara-ugs.js'] = Join-Path $web 'busara-ugs.js'
foreach ($name in @('serve-ugs.cjs', 'start-ugs-web-test.ps1', 'New-WebTestCertificate.ps1',
    'Get-UgsWebTestConfiguration.ps1')) {
    $files["scripts/$name"] = Join-Path $PSScriptRoot $name
}
$files['START-HERE.md'] = Join-Path $online '..\docs\two-pc-testing.md'
foreach ($file in $files.Values) {
    if (-not (Test-Path -LiteralPath $file -PathType Leaf)) { throw "Missing bundle input: $file" }
}

$output = Join-Path $online '.local\two-pc'
New-Item -ItemType Directory -Path $output -Force | Out-Null
$name = 'Busara-two-pc-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0, 8)
$archive = Join-Path $output ($name + '.zip')
$partial = $archive + '.partial'
$zip = $null
try {
    $zip = [IO.Compression.ZipFile]::Open($partial, [IO.Compression.ZipArchiveMode]::Create)
    foreach ($entry in $files.GetEnumerator()) {
        [IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
            $zip, $entry.Value, $entry.Key, [IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
    $entry = $zip.CreateEntry('web/busara-config.js')
    $writer = [IO.StreamWriter]::new($entry.Open(), [Text.UTF8Encoding]::new($false))
    try { $writer.Write('window.busaraConfig = ' + ($config | ConvertTo-Json) + ';') }
    finally { $writer.Dispose() }
    $zip.Dispose()
    $zip = $null
    Move-Item -LiteralPath $partial -Destination $archive
}
finally {
    if ($null -ne $zip) { $zip.Dispose() }
    if (Test-Path -LiteralPath $partial) { Remove-Item -LiteralPath $partial }
}
Write-Output "Bundle: $archive"
Write-Output "SHA256: $((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash)"
Write-Output "UGS project: $($config.projectId) / environment: $($config.environmentName)"
Write-Output 'Only the Web build, local scripts and instructions are included. No credentials, certificates or player identities.'
Write-Output 'No deployment or public listener was created. Copy this ZIP to the other Windows PC and extract it.'
