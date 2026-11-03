param([string]$OutputDirectory = (Join-Path $PSScriptRoot '../artifacts'))
$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'Build.ps1')
$repo = Split-Path $PSScriptRoot
$destination = Join-Path ([IO.Path]::GetFullPath($OutputDirectory)) 'DiffBeacon-Windows-x64'
dotnet publish (Join-Path $repo 'src/DiffBeacon.App') -c Release -r win-x64 --self-contained false -o $destination
if ($LASTEXITCODE -ne 0) { throw 'Publish failed' }
Copy-Item (Join-Path $repo 'README.md'),(Join-Path $repo 'LICENSE') $destination
Copy-Item (Join-Path $repo 'docs') (Join-Path $destination 'docs') -Recurse -Force
Copy-Item (Join-Path $repo 'assets') (Join-Path $destination 'assets') -Recurse -Force
Compress-Archive -Path (Join-Path $destination '*') -DestinationPath "$destination.zip" -Force
Write-Output "Portable Package: $destination.zip"
