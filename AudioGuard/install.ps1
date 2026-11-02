$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$source = Join-Path $root 'publish\AudioGuard'
$exe = Join-Path $source 'AudioGuard.exe'
if (-not (Test-Path $exe)) {
    throw 'AudioGuard.exe not found. Run .\publish.ps1 first.'
}
$destination = Join-Path $env:LOCALAPPDATA 'Programs\AudioGuard'
New-Item -ItemType Directory -Force -Path $destination | Out-Null
Copy-Item (Join-Path $source '*') $destination -Recurse -Force
$installedExe = Join-Path $destination 'AudioGuard.exe'
$taskName = 'AudioGuard'
$action = New-ScheduledTaskAction -Execute $installedExe
$trigger = New-ScheduledTaskTrigger -AtLogOn -User $env:USERNAME
$principal = New-ScheduledTaskPrincipal -UserId $env:USERNAME -LogonType Interactive -RunLevel Limited
$settings = New-ScheduledTaskSettingsSet -StartWhenAvailable -ExecutionTimeLimit (New-TimeSpan -Days 0)
Register-ScheduledTask -TaskName $taskName -Action $action -Trigger $trigger -Principal $principal -Settings $settings -Description 'Starts AudioGuard audio safety monitoring at sign-in.' -Force | Out-Null
Start-Process $installedExe
Write-Host "AudioGuard installed and scheduled at sign-in: $installedExe" -ForegroundColor Green
