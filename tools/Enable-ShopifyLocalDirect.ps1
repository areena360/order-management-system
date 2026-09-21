[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$settings = Join-Path $repo 'OMS_Backend/OMS_Backend/appsettings.Local.json'
if (!(Test-Path -LiteralPath $settings)) { throw 'Run Configure-Shopify.ps1 first so private Shopify credentials exist.' }

$config = Get-Content -LiteralPath $settings -Raw | ConvertFrom-Json
if (!$config.Shopify -or [string]::IsNullOrWhiteSpace($config.Shopify.ClientId) -or [string]::IsNullOrWhiteSpace($config.Shopify.ClientSecret)) {
    throw 'Shopify Client ID and secret must already be configured.'
}
$config.Shopify | Add-Member -NotePropertyName LocalDirectMode -NotePropertyValue $true -Force
$config | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath $settings -Encoding utf8
Write-Output 'Local direct mode enabled. Restart the OMS backend before connecting Shopify from localhost.'
