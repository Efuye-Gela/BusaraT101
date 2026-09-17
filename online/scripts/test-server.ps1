param(
    [string]$Dotnet = "dotnet",
    [string]$Configuration = "Debug",
    [string]$Filter = "",
    [string]$ConnectionFile = "",
    [string]$OutputPath = ""
)
$ErrorActionPreference = "Stop"
$online = Split-Path $PSScriptRoot -Parent
Push-Location $online
$oldServer = $env:BUSARA_TEST_SERVER_DLL
$oldHost = $env:DOTNET_HOST_PATH
$oldTestDatabase = $env:BUSARA_TEST_DATABASE
$redactions = @()
function Write-SanitizedOutput {
    process {
        $line = "$_"
        foreach ($value in $redactions) {
            if ($value) { $line = $line.Replace($value, "[REDACTED]") }
        }
        $line = $line -replace '(?i)(__Host-busara=)[A-Za-z0-9_-]+', '$1[REDACTED]'
        $line = $line -replace '(?i)("csrfToken"\s*:\s*")[^"]+', '$1[REDACTED]'
        $line = $line -replace '(#invite=)[A-Za-z0-9_-]+', '$1[REDACTED]'
        if ($OutputPath) { $line | Out-File -FilePath $OutputPath -Encoding utf8 -Append -ErrorAction Stop }
        Write-Output $line
    }
}
function Invoke-CheckedDotnet {
    param([string[]]$Arguments, [string]$FailureMessage)
    $previousPreference = $ErrorActionPreference
    try {
        # Windows PowerShell wraps native stderr as errors; drain it before checking the process exit code.
        $ErrorActionPreference = "Continue"
        & $Dotnet @Arguments 2>&1 | Write-SanitizedOutput
        $nativeExitCode = $LASTEXITCODE
    } finally {
        $ErrorActionPreference = $previousPreference
    }
    if ($nativeExitCode -ne 0) { throw "$FailureMessage ($nativeExitCode)." }
}
try {
    if ($ConnectionFile) {
        $connection = Get-Content -LiteralPath $ConnectionFile -Raw | ConvertFrom-Json
        if (!$connection.host -or !$connection.port -or !$connection.username -or !$connection.password -or !$connection.testDatabase) {
            throw "Private connection configuration is missing a required test database field."
        }
        function Quote-ConnectionValue([string]$value) { return '"' + $value.Replace('"', '""') + '"' }
        $env:BUSARA_TEST_DATABASE = "Host=$(Quote-ConnectionValue $connection.host);Port=$([int]$connection.port);Username=$(Quote-ConnectionValue $connection.username);Password=$(Quote-ConnectionValue $connection.password);Database=$(Quote-ConnectionValue $connection.testDatabase)"
        $redactions += [string]$connection.password
    }
    if (!$env:BUSARA_TEST_DATABASE) {
        throw "BUSARA_TEST_DATABASE is mandatory: tests require actual loopback PostgreSQL and never silently skip."
    }
    $redactions += $env:BUSARA_TEST_DATABASE
    if ($OutputPath) {
        $OutputPath = [System.IO.Path]::GetFullPath($OutputPath)
        "Real PostgreSQL server integration validation" | Out-File -FilePath $OutputPath -Encoding utf8
    }
    $env:DOTNET_HOST_PATH = (Get-Command $Dotnet -ErrorAction Stop).Source
    Invoke-CheckedDotnet -Arguments @("build", ".\src\Busara.Server\Busara.Server.csproj", "-c", $Configuration, "--nologo", "-v", "quiet") -FailureMessage "Server build failed"
    $env:BUSARA_TEST_SERVER_DLL = Join-Path $online "src\Busara.Server\bin\$Configuration\net10.0\Busara.Server.dll"
    $arguments = @("test", ".\tests\Busara.Server.Tests\Busara.Server.Tests.csproj", "-c", $Configuration, "--nologo", "-v", "minimal")
    if ($Filter) { $arguments += @("--filter", $Filter) }
    Invoke-CheckedDotnet -Arguments $arguments -FailureMessage "PostgreSQL/process integration tests failed"
} finally {
    $env:BUSARA_TEST_SERVER_DLL = $oldServer
    $env:DOTNET_HOST_PATH = $oldHost
    $env:BUSARA_TEST_DATABASE = $oldTestDatabase
    Pop-Location
}
