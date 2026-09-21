$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$file = Join-Path $repo 'artifacts/local-processes.json'
if (-not (Test-Path -LiteralPath $file)) { return }
$owned = Get-Content -LiteralPath $file -Raw | ConvertFrom-Json -AsHashtable
foreach ($name in $owned.Keys) {
    $process = Get-CimInstance Win32_Process -Filter "ProcessId=$($owned[$name])"
    if (-not $process) { continue }
    # Only stop the recorded processes when their command line identifies this demo.
    $command = ([string]$process.CommandLine).Replace('\','/')
    $normalizedRepo = $repo.Replace('\','/')
    $isDemo = ($command.Contains($normalizedRepo) -and ($command.Contains('wordpress-db') -or $command.Contains('wordpress-local') -or $command.Contains('bin/Release/net9.0/OMS_Backend.dll') -or $command.Contains('Run-LocalWooWorker.ps1'))) -or
        ($name -eq 'woo-angular' -and $command.Contains('woocommerce-local') -and $command.Contains('4201'))
    if (-not $isDemo) { throw "Refusing to stop process $($owned[$name]): not recognized as this local demo." }
    Stop-Process -Id $owned[$name]
}
'{}' | Set-Content -LiteralPath $file
Write-Output 'Local demo processes stopped. Databases and files retained.'
