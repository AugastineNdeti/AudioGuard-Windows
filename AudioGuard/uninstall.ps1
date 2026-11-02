$ErrorActionPreference = 'Continue'
$taskName = 'AudioGuard'
Unregister-ScheduledTask -TaskName $taskName -Confirm:$false -ErrorAction SilentlyContinue
$destination = Join-Path $env:LOCALAPPDATA 'Programs\AudioGuard'
Get-Process AudioGuard -ErrorAction SilentlyContinue | Stop-Process -Force
if (Test-Path $destination) { Remove-Item $destination -Recurse -Force }
Write-Host 'AudioGuard startup task and installed application removed. Logs in %LOCALAPPDATA%\AudioGuard were retained.' -ForegroundColor Green
