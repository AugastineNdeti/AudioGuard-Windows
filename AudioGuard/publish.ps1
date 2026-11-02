$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$project = Join-Path $root 'src\AudioGuard\AudioGuard.csproj'
$out = Join-Path $root 'publish\AudioGuard'
New-Item -ItemType Directory -Force -Path $out | Out-Null

dotnet publish $project -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:EnableCompressionInSingleFile=true -o $out
Write-Host "Published AudioGuard to $out" -ForegroundColor Green
