using module ..\scripts\hosting\StaticHostingProvider.psm1
using module ..\scripts\hosting\VercelHostingProvider.psm1
param()
$ErrorActionPreference = 'Stop'
$online = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$root = Join-Path ([IO.Path]::GetTempPath()) ('busara-static-hosting-' + [Guid]::NewGuid().ToString('N'))
function Assert-True($condition, $message) { if (-not $condition) { throw $message } }
function Assert-Rejected([scriptblock]$action, [string]$message) {
    $rejected = $false
    try { & $action | Out-Null } catch { $rejected = $true }
    Assert-True $rejected $message
}
try {
    New-Item -ItemType Directory -Path $root | Out-Null
    $source = Join-Path $root 'source.html'
    [IO.File]::WriteAllText($source, '<html>fixture</html>')
    $files = @{'index.html' = $source}
    $generated = @{'busara-config.js' = 'window.busaraConfig = {};'}
    $plain = [StaticHostingProvider]::new()
    $plainRoot = $plain.Export($files, $generated, (Join-Path $root 'plain'))
    Assert-True (Test-Path -LiteralPath (Join-Path $plainRoot 'public\index.html')) 'Generic export is missing its entry.'
    Assert-True (-not (Test-Path -LiteralPath (Join-Path $plainRoot 'vercel.json'))) 'Generic provider depends on Vercel.'
    $provider = [VercelHostingProvider]::new((Join-Path $online 'hosting\vercel.json'))
    $export = $provider.Export($files, $generated, (Join-Path $root 'vercel'))
    Assert-True ((Get-ChildItem -LiteralPath $export -Recurse -File).Count -eq 3) 'Unexpected exported files.'
    Assert-True ((Get-FileHash -LiteralPath $source).Hash -eq
        (Get-FileHash -LiteralPath (Join-Path $export 'public\index.html')).Hash) 'Export changed the build.'
    Assert-Rejected { $provider.Export($files, $generated, $export) } 'Existing output was overwritten.'
    Assert-Rejected { $provider.Export(@{'..\escape' = $source}, @{}, (Join-Path $root 'traversal')) } 'Traversal was accepted.'
    Assert-Rejected { $provider.Export(@{'index.html' = $source}, @{'index.html' = 'replace'}, (Join-Path $root 'duplicate')) } 'Duplicate target was accepted.'
    Assert-Rejected { $provider.Export(@{'index.html' = (Join-Path $root 'missing')}, @{}, (Join-Path $root 'missing-out')) } 'Missing input was accepted.'
    $provider.MaximumBytes = 1
    Assert-Rejected { $provider.Export($files, $generated, (Join-Path $root 'oversize')) } 'Upload limit was ignored.'
    Assert-True (-not (Test-Path -LiteralPath (Join-Path $root 'oversize'))) 'Invalid export created an output directory.'
    $config = Get-Content -LiteralPath (Join-Path $export 'vercel.json') -Raw | ConvertFrom-Json
    Assert-True ($config.outputDirectory -eq 'public' -and $null -eq $config.framework) 'Vercel must serve only the public directory.'
    Assert-True ($config.buildCommand -eq '' -and $config.installCommand -eq '') 'Vercel must not rebuild Unity or install dependencies.'
    Assert-True ($null -eq $config.functions -and $null -eq $config.rewrites) 'Unexpected server functions or proxy rewrites.'
    $wasm = $config.headers | Where-Object { $_.source -eq '/Build/(.*).wasm' }
    Assert-True ($wasm.headers.value -contains 'application/wasm') 'Wasm MIME is missing.'
    $rootHeaders = $config.headers | Where-Object { $_.source -eq '/' }
    Assert-True ($rootHeaders.headers.value -contains 'no-store') 'Entry page can become stale.'
    $global = $config.headers | Where-Object { $_.source -eq '/(.*)' }
    Assert-True ($global.headers.value -contains 'no-referrer') 'Invitation referrer protection is missing.'

    $web = Join-Path $root 'web'
    New-Item -ItemType Directory -Path (Join-Path $web 'Build') | Out-Null
    $refs = @('aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.loader.js', 'bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb.framework.js',
        'cccccccccccccccccccccccccccccccc.data', 'dddddddddddddddddddddddddddddddd.wasm')
    foreach ($name in $refs) { [IO.File]::WriteAllText((Join-Path $web ('Build\' + $name)), 'fixture') }
    [IO.File]::WriteAllText((Join-Path $web 'index.html'), (($refs | ForEach-Object { "'Build/$_'" }) -join ' '))
    $selector = Join-Path $online 'scripts\Get-UnityWebBuildFiles.ps1'
    $selected = & $selector -WebRoot $web -RequireHashedNames
    Assert-True ($selected.Count -eq 5) 'Unity build selection is incomplete.'
    [IO.File]::WriteAllText((Join-Path $web 'index.html'), ("'Build/plain.loader.js' " + (($refs[1..3] | ForEach-Object { "'Build/$_'" }) -join ' ')))
    Assert-Rejected { & $selector -WebRoot $web -RequireHashedNames } 'Immutable caching accepted a mutable filename.'
    Write-Output 'PASS: generic/provider separation, exact copies, no overwrites, path boundaries, upload limits, static headers and hashed build selection.'
}
finally {
    if (Test-Path -LiteralPath $root) { Remove-Item -LiteralPath $root -Recurse -Force }
}
