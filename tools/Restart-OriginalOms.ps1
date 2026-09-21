$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$api = Join-Path $repo 'OMS_Backend/OMS_Backend'
$hostConfig = Join-Path $repo 'OMS_Backend/.vs/OMS_Backend/config/applicationhost.config'
if (!(Test-Path -LiteralPath $hostConfig)) { throw 'Existing Visual Studio IIS Express configuration not found.' }
# Only restart the IIS Express instance for this exact existing checkout.
Get-CimInstance Win32_Process -Filter "Name='iisexpress.exe'" | Where-Object {
    $_.CommandLine -and $_.CommandLine.Contains($hostConfig)
} | ForEach-Object { Stop-Process -Id $_.ProcessId -ErrorAction Stop }
& dotnet build (Join-Path $api 'OMS_Backend.csproj') -c Debug --no-restore
if ($LASTEXITCODE -ne 0) { throw 'Original OMS build failed; restart from Visual Studio after resolving the build error.' }
$variables = @{
    ASPNETCORE_ENVIRONMENT = 'Development'
    LAUNCHER_PATH = (Join-Path $api 'bin/Debug/net9.0/OMS_Backend.exe')
    LAUNCHER_ARGS = ''
}
$previous = @{}
try {
    foreach ($key in $variables.Keys) {
        $previous[$key] = [Environment]::GetEnvironmentVariable($key, 'Process')
        [Environment]::SetEnvironmentVariable($key, $variables[$key], 'Process')
    }
    $process = Start-Process -FilePath "$env:ProgramFiles\IIS Express\iisexpress.exe" -ArgumentList @("/config:`"$hostConfig`"", '/site:OMS_Backend', '/apppool:"OMS_Backend AppPool"') -WorkingDirectory $api -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $repo 'artifacts/original-oms.stdout.log') -RedirectStandardError (Join-Path $repo 'artifacts/original-oms.stderr.log')
    $process.Id | Set-Content (Join-Path $repo 'artifacts/original-oms.pid')
} finally {
    foreach ($key in $previous.Keys) { [Environment]::SetEnvironmentVariable($key, $previous[$key], 'Process') }
}
Write-Output 'Original OMS restarted using its existing IIS Express configuration: https://localhost:44370'
