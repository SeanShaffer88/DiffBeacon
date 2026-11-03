# DiffBeacon Operational Specification

Status: first implementation specification, 2026-11-03. The user selected Core Comparison And Safe Merging for the first build (controller confirmation). This is a core milestone toward the supplied feature list, not a claim of feature parity.

## Verified Constraints

- Existing repository: MIT license and README only; clean working tree and no Git lock observed.
- Windows x64, .NET SDK 10.0.403, Microsoft.WindowsDesktop.App.Ref installed. Use `net10.0-windows`, WinForms, and the .NET standard library. No NuGet packages, external wrappers, or downloaded runtimes.
- Use Proper Case UI labels. Preserve the license and unrelated work. No telemetry, AI requests, or automatic network access.
- Produce and inspect a UI mockup before finalizing the layout. Five distinct icon concepts are separate deliverables; no selection is implied.
- No tag or release. Portable build artifacts are permitted. Full installer and integrations remain separate acceptance gates.

## Structure And Boundaries

- `src/DiffBeacon.Core`: platform-neutral `net10.0` library for comparison, parsing, reports, sessions, and safe file operations.
- `src/DiffBeacon.App`: `net10.0-windows` WinExe/WinForms shell and command dispatch. UI work stays on the UI thread; comparison/file IO runs asynchronously with cancellation.
- `tests/DiffBeacon.Tests`: dependency-free console test harness referencing Core, returning nonzero on failure. Never simulate test results.
- `docs/FEATURES.md`: every requested feature mapped to Implemented, Partial, or Planned, with precise limits. README advertises only verified behavior.
- `docs/CLI.md`: actual supported invocation, Git difftool/mergetool configuration, exit codes, and examples.
- `scripts`: reproducible build/test/publish scripts. Windows CI must execute the same build and core tests.

Use public immutable result records (or equivalent immutable objects); engines must not depend on WinForms, modify input files during comparison, or expose unbounded collections for huge inputs. The implementer may select internal algorithm details while preserving these behavioral contracts.

## Shared Contracts

- `ComparisonOptions`: IgnoreCase, IgnoreWhitespace, IgnoreBlankLines; ordinal comparison; configurable input memory ceiling (default 32 MiB combined), maximum line/cell/result counts, cancellation token. Additional advanced filters may be implemented only with dedicated validation.
- Results distinguish Equal, Different, LeftOnly, RightOnly, Conflict, and Error/Incomplete. An unreadable or cancelled input must never be reported as Equal.
- Every result carries full input paths, comparison criteria, counts, warnings/limits, and completion state. User-visible summaries must say when results are truncated.
- Input limits apply before allocation and again during streaming; checked sizes and long offsets avoid overflow. Cancellation checked regularly. UI owns disposable tokens and suppresses stale task results after a new comparison or closed tab.
- Files changed during a comparison produce a warning or failed/retry state; results are not represented as a reliable snapshot.

## Text Engine

Inputs are two Unicode file paths or two editable snippets; optional base text enables three-way review. Return aligned diff rows with source line numbers, raw content, and status. Use an edit alignment algorithm with explicit work/line limits; never allocate an unbounded N-by-M matrix. If detailed alignment exceeds its budget, give a clearly labeled positional/streaming fallback, not fabricated block alignment.

Required core behavior: insert/delete/change alignment, line coloring, next/previous difference, ignore case/whitespace/blank lines, editable panes, word wrap, counts, swap sides, two-way line/block or selected-text merge, Accept All, undo, explicit Save As. Preserve original raw lines when using ignore filters. Comparison normalization must not rewrite saved source content.

Decode BOM-marked UTF-8/UTF-16/UTF-32 and strict UTF-8 by default. Reject unsupported or invalid text with a clear choice to use Binary Compare. Preserve encoding/BOM/newline on existing-file save where determinable. Never silently transcode legacy or mixed-newline files. Display Unicode paths intact.

Three-way review compares base/local/remote: unchanged base yields to the changed side; identical changes resolve; competing changes remain visibly unresolved until a user selects Local, Remote, or Both. Saving a merge with unresolved conflicts requires explicit warning and cannot return a successful Git mergetool resolution. If this is not implemented and tested, document it as Planned rather than representing three editable panes as a merge engine.

For inputs above the editing budget, use bounded-buffer streaming equality/difference summary (several GB permitted), label it Large File Summary, and disable editing/merge/detailed alignment. This is partial large-file text support, not a claim of virtualized multi-GB editing.

## Folder Engine

Two roots, recursive enumeration, relative path matching with Windows case-insensitive ordinal semantics, and cancellable background work. Skip reparse points by default to avoid loops and escape from roots. Report inaccessible paths individually and mark the overall result incomplete.

Compare by content (streamed), size, or timestamp/size; selected criteria must be visible. Entries carry relative path, per-side size/time, status, and error. Include/exclude glob filters and ignored directory names are desirable; document exact syntax and NOT support only if implemented. Cap retained rows and report that cap; do not quietly omit differences. Use bounded concurrency if parallel content checks are implemented.

UI: results grid with sortable relative path/status/metadata, open selected pair in Text or Binary, and a clear root pair. A grid is partial fulfillment of the requested expandable tree view; do not call it a tree.

Synchronization is separate from comparison. Build an explicit copy-only Left-To-Right or Right-To-Left plan, show exact paths and overwrite actions, and require review before Apply. No automatic deletion. At apply time revalidate source metadata and destination confinement; reject reparse-point ancestors and nested/identical roots; never overwrite a source. Stage copies then atomically replace, retaining backups. Report per-file failures. An undo record must restore overwritten files and remove only newly created files still matching the written snapshot. If undo is absent, advertise previewed copy rather than reversible full synchronization.

Duplicate discovery, if provided: group by length then streamed SHA-256 and verify bytes before calling items duplicates; ignore empty groups as appropriate and expose errors. No automatic duplicate deletion.

## Binary Engine

Compare entire streams using fixed-size buffers and long offsets; return total differing-byte count, lengths, first difference, and bounded sample rows. Hex/ASCII rows show offsets, explicit missing-byte positions, and non-printable replacement characters. Samples must be labeled samples. A paged UI may read arbitrary windows on demand.

Required positional mode supports differing lengths and binary zeros. Smart offset alignment is Planned until separately implemented and tested. Decimal offsets and match/difference filtering may be included with accurate limits.

## Table Engine

Parse CSV/TSV with configurable delimiter, optional header row, quoted delimiters/newlines, doubled quotes, empty fields, Unicode, and ragged rows. Reject unclosed quoted fields with line/column context. Enforce file/row/cell limits before rendering. Compare by row and column position; visible grid colors changed/missing cells. Header handling must not hide duplicate header names. Ignore-column support and cell/row merge are optional only if round-trip saves preserve quoting and the delimiter and are tested.

XLS/XLSX are Planned unless a real multi-sheet parser or optional installed-Excel COM path is implemented and tested. Never parse ZIP/XML bytes or rename a CSV to claim Excel support.

## File Safety, Sessions, Reports, CLI

- All write operations are explicit. Save As uses a sibling temporary file, flush/dispose, and atomic move/replace, with failure preserving the existing destination. Warn before overwriting an input changed externally. Undo history is bounded.
- Persist named session metadata as versioned JSON under the user's local application-data DiffBeacon directory. Save paths/options, not pasted text, credentials, or file contents. Validate deserialized fields and tolerate corrupt session files with a recoverable error.
- Export escaped HTML of actual visible/retained results, with criteria, limits, and summary. Encode all paths and file content to prevent active HTML injection. No external script/font/network dependencies. Export is not printing support.
- CLI: `--help`; GUI launching with two paths; `--mode text|folder|binary|table` when supported; `--check LEFT RIGHT` for full streamed equality; optional `--report PATH`. Deterministic exit codes: 0 equal/success, 1 different, 2 invalid input/IO/error. A GUI dismissal must not indicate successful unresolved Git merge.
- Document Git difftool configuration against real executable arguments. Mergetool configuration only if base/local/remote/output contract, unresolved status, and exit behavior are implemented. Do not change global Git config automatically.
- Tabs keep independent state; drag/drop accepts actual file-system paths only. Errors appear in the affected session; workers must not crash the UI or write to closed controls.

## UI Direction

Native Windows application with classic menu/toolbar, mode navigation (Text, Folders, Binary, Tables), tabbed workspace, left/right path controls and Browse, comparison options, two comparison panes, and status/count footer. Use a professional light theme, teal accents, amber changed rows, Segoe UI for chrome, Consolas for compared content, readable selection/focus states, keyboard-accessible controls, resize support, and explicit labels beyond color. Custom drawing must not impair high-contrast readability. Full skinning/docking/ribbon choices are not required for the core milestone. Provide an actual-app `--smoke` screenshot and a clearly labeled prototype UI mockup using the same design; parent handles the five generated icon concepts.

## Verification Gates

1. Real Release build succeeds on verified SDK; no unexpected package dependencies.
2. Core harness covers text insert/delete/change and ignores without raw-content loss; strict Unicode/BOM/invalid input; algorithm-budget fallback; optional three-way equal/one-sided/conflict cases.
3. Binary tests cover empty, unequal-length, early/late mismatch, long-offset streaming behavior, cancellation and bounded samples. Do not claim multi-GB verification unless an actual large/sparse fixture is tested.
4. Folder tests cover recursion, exclusions if supported, missing files, inaccessible/error state, cancellation, reparse avoidance, and sync root confinement/stale-plan refusal if writes exist.
5. Table tests cover quotes, multiline fields, escaped quotes, trailing empty fields, malformed input and round-trip saves if supported.
6. File-write tests prove failure preserves destination; reports escape hostile markup; corrupt sessions recover; CLI returns specified exit codes.
7. UI smoke: start native application, compare changed text, edit/merge/undo/save, switch modes, resize, cancel comparison, open/close tabs, and close normally. Capture an actual screenshot distinct from the design mockup.
8. Independent security/quality audit passes on final diff and test evidence; fix exact findings and rerun affected gates.
9. Publish portable local artifact and instructions, push feature branch, create PR, inspect CI, merge only passing PR. No tags/releases.

## Remaining Product Scope

Track each item explicitly in FEATURES.md. Advanced requested capabilities are not implied by core availability: full virtualized multi-GB text diff/edit; granular character/word diff; all ignore/filter/resync/column controls; relational map/bookmarks/syntax definitions; EBCDIC/DBCS; URL download; three-folder view; expandable folder trees; remote FTP/SFTP/FTPS; password ZIP/RAR/JAR browsing; scheduler/Explorer integration; full reversible/custom folder sync; smart binary alignment; Excel multi-sheet compare/merge; DOC/DOCX/PDF/RTF extraction; full three-way and word merge; Git overlays/repositories/branches/blame/version browser; imported themes/dockable panes/ribbon; color printing/report layout; cloud connectors; Pieces analysis; Visual Studio extension; installer.

No user clarification blocks the native local core. Decisions requiring user input before dependent work: approval for third-party format/transport libraries; desired cloud/AI account providers and credential handling; whether Office-installed-only format support is acceptable; final icon selection. Full parity needs staged development and integration acceptance, not a feature checklist marked complete after a prototype.
