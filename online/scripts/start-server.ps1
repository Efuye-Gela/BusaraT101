param(
    [string]$Dotnet = "dotnet",
    [string]$Configuration = "Debug"
)
$ErrorActionPreference = "Stop"
$online = Split-Path $PSScriptRoot -Parent
Push-Location $online
$oldWebRoot = $env:BUSARA_WEB_ROOT
try {
    if ((!$env:ConnectionStrings__Busara -and !$env:BUSARA_DATABASE) -or !$env:BUSARA_SECRET_KEY -or (!$env:BUSARA_ORIGIN -and !$env:ASPNETCORE_URLS)) {
        throw "Set ConnectionStrings__Busara, BUSARA_SECRET_KEY and BUSARA_ORIGIN (or ASPNETCORE_URLS); see db\README.md."
    }
    if (!$env:ASPNETCORE_Kestrel__Certificates__Default__Path) {
        throw "Set ASPNETCORE_Kestrel__Certificates__Default__Path to your untracked loopback HTTPS certificate."
    }
    if (!$env:BUSARA_WEB_ROOT) { $env:BUSARA_WEB_ROOT = Join-Path $online "web" }
    & $Dotnet run --project ".\src\Busara.Server\Busara.Server.csproj" -c $Configuration --no-launch-profile --no-build
    if ($LASTEXITCODE -ne 0) { throw "Server failed ($LASTEXITCODE). Check readiness and explicit migrations." }
} finally {
    $env:BUSARA_WEB_ROOT = $oldWebRoot
    Pop-Location
}
