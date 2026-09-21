$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$runtime = Join-Path $repo 'artifacts'
$dbRoot = Join-Path $runtime 'mariadb/mariadb-11.4.12-winx64'
$data = Join-Path $runtime 'wordpress-db'
$settingsFile = Join-Path $runtime 'wordpress-local.json'
if (-not (Test-Path -LiteralPath $settingsFile)) {
    $settings = @{
        rootPassword = [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(24))
        dbPassword = [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(24))
        adminPassword = 'Local!9' + [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(10))
        adminUser = 'oms_admin'
    }
    $settings | ConvertTo-Json | Set-Content -LiteralPath $settingsFile -Encoding utf8
}
$settings = Get-Content -LiteralPath $settingsFile -Raw | ConvertFrom-Json
if (-not (Test-Path -LiteralPath (Join-Path $data 'mysql'))) {
    & (Join-Path $dbRoot 'bin/mariadb-install-db.exe') "--datadir=$data" "--password=$($settings.rootPassword)" '--port=3308' | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'MariaDB initialization failed.' }
}
$ini = @"
[mysqld]
basedir=$($dbRoot.Replace('\','/'))
datadir=$($data.Replace('\','/'))
port=3308
bind-address=127.0.0.1
max_allowed_packet=64M
character-set-server=utf8mb4
"@
$ini | Set-Content -LiteralPath (Join-Path $data 'my.ini') -Encoding ascii
$phpRoot = Join-Path $runtime 'php'
$phpIni = @"
extension_dir="$($phpRoot.Replace('\','/'))/ext"
extension=curl
extension=openssl
extension=mysqli
extension=mbstring
extension=fileinfo
extension=gd
extension=intl
extension=zip
memory_limit=512M
max_execution_time=120
upload_max_filesize=32M
post_max_size=32M
display_errors=Off
log_errors=On
error_log="$($runtime.Replace('\','/'))/wordpress-php.log"
date.timezone=UTC
"@
$phpIni | Set-Content -LiteralPath (Join-Path $phpRoot 'php.ini') -Encoding ascii
Write-Output 'Portable local runtime initialized. No Windows services were installed.'
