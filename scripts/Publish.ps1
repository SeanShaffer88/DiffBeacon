param([string]$OutputDirectory = (Join-Path $PSScriptRoot '../artifacts'))
$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'Build.ps1')
$repo = Split-Path $PSScriptRoot
$destination = Join-Path ([IO.Path]::GetFullPath($OutputDirectory)) 'DiffBeacon-Windows-x64'
dotnet publish (Join-Path $repo 'src/DiffBeacon.App') -c Release -r win-x64 --self-contained false -o $destination
if ($LASTEXITCODE -ne 0) { throw 'Publish failed' }
Copy-Item (Join-Path $repo 'README.md'),(Join-Path $repo 'LICENSE') $destination
foreach ($name in @('docs','assets')) {
    # These two package subdirectories are generated copies. Recreate them to avoid
    # Copy-Item nesting docs/docs and assets/assets on a repeated publish.
    $target = [IO.Path]::GetFullPath((Join-Path $destination $name))
    $expected = [IO.Path]::GetFullPath($destination).TrimEnd('\') + '\' + $name
    if ($target -ne $expected) { throw 'Package content target escaped its expected directory' }
    if (Test-Path -LiteralPath $target) {
        $resolved = (Resolve-Path -LiteralPath $target).Path
        if ($resolved -ne $expected) { throw 'Package content target resolved outside its expected directory' }
        Remove-Item -LiteralPath $target -Recurse -Force
    }
    Copy-Item -LiteralPath (Join-Path $repo $name) -Destination $target -Recurse -Force
}
Compress-Archive -Path (Join-Path $destination '*') -DestinationPath "$destination.zip" -Force
Write-Output "Portable Package: $destination.zip"
