$ErrorActionPreference = 'Stop'
# Inspect tracked config only; never print credential values.
$files = @(git ls-files -- '*appsettings*.json')
if ($LASTEXITCODE -ne 0) { throw 'Unable to list tracked configuration.' }
$failed = $false
foreach ($file in $files) {
    $config = Get-Content -LiteralPath $file -Raw | ConvertFrom-Json
    if (-not [string]::IsNullOrWhiteSpace($config.SmtpSettings.Password)) {
        Write-Error "SMTP password must be empty in tracked configuration: $file" -ErrorAction Continue
        $failed = $true
    }
}
if ($failed) { exit 1 }
Write-Output 'Tracked SMTP configuration contains no passwords.'
