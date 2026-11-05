# DiffBeacon Operational Specification

Status: alpha / core preview. The current product target is a complete local comparison and merge workflow replacement. The first milestone was Core Comparison And Safe Merging; its narrower boundaries below remain historical milestone contracts, not a reduction of the full product target. Current completion is tracked in FEATURES.md; full replacement readiness is not established.

## Product Completion And Free-Only Policy

All required workflows must operate with zero mandatory paid software, subscriptions or metered APIs. Document, spreadsheet and archive support stays Planned until free local implementations are implemented and tested. Installed Office, paid codecs and paid libraries cannot be prerequisites. Third-party dependencies require prior user approval. Exclude billed, metered and paid-only integrations. Cloud or AI work is Deferred unless a verified zero-cost path or local approach satisfies the same policy; a free tier does not guarantee no cost.

Complete the ordered milestones and acceptance gates in FEATURES.md. Readiness requires real daily workflow tests, explicit incomplete comparison/save outcomes, file safety, keyboard accessibility, DPI and large-file checks. Passing the current core tests alone does not establish completion.

## Verified Constraints

- Historical initial intake: repository contained an MIT license and README only; the tree was clean with no Git lock observed at that intake. Preserve subsequent unrelated work.
- Windows x64, .NET SDK 10.0.403, Microsoft.WindowsDesktop.App.Ref installed. Use `net10.0-windows`, WinForms, and the .NET standard library. No NuGet packages, external wrappers, or downloaded runtimes.
- Use Proper Case UI labels. Preserve the license and unrelated work. No telemetry, AI requests, or automatic network access.
- Produce and inspect a UI mockup before finalizing the layout. Five distinct icon concepts are separate deliverables; no selection is implied.
- No tag or release. Portable build artifacts are permitted. Full installer and integrations remain separate acceptance gates.

## Structure And Boundaries

- `src/DiffBeacon.Core`: platform-neutral `net10.0` library for comparison, parsing, reports, sessions, and safe file operations.
- `src/DiffBeacon.App`: `net10.0-windows` WinExe/WinForms shell and command dispatch. UI work stays on the UI thread; comparison/file IO runs asynchronously with cancellation.
- `tests/DiffBeacon.Tests`: dependency-free console test harness referencing Core, returning nonzero on failure. Never simulate test results.
- `docs/FEATURES.md`: every requested feature mapped to Implemented, Partial, Planned, or Deferred, with precise limits. README advertises only verified behavior.
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

XLS/XLSX remain Planned until a free local multi-sheet parser is implemented and tested, with prior approval for any third-party dependency. Installed Excel/Office is not a prerequisite. Reading container bytes alone or renaming CSV does not establish spreadsheet support.

## File Safety, Sessions, Reports, CLI

- All write operations are explicit. Save As uses a sibling temporary file, flush/dispose, and atomic move/replace, with failure preserving the existing destination. Warn before overwriting an input changed externally. Undo history is bounded.
- Persist named session metadata as versioned JSON under the user's local application-data DiffBeacon directory. Save paths/options, not pasted text, credentials, or file contents. Validate deserialized fields and tolerate corrupt session files with a recoverable error.
- Export escaped HTML of actual visible/retained results, with criteria, limits, and summary. Encode all paths and file content to prevent active HTML injection. No external script/font/network dependencies. Export is not printing support.
- CLI: `--help`; GUI launching with two paths; `--mode text|folder|binary|table` when supported; `--check LEFT RIGHT` for full streamed equality; optional `--report PATH`. Deterministic exit codes: 0 equal/success, 1 different, 2 invalid input/IO/error. A GUI dismissal must not indicate successful unresolved Git merge.
- Document Git difftool configuration against real executable arguments. Mergetool configuration only if base/local/remote/output contract, unresolved status, and exit behavior are implemented. Do not change global Git config automatically.
- Tabs keep independent state; drag/drop accepts actual file-system paths only. Errors appear in the affected session; workers must not crash the UI or write to closed controls.

## UI Direction

Native Windows application with classic menu/toolbar, mode navigation (Text, Folders, Binary, Tables), tabbed workspace, left/right path controls and Browse, comparison options, two comparison panes, and status/count footer. Use the existing near-black/charcoal workspace, burgundy accents, custom dark titlebar, vertically stacked source panes, Segoe UI for chrome, Consolas for compared content, readable selection/focus states, keyboard-accessible controls, resize support, and explicit labels beyond color. Custom drawing must not impair high-contrast readability. Full skinning/docking/ribbon choices are not required for the core milestone. Provide an actual-app `--smoke` screenshot and a clearly labeled prototype UI mockup using the same design; parent handles the five generated icon concepts.

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

Track each item explicitly in FEATURES.md. Advanced requested capabilities are not implied by core availability: full virtualized multi-GB text diff/edit; all ignore/filter/resync/column controls; relational map/bookmarks/syntax definitions; EBCDIC/DBCS; URL download; three-folder view; expandable folder trees; remote FTP/SFTP/FTPS; password ZIP/RAR/JAR browsing; scheduler/Explorer integration; full reversible/custom folder sync; smart binary alignment; Excel multi-sheet compare/merge; DOC/DOCX/PDF/RTF extraction; full three-way and word merge; Git overlays/repositories/branches/blame/version browser; imported themes/dockable panes/ribbon; color printing/report layout; optional verified zero-cost cloud connectors or local AI analysis; Visual Studio extension; installer.

No user clarification blocks the native local core. Dependent third-party format/transport libraries require prior user approval. Any proposed cloud/AI credential handling requires an eligible verified zero-cost path first. The lighthouse icon is selected. Full completion requires staged development and integration acceptance; the current preview does not establish it.

## Approved Folder Filter Slice

`FolderFilters.Parse(fileFilterText, ignoredDirectoriesText)` creates an immutable BCL-only snapshot before traversal. Blank fields preserve the original all-files behavior. Semicolon-separated nonempty file clauses have OR-positive basename globs, and case-insensitive ordinal `NOT` followed by whitespace excludes. Exclusions win; no positive clauses implies `*`. Only `*` and `?` wildcard syntax is accepted; `*.*` requires a dot. Basenames match at all depths, ignoring case. Reject paths/separators/colon, brackets/braces, `**`, controls/NUL and bare/missing-operand NOT. Each field allows at most 4096 characters and 64 items; each operand allows at most 256 characters. Ignore directories are semicolon-separated exact names, ignoring case; reject wildcards, paths, controls, `.` and `..`. No default excludes and never exclude the selected root itself.

Prune ignored directories before traversing them and filter files before retention/content reads using the same immutable scope on both sides. Count excluded enumerated files and directly pruned directories separately per root. Count all enumerated entries, including exclusions, toward a 1,000,000-entry per-root ceiling. Keep existing retained-file/result and queued-directory bounds. In-scope access/content errors and reparse points remain incomplete; out-of-scope exclusions do not become errors or equal rows. Report explicit incomplete reasons. Filtered summaries/HTML contain normalized rules and counts; zero rows say No Files Match These Filters without claiming whole-folder equality.

Folder-only labeled filter fields and tooltips appear below path/options controls. Scope edits cancel/invalidate pending results and reports; text-only controls are hidden/disabled in folder mode. Mode switches retain fields. Compare validates a snapshot before starting worker traversal. Session v2 stores optional filter texts, validates them, and restores scope; v1 reads with blank defaults and cannot silently store nonblank scope. There are no CLI filter arguments. Folder comparison remains read-only.

## Approved Two-Way Aligned Block Merge Slice

`AlignedBlockMerge.Plan` receives immutable editor snapshots, a fresh strict TextResult, comparison options, the selected difference ordinal and direction. It verifies all ignore options are off, rejects positional fallback/error/stale rows, and checks the supplied result against a fresh raw line alignment. The ordinal counts non-Equal rows; map it into the full aligned Rows before expanding to the maximal contiguous non-Equal hunk. Plan exposes zero-based source/destination logical-line start/count, aligned-row range and proposed output. Replace only the destination span with source raw logical lines; an absent source span deletes, and a missing destination span inserts after the prior aligned destination line. Preserve all empty/trailing tokens using TextComparison.Lines and LF joining. Reject invalid/no-op selections, combined input/output character/line limits and cancellation before proposing a mutation. `Apply` checks both current editor snapshots ordinally against the plan before returning output; planning/apply perform no file IO.

The wrapping command toolbar includes Block → Right and Left ← Block with accessible guidance/tooltips. Enable only for a fresh exact Text comparison and selected difference; source/option/path/mode edits disable actions. Planning also enforces the actual destination RichTextBox limit of TextComparison.MaxCharacters / 2 (2 million characters), before output allocation, in addition to combined budgets. Plan/apply completes before adding exactly one existing 20-item undo snapshot. Change only the destination editor, invalidate comparison/report/grid/map/selection, then show Block Merged Into Right/Left Editor · Review And Save As To Write A File. Rejections do not mutate editors or undo. Free-text selection/all merge behavior, explicit saving, three-way review and other modes remain unchanged. UI mockup precedes toolbar finalization; native smoke captures the actual block toolbar separately.


## Responsive Commands And Caption Input

Command bars wrap with automatic height and no horizontal scrolling. Three-way conflict and merged-preview panes share one bottom review command bar across the full lower workspace, including a 200px conflict pane. Preserve feasible user splitter distances while retaining source/review minimum row budgets at narrow widths and 620px height.

A maximized caption press captures until the centered system drag threshold is crossed. Restore the saved normal size with signed screen coordinates and pointer anchoring, clear capture state before release/reentrancy, then queue native caption movement only for a held physical left button. Cancel pending capture on release, capture loss, cancel mode, deactivation, state changes and disposal. Single-click preserves maximization; double-click toggles it. See VERIFICATION.md for runtime evidence and remaining timing/display acceptance gaps.


## Granular text and viewport contract

Retain WinForms/RichEdit, custom charcoal/burgundy chrome and stacked panes. The original controls mockup is artifacts/Granular-Controls-Mockup.svg. Text-only Highlight Line/Word/Character defaults to Line; Sync scrolling defaults true. Session v3 appends granularity, sync, wrap and separator to existing scope metadata; v1/v2 retain backward defaults and must reject nondefault new fields when writing.

Core produces immutable per-row raw UTF-16 spans and monotonic bidirectional logical-line maps. App detail uses immutable snapshots, cancellation and a generation guard; only the current result/granularity is retained. Rendering yields every 256 spans and caps visible decoration at 20,000 spans with a status limit. Native formatting suspends and resumes TOM undo, restores selection, modified state and display-line/horizontal viewport, and never moves focus. Cached logical offsets come from actual editor text; wrapped display lines are used only for viewport movement. Edits/options/paths clear decorations and invalidate reports/maps.

Fresh exact maps synchronize vertical anchors with reciprocal suppression. Wrap preserves a clamped within-logical-line display offset; unwrapped horizontal pixels clamp independently to destination extent. Scroll paths include native wheel, scrollbar and keyboard messages. Vertical movement uses EM_GETFIRSTVISIBLELINE/EM_EXLINEFROMCHAR/EM_LINEINDEX and EM_LINESCROLL with wParam zero, avoiding the 16-bit EM_GETSCROLLPOS Y limitation. Navigation changes caret/viewport without repainting granular spans.

Selection/all merge validates TextEditProposal before undo push or mutation. First Previous selects the last difference. Report readiness is explicit completed-result state, independent of sample/row count, so equal binary and empty table comparisons can export.

Native definitions were checked against installed Windows SDK 10.0.26100.0 um/TOM.h (Undo slot 22, tomSuspend=-9999995, tomResume=-9999994) and Microsoft documentation: https://learn.microsoft.com/en-us/windows/win32/api/tom/nf-tom-itextdocument-undo and https://learn.microsoft.com/en-us/windows/win32/controls/em-getscrollpos. Do not substitute Undo(0)/Undo(-1). Actual undo/viewport preservation requires native smoke; SDK/header inspection alone is insufficient.
