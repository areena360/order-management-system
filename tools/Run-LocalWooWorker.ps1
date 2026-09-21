$ErrorActionPreference = 'Continue'
$repo = Split-Path -Parent $PSScriptRoot
while ($true) {
    if (Test-Path -LiteralPath (Join-Path $repo 'artifacts/wordpress-local/wordpress/wp-config.php')) {
        & (Join-Path $repo 'artifacts/php/php.exe') (Join-Path $PSScriptRoot 'local-wordpress.php') tick
    }
    Start-Sleep -Seconds 60
}
