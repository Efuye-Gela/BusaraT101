param(
    [string]$Dotnet = "dotnet",
    [string]$Configuration = "Debug"
)
$ErrorActionPreference = "Stop"
$online = Split-Path $PSScriptRoot -Parent
Push-Location $online
try {
    if ((!$env:ConnectionStrings__Busara -and !$env:BUSARA_DATABASE) -or !$env:BUSARA_SECRET_KEY -or (!$env:BUSARA_ORIGIN -and !$env:ASPNETCORE_URLS)) {
        throw "Set ConnectionStrings__Busara, BUSARA_SECRET_KEY and BUSARA_ORIGIN (or ASPNETCORE_URLS); see db\README.md."
    }
    & $Dotnet build ".\src\Busara.Server\Busara.Server.csproj" -c $Configuration --nologo -v quiet
    if ($LASTEXITCODE -ne 0) { throw "Server build failed ($LASTEXITCODE)." }
    & $Dotnet ".\src\Busara.Server\bin\$Configuration\net10.0\Busara.Server.dll" --migrate
    if ($LASTEXITCODE -ne 0) { throw "Explicit database migration failed ($LASTEXITCODE)." }
} finally {
    Pop-Location
}
