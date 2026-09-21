$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$runtime = Join-Path $repo 'artifacts'
if (!(Test-Path (Join-Path $runtime 'woocommerce-tunnel-web/browser/index.html'))) { throw 'Build woocommerce-tunnel frontend first.' }
if (Test-Path (Join-Path $runtime 'woo-tunnel-processes.json')) {
    $old = Get-Content (Join-Path $runtime 'woo-tunnel-processes.json') -Raw | ConvertFrom-Json
    if (Get-Process -Id $old.proxy -ErrorAction SilentlyContinue) { throw 'Tunnel gateway already running. Stop it before restarting.' }
}
$proxy = Start-Process (Get-Command node).Source -ArgumentList @("`"$(Join-Path $PSScriptRoot 'woo-tunnel-proxy.cjs')`"") -WorkingDirectory $repo -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $runtime 'woo-tunnel-proxy.stdout.log') -RedirectStandardError (Join-Path $runtime 'woo-tunnel-proxy.stderr.log')
@{proxy=$proxy.Id} | ConvertTo-Json | Set-Content (Join-Path $runtime 'woo-tunnel-processes.json')
$tunnel = Start-Process (Join-Path $runtime 'cloudflared.exe') -ArgumentList @('tunnel','--url','http://127.0.0.1:5522','--no-autoupdate','--protocol','http2') -WorkingDirectory $repo -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $runtime 'woo-tunnel.stdout.log') -RedirectStandardError (Join-Path $runtime 'woo-tunnel.stderr.log')
@{proxy=$proxy.Id;tunnel=$tunnel.Id} | ConvertTo-Json | Set-Content (Join-Path $runtime 'woo-tunnel-processes.json')
Write-Output 'Temporary tunnel starting. URL will appear in artifacts/woo-tunnel.stderr.log.'
