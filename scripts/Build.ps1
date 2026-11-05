$ErrorActionPreference = 'Stop'
Push-Location (Join-Path $PSScriptRoot '..')
try {
    dotnet build DiffBeacon.sln -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
    dotnet run --project tests/DiffBeacon.Tests -c Release --no-build
    if ($LASTEXITCODE -ne 0) { throw 'Tests failed' }
    foreach ($module in @('Text','Files','Formats','Sessions')) {
        dotnet run --project "examples/$module" -c Release
        if ($LASTEXITCODE -ne 0) { throw "Example failed: $module" }
    }
    & (Join-Path $PSScriptRoot 'Verify-Packages.ps1')
} finally { Pop-Location }
