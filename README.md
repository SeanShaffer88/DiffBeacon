# DiffBeacon

Native Windows file comparison, built around clear review and explicit saving.

The native interface uses the [VaporGrid dark adaptation](docs/VAPORGRID.md), with flat comparison content and restrained command surfaces.

DiffBeacon's product goal is a complete local comparison and merge workflow replacement for daily text, folder, binary, table, document and Git work. It requires zero mandatory paid software, subscriptions or metered APIs. Planned formats must work through free local implementations; they must not require installed Office or paid codecs. Third-party dependencies require prior user approval. Billed, metered and paid-only integrations are excluded; a service's free tier alone does not establish a zero-cost path.

<img src="assets/DiffBeacon.png" alt="Selected DiffBeacon lighthouse icon" width="128">

**Alpha / Core Preview:** a working first milestone with substantial workflows still incomplete. Full replacement readiness has not been established. See the [feature matrix](docs/FEATURES.md) for current limits, ordered milestones and acceptance gates.

- Compare and edit two text files or pasted snippets; align inserted/deleted/changed lines and ignore case, whitespace or blank lines.
- Review a three-way Base/Local/Remote merge in a dedicated tab. Disjoint edits merge automatically; overlapping conflicts require explicit Local, Remote or Both choices before saving. Source panes and the merged preview stay read-only.
- Scrub a proportional difference overview by mouse or keyboard; navigate differences, merge selected text into a selected destination range, accept all left content into the right editor, and undo up to 20 merge operations.
- Merge a selected aligned difference block in either direction. Block actions replace the whole contiguous hunk in the destination editor, including insertions/deletions; they require a fresh exact comparison with all ignore options off.
- Save explicitly with original Unicode encoding/BOM/newline convention, stale-file checks, atomic replacement and backups beside overwritten destinations.
- Compare recursive folders by file content; inspect file sizes, missing files and errors in a results grid.
- Scope folder comparisons with basename globs, `NOT` exclusions and exact ignored directory names. Review normalized rules and per-root exclusion/pruning counts; named sessions retain the scope.
- Stream whole binary files using fixed-size buffers; show difference counts, offsets and up to 512 hex/ASCII byte samples.
- Compare CSV/TSV cells by row/column position, including quoted and multiline fields.
- Save path/options sessions, export escaped HTML, open concurrent comparison tabs, and drag files/folders onto path fields.
- Run scripted equality checks or launch from Git difftool. No telemetry, automatic uploads, third-party packages or automatic Git configuration changes.

## Run

Windows x64 with the **.NET 10 Desktop Runtime** is required. Extract the portable ZIP, then run `DiffBeacon.exe`. The desktop app opens without a console window. CLI commands attach to the calling console (or open one when launched outside a terminal) so `--check` and `--help` produce script-readable output.

Choose a mode, select two paths, then **Compare**. For snippets, leave paths blank and paste into the Text editors. Select source text and select a destination range/caret before **Selected → Right** or **Left ← Selected**. Merges change the editor only; use **Save Left As…** or **Save Right As…** to write. **New Snippets** resets the pair after a discard warning.

The startup workspace has independent Left and Right open/drop targets and a Paste Snippets action. A grouped **Home / Merge / View** command deck sits above the comparison session tabs. Text starts **side by side**, with each path and Browse button above its editor. **View → Stacked Editors** switches orientation while retaining the same editors and comparison. **Show Results** beside Previous/Next hides or restores the lower grid. Folder, Binary and Table modes keep both paths in a shared header; three-way review keeps its dedicated commands. The charcoal workspace, crimson command accents and horizontal difference overview follow the [VaporGrid adaptation](docs/VAPORGRID.md).

For an aligned block, **Compare**, then select a difference using the overview or **Previous/Next Difference**, and choose **Block → Right** or **Left ← Block**. A selected differing row identifies its whole contiguous nonmatching hunk; separated hunks remain unchanged. The buttons stay disabled for stale comparisons, active ignore options, positional fallback or no selected difference. Planning rejects output exceeding 2 million characters in the destination editor or the combined comparison budgets. A successful block operation changes only the destination editor, adds one undo snapshot and requires a new comparison. **Undo Merge** restores the exact editor snapshots. **Save As** remains explicit. Free-text selection merge and **All → Right** keep their existing behavior. Command bars wrap into additional rows at narrow widths without horizontal scrolling.

In **Folders** mode, **File Filter** accepts semicolon-separated globs such as `*.cs;*.txt;NOT *.g.cs`. Positive patterns are OR; case-insensitive exclusions win. With no positive pattern, all basenames are included before exclusions. Only `*` and `?` are wildcards, and `*.*` requires a literal dot. Patterns match file basenames at every depth. **Ignore Directories** takes exact names such as `.git;bin;obj`, pruned below each selected root; there are no default exclusions. Blank fields compare all files. Neither field accepts paths, control characters, `**`, brackets or braces; directory names also reject wildcards, `.` and `..`. Each field is limited to 4096 characters, 64 nonempty items, and 256 characters per operand/name.

Filtered results are labeled **Filtered Scope**, including exact normalized rules and counts of enumerated excluded files and directly pruned directories per root. Descendants of a pruned directory are not visited or counted individually. Zero matches says **No Files Match These Filters**; it does not assert that the folders are identical. HTML reports retain this scope even with zero rows. Session version 3 preserves filters, highlight granularity, synchronized scrolling, word wrap, and table separator. Older sessions use Line highlighting, synchronized scrolling enabled, wrap disabled, and comma separator; version 1 also has blank filters. CLI folder filters are not implemented.

Existing-file replacement retains a sibling `.diffbeacon-backup-<id>` file. Backups are not automatically deleted. Review paths before saving. Folder and table modes are currently read-only. Mixed-newline text saves are refused to avoid silent normalization. Unsupported text should be compared in Binary mode.

For three-way review, choose **File → New Three-Way Merge**, browse to Base, Local and Remote, then **Compare**. Select conflicts, accept a side or both, and use **Undo Choice** to revise a decision. **Save Merged As…** is enabled only after every conflict has a choice. Output encoding, BOM and newline convention follow Local. Any externally changed source requires a new comparison. Review actions share a full-width bottom command bar beneath the conflict list and merged preview; commands wrap at narrow widths. Three-way sessions and Git mergetool automation are not implemented yet.

A maximized titlebar press starts dragging only after the system drag threshold is crossed, then restores the saved normal size beneath the pointer. A single caption click keeps the window maximized; double-click toggles maximize/restore.

[Responsive text toolbar](docs/DiffBeacon-Responsive-Toolbar.png) · [Responsive three-way review](docs/DiffBeacon-Responsive-Three-Way.png)

## Limits

Detailed text comparison supports at most 4 million combined characters and 100,000 combined lines. A bounded alignment matrix uses positional fallback above 2 million cells. Each text file has a 16 MiB loading ceiling; larger files can be compared fully in Binary mode, without editing. Binary samples are limited, but equality/counting scans the entire input. Folder enumeration retains up to 50,000 paths per root and reports errors/skipped reparse points as incomplete. Grids show at most 10,000 rows. CSV parsing is capped at 200,000 cells per input, with first 10,000 compared cells retained. Editor decoration retains the first 20,000 changed spans and paints visible content first, adding offscreen highlights as you scroll or navigate. Native paint batches stop after 128 spans or an elapsed-time check of 16 ms; an individual native operation can exceed that interval, so this is not a latency guarantee. Word/Character detail uses bounded token alignment (16,384 tokens per pair, 250,000 cells per pair, 2 million total cells, 20,000 detailed spans); over-budget lines use explicit whole-line fallback. Counts and the grid cover retained comparisons.

Three-way inputs share a 4 million character / 100,000 logical line ceiling across all three sources. Merging rejects comparisons exceeding the exact alignment budget; it never uses a positional fallback to decide a merge. Insertions touching another side's changed/deleted range are conservatively reviewed as conflicts. Choice undo retains the last 100 decisions. No Git settings are changed.

Folder traversal also stops at 1,000,000 visited entries per root, including filtered entries, or 50,000 queued directories. Reached limits, in-scope reparse points and access/content errors produce explicit incomplete reasons; filtering itself is not an error. Excluded files are filtered before retention and content reads, and ignored directories are pruned before traversal.

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



In Text mode, **Highlight** selects Line (default), Word, or Character detail. **Sync scrolling** defaults on and maps fresh exact aligned lines in either direction, including insertions/deletions. Positional fallback and stale inputs scroll independently; compare again to restore mapping. Wrapped panes synchronize logical anchors plus a clamped wrapped-line offset; unwrapped panes also synchronize horizontal position within the destination range. Changing the sync checkbox does not reposition either pane. Highlight changes run asynchronously and discard obsolete results. Navigation keeps the chosen detail style.
# Reusable libraries

The alpha `net10.0` modules, dependency graph, examples, local package verification and compatibility notes are documented in [MODULES.md](docs/MODULES.md).

