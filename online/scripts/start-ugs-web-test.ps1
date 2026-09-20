param(
    [string]$CertificateFile = (Join-Path $env:LOCALAPPDATA 'Busara\LocalWebTest\certificate.json'),
    [switch]$CreateCertificate,
    [ValidateRange(1024, 65535)][int]$Port = 7443
)
$ErrorActionPreference = 'Stop'
$web = Join-Path $PSScriptRoot '..\web'
$config = & (Join-Path $PSScriptRoot 'Get-UgsWebTestConfiguration.ps1') -WebRoot $web
if (-not (Test-Path -LiteralPath (Join-Path $web 'index.html') -PathType Leaf)) {
    throw 'The Unity Web player is missing. Build it first or extract the complete test ZIP.'
}
$node = Get-Command node -CommandType Application -ErrorAction SilentlyContinue
if ($null -eq $node) { throw 'Install Node.js 22 or newer, then reopen PowerShell.' }
$version = & $node.Source --version
if ($LASTEXITCODE -ne 0 -or $version -notmatch '^v(\d+)\.' -or [int]$Matches[1] -lt 22) {
    throw 'This launcher requires Node.js 22 or newer.'
}
if (-not (Test-Path -LiteralPath $CertificateFile -PathType Leaf)) {
    if (-not $CreateCertificate) {
        throw 'No certificate configuration exists. Run again with -CreateCertificate to create a private localhost test certificate, without installing trust.'
    }
    if ([IO.Path]::GetFileName($CertificateFile) -ne 'certificate.json') {
        throw 'New certificate configuration must be named certificate.json in a new private directory.'
    }
    $CertificateFile = & (Join-Path $PSScriptRoot 'New-WebTestCertificate.ps1') `
        -Directory (Split-Path ([IO.Path]::GetFullPath($CertificateFile)))
    Write-Output 'Created a 30-day localhost test certificate. No trusted root or firewall rule was installed.'
}
$certificate = $null
try {
    $settings = Get-Content -LiteralPath $CertificateFile -Raw | ConvertFrom-Json
    $certificate = [System.Security.Cryptography.X509Certificates.X509Certificate2]::new(
        $settings.path, $settings.password,
        [System.Security.Cryptography.X509Certificates.X509KeyStorageFlags]::EphemeralKeySet)
    if (-not $certificate.HasPrivateKey -or $certificate.NotBefore -gt (Get-Date) -or
        $certificate.NotAfter -le (Get-Date)) {
        throw 'Certificate is expired, not yet valid or has no private key.'
    }
    $san = @($certificate.Extensions | Where-Object { $_.Oid.Value -eq '2.5.29.17' })
    if ($san.Count -ne 1 -or -not $san[0].Format($false).Contains('127.0.0.1')) {
        throw 'Certificate does not include the loopback address.'
    }
}
catch { throw 'Cannot use the local certificate. Check its private configuration, validity and 127.0.0.1 coverage; no files were reset.' }
finally { if ($null -ne $certificate) { $certificate.Dispose() } }

$oldCertificate = $env:BUSARA_WEB_CERTIFICATE_FILE
$oldPort = $env:BUSARA_WEB_PORT
try {
    $env:BUSARA_WEB_CERTIFICATE_FILE = [IO.Path]::GetFullPath($CertificateFile)
    $env:BUSARA_WEB_PORT = $Port.ToString()
    Write-Output "UGS project: $($config.projectId) / environment: $($config.environmentName)"
    Write-Output 'Use an external test browser profile. Any certificate exception must apply only to this local origin.'
    Write-Output 'Keep this terminal open. Press Ctrl+C to stop. No UGS login or deployment occurs here.'
    & $node.Source (Join-Path $PSScriptRoot 'serve-ugs.cjs')
    if ($LASTEXITCODE -ne 0) { throw 'The local Web server stopped with an error. Check its message above.' }
}
finally {
    $env:BUSARA_WEB_CERTIFICATE_FILE = $oldCertificate
    $env:BUSARA_WEB_PORT = $oldPort
}
