using module .\hosting\VercelHostingProvider.psm1
param()
$ErrorActionPreference = 'Stop'
$online = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$web = Join-Path $online 'web'
$config = & (Join-Path $PSScriptRoot 'Get-UgsWebTestConfiguration.ps1') -WebRoot $web
$files = & (Join-Path $PSScriptRoot 'Get-UnityWebBuildFiles.ps1') -WebRoot $web -RequireHashedNames
$files['busara-ugs.js'] = Join-Path $web 'busara-ugs.js'
$files['busara-latency.js'] = Join-Path $web 'busara-latency.js'
$generated = @{'busara-config.js' = 'window.busaraConfig = ' + ($config | ConvertTo-Json) + ';'}
$provider = [VercelHostingProvider]::new((Join-Path $online 'hosting\vercel.json'))
$name = 'site-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0, 8)
$directory = $provider.Export($files, $generated, (Join-Path $online ('.local\vercel\' + $name)))
Write-Output "Prepared site: $directory"
Write-Output "UGS project: $($config.projectId) / environment: $($config.environmentName)"
Write-Output 'Only public Unity files and Vercel static configuration were exported.'
Write-Output 'Nothing was uploaded, deployed or linked. Publish only after reviewing access and usage limits.'
