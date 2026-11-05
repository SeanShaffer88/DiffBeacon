# Reusable modules (0.2.0-alpha.1)

The application remains alpha. These five MIT packages target net10.0 and use only the .NET framework. All public types retain namespace `DiffBeacon.Core`.

| Package | Entry points | Dependencies |
| --- | --- | --- |
| DiffBeacon.Text | TextComparison.Compare, InlineDifferences.Compare, TextScrollMap.Create, TextEditProposal.Replace, AlignedBlockMerge.Plan/Apply, ThreeWayMerge.Create/Render | Framework only |
| DiffBeacon.Files | TextDocument.Load/Encode, FileStamp.Capture, SafeFiles.Write, BinaryComparison.Compare, FolderComparison.Compare, FolderFilters.Parse | Text |
| DiffBeacon.Formats | DelimitedText.Parse, Reports.Html | Framework only |
| DiffBeacon.Sessions | Session.Load/Save | Text, Files |
| DiffBeacon.Core | Compatibility type-forwarding facade | All four modules |

Dependency graph: App → Core → {Text, Files, Formats, Sessions}; Sessions → {Text, Files}; Files → Text. No module depends on App or Core. Table presentation and comparison orchestration remain in the application; Formats supplies parsing and HTML generation, not a table comparison engine.

## Consumption

Use SDK 10 and run `scripts/Build.ps1`. This runs the existing harness, four direct module examples, packs five libraries into `artifacts/packages`, inspects package contents/dependencies, then restores/builds/runs consumers against that local feed in a unique temporary directory with isolated build configuration, package cache and cleared package sources. No NuGet publication occurs.

Add `<PackageReference Include="DiffBeacon.Text" Version="0.2.0-alpha.1" />` to a net10.0 project, restore from the local feed, import `DiffBeacon.Core`, and call `TextComparison.Compare("before", "after", new CompareOptions())`. Select the package containing the required entry points. Use the Core package for an existing Core dependency.

Run each example independently with `dotnet run --project examples/Text -c Release` (or Files, Formats, Sessions). Each references its selected module directly. Files and Sessions create unique temporary directories and remove their own files afterward. Files explicitly overwrites its fixture and verifies the backup before cleanup.

## Limits and effects

Text comparison accepts 4,000,000 combined characters and 100,000 combined logical lines. The default alignment budget is 2,000,000 cells; overflow reports positional fallback. Inline comparison has explicit token/cell/span limits and fallback reasons. Block planning requires strict current snapshots, no ignore options and exact alignment; Apply rejects stale snapshots. Three-way merging shares the combined character/line budgets, rejects positional fallback, and requires an explicit choice for every conflict. Text operations do not write files. Comparison/merge APIs support cancellation where their signatures expose a token.

TextDocument.Load reads at most 16 MiB, rejects invalid encoding/NUL and enforces the decoded text budget. Encode preserves BOM and the loaded newline convention and rejects mixed newline conventions. SafeFiles.Write rejects reparse ancestors, requires explicit overwrite, checks supplied file stamps, writes a sibling temporary file, and replaces existing files with a uniquely named backup. Successful overwrites retain backups for the caller to manage. A supplied expected stamp rejects stale saves; callers choose destinations and handle filesystem exceptions.

Binary comparison streams full files, retains at most 512 samples, checks input changes and supports cancellation. Folder comparison supports cancellation, bounded traversal and explicit filters; inspect Complete and IncompleteReasons rather than treating incomplete scans as equality.

DelimitedText.Parse has its own private 4,000,000-character limit (currently matching Text), 200,000 cells and 50,000 rows; malformed quoting is rejected. It has no cancellation parameter. Reports.Html escapes title, summary and cells, returning the full HTML in memory without a separate output budget. Session.Load/Save accept versions 1–3, validate metadata/preferences/filters, enforce 64 KiB and use safe writes; sessions store paths/preferences rather than document contents. These synchronous APIs have no cancellation parameter.

Libraries use a platform-neutral target. Filesystem replacement, backups, permissions and reparse behavior depend on the OS/filesystem. Windows is the exercised application platform; extraction does not certify other platforms.

## Compatibility

Core forwards every original public type without duplicate types or wrappers. Existing app/test references remain unchanged. The harness compares exported types and declared public members against the original working-tree assembly snapshot and verifies runtime forwarding identities. Defining assemblies now differ: reflection code assuming all types are defined in Core, enumerating only its exported/defined types, or inspecting assembly-qualified identities may need adjustment.
