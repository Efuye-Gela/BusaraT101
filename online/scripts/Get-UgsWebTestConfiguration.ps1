param([Parameter(Mandatory = $true)][string]$WebRoot)
$ErrorActionPreference = 'Stop'

$source = Get-Content -LiteralPath (Join-Path $WebRoot 'busara-config.js') -Raw
$match = [regex]::Match($source, '^\s*window\.busaraConfig\s*=\s*(\{[\s\S]*\})\s*;\s*$')
if (-not $match.Success) { throw 'The Web build has an unrecognized configuration format. Rebuild it first.' }
$config = $match.Groups[1].Value | ConvertFrom-Json
$project = [Guid]::Empty
if ($config.backend -ne 'ugs' -or -not [Guid]::TryParse($config.projectId, [ref]$project) -or
    $project -eq [Guid]::Empty -or $config.environmentName -cnotmatch '^[a-zA-Z0-9_-]{1,50}$' -or
    $config.environmentName -ieq 'production' -or $config.moduleName -cnotmatch '^[a-zA-Z0-9_]{1,50}$' -or
    $config.pollSeconds -isnot [ValueType] -or $config.pollSeconds -lt 1 -or $config.pollSeconds -gt 60) {
    throw 'Build a configured, non-production UGS Web player before using the two-PC test tools.'
}
# Only these public fields belong in a distributable configuration.
[pscustomobject]@{
    backend = 'ugs'
    projectId = $project.ToString()
    environmentName = $config.environmentName
    moduleName = $config.moduleName
    pollSeconds = $config.pollSeconds
}
