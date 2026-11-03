# DiffBeacon

Native Windows file comparison, built around clear review and explicit saving.

<img src="assets/DiffBeacon.png" alt="Selected DiffBeacon lighthouse icon" width="128">

**Core Preview:** a working first milestone, not yet the full professional feature list. See the [feature matrix](docs/FEATURES.md) for current limits and planned capabilities.

- Compare and edit two text files or pasted snippets; align inserted/deleted/changed lines and ignore case, whitespace or blank lines.
- Navigate differences, merge selected text into a selected destination range, accept all left content into the right editor, and undo up to 20 merge operations.
- Save explicitly with original Unicode encoding/BOM/newline convention, stale-file checks, atomic replacement and backups beside overwritten destinations.
- Compare recursive folders by file content; inspect file sizes, missing files and errors in a results grid.
- Stream whole binary files using fixed-size buffers; show difference counts, offsets and up to 512 hex/ASCII byte samples.
- Compare CSV/TSV cells by row/column position, including quoted and multiline fields.
- Save path/options sessions, export escaped HTML, open concurrent comparison tabs, and drag files/folders onto path fields.
- Run scripted equality checks or launch from Git difftool. No telemetry, automatic uploads, third-party packages or automatic Git configuration changes.

## Run

Windows x64 with the **.NET 10 Desktop Runtime** is required. Extract the portable ZIP, then run `DiffBeacon.exe`. This build is console-enabled so `--check` and `--help` produce script-readable output; GUI launches may also show a console window.

Choose a mode, select two paths, then **Compare**. For snippets, leave paths blank and paste into the Text editors. Select source text and select a destination range/caret before **Selected → Right** or **Left ← Selected**. Merges change the editor only; use **Save Left As…** or **Save Right As…** to write. **New Snippets** resets the pair after a discard warning.

Existing-file replacement retains a sibling `.diffbeacon-backup-<id>` file. Backups are not automatically deleted. Review paths before saving. Folder and table modes are currently read-only. Mixed-newline text saves are refused to avoid silent normalization. Unsupported text should be compared in Binary mode.

## Limits

Detailed text comparison supports at most 4 million combined characters and 100,000 combined lines. A bounded alignment matrix uses positional fallback above 2 million cells. Each text file has a 16 MiB loading ceiling; larger files can be compared fully in Binary mode, without editing. Binary samples are limited, but equality/counting scans the entire input. Folder enumeration retains up to 50,000 paths per root and reports errors/skipped reparse points as incomplete. Grids show at most 10,000 rows. CSV parsing is capped at 200,000 cells per input, with first 10,000 compared cells retained. Only the first 2,000 differing text rows are colored in the editors; the results grid and summary cover retained comparisons.

## Build And Verify

Requires the .NET 10 SDK on Windows; uses only the installed SDK and standard library.

```powershell
./scripts/Build.ps1
./scripts/Publish.ps1
./src/DiffBeacon.App/bin/Release/net10.0-windows/DiffBeacon.exe --smoke "$PWD/artifacts/Preview.png"
```

Build outputs are ignored by Git. Publish creates a framework-dependent Windows x64 ZIP under `artifacts/`. Core tests are a dependency-free console harness. Native smoke verifies comparison, merge/undo, result invalidation, cancellation through mutation, mode switches, and rendering. Dialog automation, full accessibility, multi-GB performance and every advanced integration still require further acceptance testing.

[CLI And Git Setup](docs/CLI.md) · [Feature Matrix](docs/FEATURES.md) · [Specification](docs/SPEC.md) · [Icon Concepts](docs/Icon-Concepts)

MIT licensed. No tags or releases are created by the build scripts.
