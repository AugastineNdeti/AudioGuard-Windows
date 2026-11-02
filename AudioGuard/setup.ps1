$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path

Write-Host 'AudioGuard setup' -ForegroundColor Cyan
Write-Host 'This will publish AudioGuard, install it for your Windows user, and register automatic launch at sign-in.'

$sdk = Get-Command dotnet -ErrorAction SilentlyContinue
if (-not $sdk) {
    throw '.NET 10 SDK was not found. Install the .NET 10 SDK, reopen PowerShell, and run setup.ps1 again.'
}

$versionText = (& dotnet --list-sdks) -join "`n"
if ($versionText -notmatch '(?m)^10\.') {
    throw '.NET 10 SDK is required. Install it from https://dotnet.microsoft.com/download/dotnet/10.0 and run setup.ps1 again.'
}

& (Join-Path $root 'publish.ps1')
if ($LASTEXITCODE -ne 0) {
    throw 'Publish failed. Review the build output above.'
}

& (Join-Path $root 'install.ps1')
if ($LASTEXITCODE -ne 0) {
    throw 'Installation or startup-task registration failed. Review the output above.'
}

Write-Host ''
Write-Host 'AudioGuard setup completed.' -ForegroundColor Green
Write-Host 'It will launch automatically whenever this Windows account signs in.'
Write-Host 'Use the AudioGuard shield icon in the notification area to control it.'
