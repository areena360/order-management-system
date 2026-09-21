$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$record = Join-Path $repo 'artifacts/woo-tunnel-processes.json'
if (!(Test-Path $record)) { return }
$owned = Get-Content $record -Raw | ConvertFrom-Json
foreach ($entry in $owned.PSObject.Properties) {
    $process = Get-CimInstance Win32_Process -Filter "ProcessId=$($entry.Value)"
    if (!$process) { continue }
    if (($entry.Name -eq 'proxy' -and $process.CommandLine.Contains((Join-Path $PSScriptRoot 'woo-tunnel-proxy.cjs'))) -or
        ($entry.Name -eq 'tunnel' -and $process.ExecutablePath -eq (Join-Path $repo 'artifacts/cloudflared.exe') -and $process.CommandLine.Contains('http://127.0.0.1:5522'))) {
        Stop-Process -Id $process.ProcessId
    } else { throw 'Process identity changed; refusing to stop it.' }
}
Remove-Item -LiteralPath $record
Write-Output 'Public tunnel stopped. Original OMS remains running.'
