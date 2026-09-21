$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$source = Join-Path $repo 'WooCommerce/oms-woocommerce'
$version = [regex]::Match((Get-Content (Join-Path $source 'oms-woocommerce.php') -Raw), 'Version:\s*([0-9.]+)').Groups[1].Value
if (!$version) { throw 'Plugin version not found.' }
$output = Join-Path $repo "artifacts/oms-woocommerce-$version.zip"
$php = Join-Path $repo 'artifacts/php/php.exe'
if (-not (Test-Path -LiteralPath $php)) { $php = (Get-Command php -ErrorAction Stop).Source }
foreach ($file in Get-ChildItem -LiteralPath $source -Filter '*.php' -Recurse) {
    & $php -l $file.FullName
    if ($LASTEXITCODE -ne 0) { throw "PHP syntax check failed: $($file.Name)" }
}
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $output) | Out-Null
Compress-Archive -LiteralPath $source -DestinationPath $output -Force
$hash = (Get-FileHash -LiteralPath $output -Algorithm SHA256).Hash.ToLowerInvariant()
"$hash  oms-woocommerce-$version.zip" | Set-Content -LiteralPath "$output.sha256"
Write-Output "Plugin ZIP ready: $output"
