$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$runtime = Join-Path $repo 'artifacts'
if (!(Test-Path (Join-Path $runtime 'woocommerce-tunnel-web/browser/index.html'))) { throw 'Build woocommerce-tunnel frontend first.' }
if (Test-Path (Join-Path $runtime 'shopify-tunnel-processes.json')) {
    $old = Get-Content (Join-Path $runtime 'shopify-tunnel-processes.json') -Raw | ConvertFrom-Json
    if (Get-Process -Id $old.proxy -ErrorAction SilentlyContinue) { throw 'Tunnel gateway already running. Stop it before restarting.' }
}
$proxy = Start-Process (Get-Command node).Source -ArgumentList @("`"$(Join-Path $PSScriptRoot 'shopify-tunnel-proxy.cjs')`"") -WorkingDirectory $repo -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $runtime 'shopify-tunnel-proxy.stdout.log') -RedirectStandardError (Join-Path $runtime 'shopify-tunnel-proxy.stderr.log')
@{proxy=$proxy.Id} | ConvertTo-Json | Set-Content (Join-Path $runtime 'shopify-tunnel-processes.json')
$tunnel = Start-Process (Join-Path $runtime 'cloudflared.exe') -ArgumentList @('tunnel','--url','http://127.0.0.1:5523','--no-autoupdate','--protocol','http2') -WorkingDirectory $repo -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $runtime 'shopify-tunnel.stdout.log') -RedirectStandardError (Join-Path $runtime 'shopify-tunnel.stderr.log')
@{proxy=$proxy.Id;tunnel=$tunnel.Id} | ConvertTo-Json | Set-Content (Join-Path $runtime 'shopify-tunnel-processes.json')
Write-Output 'Temporary tunnel starting. URL will appear in artifacts/shopify-tunnel.stderr.log.'

