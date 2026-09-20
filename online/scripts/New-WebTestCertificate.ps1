param(
    [string]$Directory = (Join-Path $env:LOCALAPPDATA 'Busara\LocalWebTest')
)
$ErrorActionPreference = 'Stop'
if (Test-Path -LiteralPath $Directory) {
    throw 'Certificate directory already exists. Reuse its configuration or choose a new private directory; nothing was overwritten.'
}

$directoryInfo = New-Item -ItemType Directory -Path $Directory
$security = New-Object System.Security.AccessControl.DirectorySecurity
$owner = [System.Security.Principal.WindowsIdentity]::GetCurrent().User
$security.SetOwner($owner)
$security.SetAccessRuleProtection($true, $false)
$security.AddAccessRule([System.Security.AccessControl.FileSystemAccessRule]::new(
    $owner, 'FullControl', 'ContainerInherit,ObjectInherit', 'None', 'Allow'))
Set-Acl -LiteralPath $directoryInfo.FullName -AclObject $security

$certificate = $null
$random = [System.Security.Cryptography.RandomNumberGenerator]::Create()
try {
    $bytes = New-Object byte[] 32
    $random.GetBytes($bytes)
    $password = [Convert]::ToBase64String($bytes)
    $securePassword = ConvertTo-SecureString $password -AsPlainText -Force
    $certificate = New-SelfSignedCertificate -Type Custom -Subject 'CN=Busara localhost test' `
        -CertStoreLocation 'Cert:\CurrentUser\My' -KeyAlgorithm RSA -KeyLength 2048 `
        -KeyExportPolicy Exportable -HashAlgorithm SHA256 `
        -KeyUsage DigitalSignature, KeyEncipherment -NotAfter (Get-Date).AddDays(30) `
        -TextExtension @(
            '2.5.29.17={text}DNS=localhost&IPAddress=127.0.0.1',
            '2.5.29.37={text}1.3.6.1.5.5.7.3.1'
        )
    $pfx = Join-Path $directoryInfo.FullName 'localhost.pfx'
    Export-PfxCertificate -Cert $certificate -FilePath $pfx -Password $securePassword | Out-Null
    $configuration = Join-Path $directoryInfo.FullName 'certificate.json'
    $json = @{ path = $pfx; password = $password } | ConvertTo-Json
    [IO.File]::WriteAllText($configuration, $json, [Text.UTF8Encoding]::new($false))
}
finally {
    $random.Dispose()
    if ($null -ne $certificate) {
        # Remove only the temporary personal-store entry we just created, not a trust root.
        Remove-Item -LiteralPath ('Cert:\CurrentUser\My\' + $certificate.Thumbprint) -DeleteKey -Force
    }
}
Write-Output $configuration
