param([Parameter(Mandatory = $true)][string]$WebRoot, [switch]$RequireHashedNames)
$ErrorActionPreference = 'Stop'
$files = [ordered]@{'index.html' = Join-Path $WebRoot 'index.html'}
$html = Get-Content -LiteralPath $files['index.html'] -Raw
$references = @([regex]::Matches($html, 'Build/[a-zA-Z0-9_.-]+\.(?:js|wasm|data)') |
    ForEach-Object { $_.Value } | Sort-Object -Unique)
if ($references.Count -ne 4) {
    throw 'Expected the four uncompressed Unity build files. Rebuild with the approved Web build script.'
}
foreach ($suffix in @('.loader.js', '.framework.js', '.data', '.wasm')) {
    if (@($references | Where-Object { $_.EndsWith($suffix) }).Count -ne 1) {
        throw "Unity Web build must reference exactly one $suffix file."
    }
    if ($RequireHashedNames -and @($references | Where-Object {
        $_ -notmatch '^Build/[a-f0-9]{32}\.(loader\.js|framework\.js|data|wasm)$'
    }).Count -gt 0) { throw 'Immutable CDN caching requires Unity content-hashed build filenames.' }
}
foreach ($relative in $references) { $files[$relative] = Join-Path $WebRoot $relative.Replace('/', '\') }
foreach ($file in $files.Values) {
    if (-not (Test-Path -LiteralPath $file -PathType Leaf)) { throw "Missing build input: $file" }
}
if (Test-Path -LiteralPath (Join-Path $WebRoot 'StreamingAssets')) {
    throw 'This exporter does not yet support StreamingAssets. Add reviewed asset packaging before publishing this build.'
}
$files
