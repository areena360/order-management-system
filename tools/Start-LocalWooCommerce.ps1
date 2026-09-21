param([switch]$SkipFrontend)
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$runtime = Join-Path $repo 'artifacts'
$processFile = Join-Path $runtime 'local-processes.json'
$owned = @{}
if (Test-Path -LiteralPath $processFile) {
    $old = Get-Content -LiteralPath $processFile -Raw | ConvertFrom-Json -AsHashtable
    foreach ($key in $old.Keys) { if (Get-Process -Id $old[$key] -ErrorAction SilentlyContinue) { $owned[$key] = $old[$key] } }
}
function Start-LocalProcess($name, $exe, $arguments, $directory) {
    if ($owned.ContainsKey($name)) { return }
    $p = Start-Process -FilePath $exe -ArgumentList $arguments -WorkingDirectory $directory -WindowStyle Hidden -PassThru `
        -RedirectStandardOutput (Join-Path $runtime "$name.stdout.log") -RedirectStandardError (Join-Path $runtime "$name.stderr.log")
    $owned[$name] = $p.Id
    $owned | ConvertTo-Json | Set-Content -LiteralPath $processFile -Encoding utf8
}
$dbRoot = Join-Path $runtime 'mariadb/mariadb-11.4.12-winx64'
Start-LocalProcess 'woo-db' (Join-Path $dbRoot 'bin/mariadbd.exe') @("--defaults-file=`"$(Join-Path $runtime 'wordpress-db/my.ini')`"", '--console') $dbRoot
$demo = Get-Content -LiteralPath (Join-Path $runtime 'local-demo.json') -Raw | ConvertFrom-Json
$apiDir = Join-Path $repo 'OMS_Backend/OMS_Backend'
$dll = Join-Path $apiDir 'bin/Release/net9.0/OMS_Backend.dll'
$apiEnvironment = @{
    ConnectionStrings__DefaultConnection=$demo.connectionString; JwtSettings__Secret=$demo.jwtSecret
    JwtSettings__Issuer='OMS-Local-Demo'; JwtSettings__Audience='OMS-Local-Demo'
    ASPNETCORE_ENVIRONMENT='Development'; ASPNETCORE_URLS='http://localhost:5511'
    WooCommerce__Enabled='true'; WooCommerce__AllowLocalHttp='true'
}
$previousEnvironment = @{}
try {
    foreach ($key in $apiEnvironment.Keys) {
        $previousEnvironment[$key] = [Environment]::GetEnvironmentVariable($key, 'Process')
        [Environment]::SetEnvironmentVariable($key, $apiEnvironment[$key], 'Process')
    }
    Start-LocalProcess 'woo-api' (Get-Command dotnet).Source @("`"$dll`"") $apiDir
} finally {
    foreach ($key in $previousEnvironment.Keys) { [Environment]::SetEnvironmentVariable($key, $previousEnvironment[$key], 'Process') }
}
Start-LocalProcess 'woo-wordpress' (Join-Path $runtime 'php/php.exe') @('-S', 'localhost:8088', '-t', "`"$(Join-Path $runtime 'wordpress-local/wordpress')`"") $repo
Start-LocalProcess 'woo-worker' (Get-Command pwsh).Source @('-NoProfile', '-File', "`"$(Join-Path $PSScriptRoot 'Run-LocalWooWorker.ps1')`"") $repo
if (-not $SkipFrontend) {
    Start-LocalProcess 'woo-angular' (Get-Command node).Source @('node_modules/@angular/cli/bin/ng.js','serve','--configuration','woocommerce-local','--host','localhost','--port','4201') (Join-Path $repo 'OMS_Frontend')
}
Write-Output 'Local demo: OMS http://localhost:4201 | API http://localhost:5511 | WordPress http://localhost:8088'
Write-Output 'Credentials: artifacts/local-demo.json and artifacts/wordpress-local.json (not committed).'
