param()
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
$scripts = (Resolve-Path (Join-Path $PSScriptRoot '..\scripts')).Path
$root = Join-Path ([IO.Path]::GetTempPath()) ('busara-web-tools-' + [Guid]::NewGuid().ToString('N'))
$utf8 = [Text.UTF8Encoding]::new($false)
function Assert-True($condition, $message) { if (-not $condition) { throw $message } }
function Assert-Rejected([scriptblock]$action, [string]$message) {
    $rejected = $false
    try { & $action | Out-Null } catch { $rejected = $true }
    Assert-True $rejected $message
}
function Write-Configuration($values) {
    $source = 'window.busaraConfig = ' + ($values | ConvertTo-Json) + ';'
    [IO.File]::WriteAllText((Join-Path $web 'busara-config.js'), $source, $utf8)
}

try {
    $web = Join-Path $root 'online\web'
    $copiedScripts = Join-Path $root 'online\scripts'
    New-Item -ItemType Directory -Path (Join-Path $web 'Build'), $copiedScripts, (Join-Path $root 'docs') | Out-Null
    foreach ($name in @('package-ugs-web-test.ps1', 'Get-UgsWebTestConfiguration.ps1', 'Get-UnityWebBuildFiles.ps1',
        'New-WebTestCertificate.ps1', 'start-ugs-web-test.ps1', 'serve-ugs.cjs')) {
        Copy-Item -LiteralPath (Join-Path $scripts $name) -Destination $copiedScripts
        if ($name.EndsWith('.ps1')) {
            $tokens = $null
            $parseErrors = $null
            [Management.Automation.Language.Parser]::ParseFile(
                (Join-Path $copiedScripts $name), [ref]$tokens, [ref]$parseErrors) | Out-Null
            Assert-True ($parseErrors.Count -eq 0) "PowerShell syntax error: $name"
            Assert-True ((Get-Content -LiteralPath (Join-Path $copiedScripts $name)).Count -lt 200) "Script exceeds 200 lines: $name"
        }
    }
    $valid = @{backend = 'ugs'; projectId = '11111111-1111-4111-8111-111111111111';
        environmentName = 'development'; moduleName = 'BusaraUgs'; pollSeconds = 10}
    $readConfig = Join-Path $copiedScripts 'Get-UgsWebTestConfiguration.ps1'
    foreach ($change in @(@{backend = 'legacy'}, @{environmentName = 'PRODUCTION'},
        @{projectId = 'not-a-project'}, @{pollSeconds = 0}, @{moduleName = 'bad.module'})) {
        $invalid = $valid.Clone()
        foreach ($key in $change.Keys) { $invalid[$key] = $change[$key] }
        Write-Configuration $invalid
        Assert-Rejected { & $readConfig -WebRoot $web } 'Invalid build configuration was accepted.'
    }
    $valid['unwantedPrivateField'] = 'must-not-be-packaged'
    Write-Configuration $valid
    $publicConfig = & $readConfig -WebRoot $web
    Assert-True ($null -eq $publicConfig.unwantedPrivateField) 'Configuration leaked extra fields.'
    $html = '<script src="Build/test.loader.js"></script> dataUrl: ''Build/test.data'', frameworkUrl: ''Build/test.framework.js'', codeUrl: ''Build/test.wasm'''
    [IO.File]::WriteAllText((Join-Path $web 'index.html'), $html, $utf8)
    foreach ($name in @('test.loader.js', 'test.framework.js', 'test.data', 'test.wasm', 'private.pfx')) {
        [IO.File]::WriteAllText((Join-Path $web ('Build\' + $name)), 'fixture', $utf8)
    }
    [IO.File]::WriteAllText((Join-Path $web 'busara-ugs.js'), '// fixture', $utf8)
    [IO.File]::WriteAllText((Join-Path $web 'certificate.json'), 'must-not-be-packaged', $utf8)
    [IO.File]::WriteAllText((Join-Path $root 'docs\two-pc-testing.md'), 'fixture guide', $utf8)
    & (Join-Path $copiedScripts 'package-ugs-web-test.ps1') | Out-Null
    $archive = @(Get-ChildItem -LiteralPath (Join-Path $root 'online\.local\two-pc') -Filter '*.zip')
    Assert-True ($archive.Count -eq 1) 'Expected one completed test archive.'
    $zip = [IO.Compression.ZipFile]::OpenRead($archive[0].FullName)
    try {
        Assert-True ($zip.Entries.Count -eq 12) 'Unexpected files in bundle.'
        foreach ($entry in $zip.Entries) {
            Assert-True ($entry.FullName -notmatch '(private\.pfx|certificate\.json|\.partial|\.ccm)$') 'Private file was bundled.'
        }
        $reader = [IO.StreamReader]::new($zip.GetEntry('web/busara-config.js').Open())
        try { Assert-True (-not $reader.ReadToEnd().Contains('must-not-be-packaged')) 'Extra configuration was bundled.' }
        finally { $reader.Dispose() }
    } finally { $zip.Dispose() }
    $extracted = Join-Path $root 'extracted'
    [IO.Compression.ZipFile]::ExtractToDirectory($archive[0].FullName, $extracted)
    Remove-Item -LiteralPath (Join-Path $web 'Build\test.wasm')
    Assert-Rejected { & (Join-Path $copiedScripts 'package-ugs-web-test.ps1') } 'Incomplete build was packaged.'

    $private = Join-Path $root 'private'
    $configuration = & (Join-Path $scripts 'New-WebTestCertificate.ps1') -Directory $private
    Assert-True ((Get-Acl -LiteralPath $private).AreAccessRulesProtected) 'Certificate directory inherited permissions.'
    $hash = (Get-FileHash -LiteralPath $configuration).Hash
    Assert-Rejected { & (Join-Path $scripts 'New-WebTestCertificate.ps1') -Directory $private } 'Existing certificate was overwritten.'
    Assert-True ((Get-FileHash -LiteralPath $configuration).Hash -eq $hash) 'Existing configuration changed.'
    $settings = Get-Content -LiteralPath $configuration -Raw | ConvertFrom-Json
    $cert = [Security.Cryptography.X509Certificates.X509Certificate2]::new(
        $settings.path, $settings.password, [Security.Cryptography.X509Certificates.X509KeyStorageFlags]::EphemeralKeySet)
    try {
        Assert-True (-not (Test-Path -LiteralPath ('Cert:\CurrentUser\My\' + $cert.Thumbprint))) 'Temporary certificate-store entry remains.'
        Assert-True (-not (Test-Path -LiteralPath ('Cert:\CurrentUser\Root\' + $cert.Thumbprint))) 'Certificate was installed as trusted.'
        $der = $cert.Export([Security.Cryptography.X509Certificates.X509ContentType]::Cert)
        $pem = "-----BEGIN CERTIFICATE-----`n" + [Convert]::ToBase64String($der, 'InsertLineBreaks') + "`n-----END CERTIFICATE-----"
        $publicCertificate = Join-Path $root 'public.pem'
        [IO.File]::WriteAllText($publicCertificate, $pem, $utf8)
    } finally { $cert.Dispose() }
    & node (Join-Path $PSScriptRoot 'ugs-web-test-server.test.cjs') `
        (Join-Path $extracted 'scripts\serve-ugs.cjs') $configuration $publicCertificate
    if ($LASTEXITCODE -ne 0) { throw 'Portable HTTPS server test failed.' }

    $listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, 0)
    $listener.Start()
    $oldCertificate = $env:BUSARA_WEB_CERTIFICATE_FILE
    $oldPort = $env:BUSARA_WEB_PORT
    try {
        Assert-Rejected {
            & (Join-Path $extracted 'scripts\start-ugs-web-test.ps1') `
                -CertificateFile $configuration -Port $listener.LocalEndpoint.Port *>&1
        } 'Launcher concealed an occupied port.'
        Assert-True ($env:BUSARA_WEB_CERTIFICATE_FILE -eq $oldCertificate -and $env:BUSARA_WEB_PORT -eq $oldPort) 'Launcher did not restore environment.'
    } finally { $listener.Stop() }
    Write-Output 'PASS: Windows script syntax/size, fail-closed config, portable allowlist, certificate privacy, no trust changes and launcher failure handling.'
}
finally {
    if (Test-Path -LiteralPath $root) { Remove-Item -LiteralPath $root -Recurse -Force }
}
