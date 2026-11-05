param([string]$OutputDirectory = (Join-Path $PSScriptRoot '../artifacts/packages'))
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot
$feed = [IO.Path]::GetFullPath($OutputDirectory)
$modules = @('Text','Files','Formats','Sessions','Core')
[xml]$moduleProperties = Get-Content -LiteralPath (Join-Path $repo 'src/ModulePackage.props') -Raw
$version = [string]$moduleProperties.Project.PropertyGroup.Version
if ([string]::IsNullOrWhiteSpace($version)) { throw 'Missing or empty canonical module version in src/ModulePackage.props' }
New-Item -ItemType Directory -Path $feed -Force | Out-Null
foreach ($module in $modules) {
    dotnet pack (Join-Path $repo "src/DiffBeacon.$module") -c Release --no-build -o $feed
    if ($LASTEXITCODE -ne 0) { throw "Pack failed: $module" }
    $package = Join-Path $feed "DiffBeacon.$module.$version.nupkg"
    $zip = [IO.Compression.ZipFile]::OpenRead($package)
    try {
        $entry = $zip.GetEntry("DiffBeacon.$module.nuspec")
        if (!$entry) { throw "Missing nuspec: $module" }
        $reader = [IO.StreamReader]::new($entry.Open())
        try { [xml]$spec = $reader.ReadToEnd() } finally { $reader.Dispose() }
        $meta = $spec.package.metadata
        if ($meta.id -ne "DiffBeacon.$module" -or $meta.version -ne $version -or $meta.license.InnerText -ne 'MIT' -or
            $meta.readme -ne 'README.md' -or $meta.repository.url -ne 'https://github.com/SeanShaffer88/DiffBeacon') { throw "Invalid metadata: $module" }
        $libraries = @($zip.Entries.FullName | Where-Object { $_ -like 'lib/*' })
        if ($libraries.Count -ne 1 -or $libraries[0] -ne "lib/net10.0/DiffBeacon.$module.dll" -or !$zip.GetEntry('README.md')) { throw "Invalid library content: $module" }
        if (@($zip.Entries.FullName | Where-Object { $_ -match '(?i)\.(exe|png|pdb)$|(^|/)(tests|artifacts|credentials)/' }).Count) { throw "Unexpected content: $module" }
        $expected = switch ($module) { Files { @('DiffBeacon.Text') }; Sessions { @('DiffBeacon.Files','DiffBeacon.Text') }; Core { @('DiffBeacon.Files','DiffBeacon.Formats','DiffBeacon.Sessions','DiffBeacon.Text') }; default { @() } }
        $actual = @($meta.dependencies.group.dependency | Where-Object { $_ } | ForEach-Object { $_.id } | Sort-Object)
        if (($actual -join ',') -ne (($expected | Sort-Object) -join ',')) { throw "Invalid dependencies: $module" }
    } finally { $zip.Dispose() }
}
$temp = Join-Path ([IO.Path]::GetTempPath()) ('DiffBeacon-PackageConsumers-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory $temp | Out-Null
Write-Output "Package consumer evidence: $temp"
'<configuration><packageSources><clear /></packageSources></configuration>' | Set-Content (Join-Path $temp 'NuGet.Config')
'<Project />' | Set-Content (Join-Path $temp 'Isolated.props')
'<Project />' | Set-Content (Join-Path $temp 'Isolated.targets')
foreach ($module in $modules) {
    $consumer = Join-Path $temp $module
    New-Item -ItemType Directory $consumer | Out-Null
    @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable><TreatWarningsAsErrors>true</TreatWarningsAsErrors></PropertyGroup>
  <ItemGroup><PackageReference Include="DiffBeacon.$module" Version="$version" /></ItemGroup>
</Project>
"@ | Set-Content (Join-Path $consumer 'Consumer.csproj')
    if ($module -eq 'Core') {
        @'
using DiffBeacon.Core;
if (TextComparison.Compare("x", "x", new()).Differences != 0 || DelimitedText.Parse("a,b")[0].Length != 2) throw new Exception("Core consumption failed");
if (typeof(Session).Assembly.GetName().Name != "DiffBeacon.Sessions" || typeof(SafeFiles).Assembly.GetName().Name != "DiffBeacon.Files") throw new Exception("Transitive package identity failed");
Console.WriteLine("Core package and transitive modules passed.");
'@ | Set-Content (Join-Path $consumer 'Program.cs')
    } else { Copy-Item (Join-Path $repo "examples/$module/Program.cs") (Join-Path $consumer 'Program.cs') }
    $properties = @("-p:DirectoryBuildPropsPath=$temp/Isolated.props", "-p:DirectoryBuildTargetsPath=$temp/Isolated.targets")
    dotnet restore (Join-Path $consumer 'Consumer.csproj') --configfile (Join-Path $temp 'NuGet.Config') --source $feed --packages (Join-Path $temp 'cache') -p:NuGetAudit=false @properties
    if ($LASTEXITCODE -ne 0) { throw "Local feed restore failed: $module" }
    dotnet build (Join-Path $consumer 'Consumer.csproj') -c Release --no-restore @properties
    if ($LASTEXITCODE -ne 0) { throw "Local feed build failed: $module" }
    dotnet (Join-Path $consumer 'bin/Release/net10.0/Consumer.dll')
    if ($LASTEXITCODE -ne 0) { throw "Local feed run failed: $module" }
}
Write-Output "Verified five packages and five isolated consumers. Evidence retained at $temp"
