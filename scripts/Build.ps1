$ErrorActionPreference = 'Stop'
Push-Location (Join-Path $PSScriptRoot '..')
try {
    dotnet build DiffBeacon.sln -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
    dotnet run --project tests/DiffBeacon.Tests -c Release --no-build
    if ($LASTEXITCODE -ne 0) { throw 'Tests failed' }
} finally { Pop-Location }
