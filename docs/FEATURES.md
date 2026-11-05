# Feature Matrix

The product target is a complete local comparison and merge workflow replacement with zero mandatory paid software, subscriptions or metered APIs. This v0.2.0-beta.1 public beta remains incomplete; stable and full replacement readiness are outstanding. Status describes this build only. Implemented means present in this build; Partial gives a specific narrower implementation; Planned means absent; Deferred means outside current implementation pending an eligible zero-cost or local approach. No advanced feature is implied by a similar control.

| Requested Capability | Status | Current Behavior / Remaining Work |
|---|---|---|
| Large Text Files | Partial | Bounded text editing; full-file streamed binary summary for larger inputs. No virtualized GB text editor. |
| Two-Way Text Compare | Implemented | Aligned lines, bounded positional fallback, explicit status. |
| Three-Way Text Compare / Conflicts | Partial | Dedicated Base/Local/Remote tab, read-only sources/preview, strict line-hunk merge, explicit conflict choices and safe Save As. Bounded alignment; no three-way editing/sessions/Git mergetool. |
| Inline Text Editing / Merging | Partial | Editable raw panes; free-text selection replacement, strict aligned contiguous-block merge both directions, all-left-to-right, bounded undo and explicit Save As. Block merge requires selected fresh exact alignment with ignore options off; word-level merge planned. |
| Character / Word / Line Highlight | Partial | Line, Word and Character highlight controls with raw Unicode offsets, bounded detail and explicit whole-line fallback. Recorded native smoke verifies decoration preservation and mapped scrolling. Manual gestures, DPI/high-contrast and full accessibility acceptance remain pending. WordWrap toggles can lose typing undo history. |
| Ignore Case / Whitespace / Blank Lines | Implemented | Comparison normalization preserves raw editor text. |
| Ignore Terminators / Strings / Ranges / Start-End Lines | Partial | Logical line comparison ignores terminator differences. Advanced strings/ranges/line exclusions planned. |
| Resync / Logical Matching / Tab Values / Columns | Planned | Manual resync and column-range matching. |
| Snippet Compare | Implemented | Blank path frames accept pasted text. |
| Differences Map / Relational Lines / Active Line / Counts | Partial | Proportional overview markers follow aligned row positions; mouse and keyboard navigation, selected status, counts and row grid. Relational line mode planned. |
| Unicode Paths / UTF Data | Implemented | Strict UTF-8; BOM UTF-8/16/32; encoding-preserving saves. Legacy encodings rejected. |
| Bookmarks / Wrap / Line Compare / EBCDIC / DBCS | Partial | Wrap and line comparison; bookmarks and legacy encodings planned. |
| Web / URL Compare | Planned | No network fetching. |
| Two / Three Folders Recursive | Partial | Two-folder recursive content comparison. Third root planned. |
| Threaded Folder Engine | Partial | Background cancellable scanning, sequential streamed file comparison. Parallel traversal planned. |
| Expandable Folder Trees | Planned | Current view is a results grid, not a tree. |
| Include / Exclude / NOT / Ignore Directories | Implemented | Case-insensitive basename `*`/`?` globs; semicolon OR positives, `NOT` exclusions win; exact ignored directory names prune below roots. Explicit scoped summaries/counts and zero-match wording; no paths/recursive-glob syntax. |
| FTP / SFTP / FTPS | Planned | Transport and credential integrations. |
| Password ZIP / RAR / JAR | Planned | Free local archive parsing/content comparison; no mandatory paid software or codec. |
| Folder Sync / Preview / Rules / Sessions / Schedule / Explorer | Planned | Folder mode is read-only. No copy/delete/sync actions. Session metadata is independent. |
| Time / Date / Size Criteria | Partial | Sizes displayed; comparison currently always content. Custom criteria/date formats planned. |
| Duplicate Finder | Planned | Content grouping and keep/delete workflow. |
| Folder Difference Summary / Binary-ASCII Label | Partial | Missing/changed/error status and sizes; type labels planned. |
| Binary Fast / Smart Modes | Partial | Whole-file positional streaming. Smart alignment planned. |
| Binary Hex / ASCII / Offsets | Partial | Up to 512 differing-byte samples with hex and decimal offsets. Full paged hex grid planned. |
| Binary Matching / Differing Filters | Planned | Current samples show differing bytes only. |
| CSV / TSV Grid | Partial | Positional cells in row/column result grid, robust quoting; spreadsheet-style multi-column panes planned. |
| Delimiter / Header Parsing | Partial | Custom one-character delimiter or literal `\t`; header semantics planned. |
| XLS / XLSX / Multi-Sheet | Planned | Free local multi-sheet parsing and comparison; installed Office cannot be required. |
| Table Ignore / Filter / Merge / Save | Planned | Tables currently read-only. |
| DOC / DOCX / PDF / RTF | Planned | Free local extraction required; currently no document parser. No mandatory Office, paid codec or paid library. |
| Multi-Mode / Folder / Table Merge | Partial | Text selection/block/all merge and bounded three-way conflict choices; folder and table merge remain planned. |
| Undo Merge / Review / Accept All | Partial | Up to 20 two-way merge snapshots; three-way conflict list with Local/Remote/Both and last 100 choice undo. Review marks planned. |
| Git Status / Context Menu | Planned | No repository scanning or changes. |
| Git Difftool / Mergetool | Partial | CLI launch and documented opt-in difftool. Mergetool planned. |
| Git Conflicts / Version Diff / Blame / Dedicated Windows | Planned | Full Git workflow UI. |
| Themes / Skinning / Import-Export | Partial | Near-black charcoal workspace, burgundy accents, custom dark titlebar and dark prompts. Theme import/export and user-created skins planned. |
| Syntax Highlight / Wordfiles | Planned | Content remains raw text. |
| Dockable Legend / Filters / Folder Preview | Planned | No docking framework. |
| Synchronized Scroll / Flip Frames / Ribbon-Classic Choice | Partial | Swap sides and optional bidirectional synchronized scrolling for fresh exact alignment are implemented and covered by recorded native smoke. Stale/positional comparisons scroll independently. Ribbon/classic choice remains planned. |
| HTML Results | Implemented | Escaped retained rows, paths, summary and limits; no external scripts/assets. |
| Color Printing / Headers / Footers / Output Pane | Planned | No print integration. |
| Explorer Context Menu / Editor Integration | Partial | Path-based command-line launch; shell extension/external editor configuration planned. |
| Sessions / Workspace Manager | Partial | Version 3 JSON path/options sessions preserve folder filters, highlight, sync, wrap and separator. Versions 1/2 migrate with default preferences; version 1 has blank filters. No content storage; manager browser planned. |
| CLI / Automation | Partial | GUI launches and `--check` exit codes; scheduler/CLI reports planned. |
| Cloud Connections | Deferred | No connections or credentials. Only verified zero-cost paths qualify; free tiers alone do not establish no cost. Billed/metered/paid-only integrations excluded. |
| Local / Zero-Cost AI Analysis | Deferred | No AI calls or telemetry. Local approaches or verified zero-cost paths only; billed/metered/paid-only integrations excluded. |
| Visual Studio 2022 Plugin | Planned | No IDE extension. |
| Drag And Drop | Partial | Files/folders onto path fields; frame drops planned. |
| Tabbed Sessions | Implemented | Independent comparison tabs, close and unsaved-change prompts. |
| Installer | Planned | Framework-dependent portable ZIP only. |

Advanced integrations require their own architecture, eligible free local dependencies, and acceptance testing. See [SPEC.md](SPEC.md) for safety and verification contracts.

## Ordered Free Local Milestones

Scoped dependencies may be added under project policy; verify sources, versions and free local operation without mandatory paid services. Each milestone remains incomplete until its real workflow acceptance gate passes; no delivery timing is claimed.

| Order | Milestone | Acceptance Gate |
|---|---|---|
| 1 | Complete daily text workflow: character/word granularity, find, resync, synchronized scrolling and three-way editing | Exercise open, compare, navigate, edit, resolve, undo and save with representative fixtures; preserve encoding/newlines and reject stale or unresolved output. |
| 2 | Git Base/Local/Remote/output contract, three-way sessions and CLI | Test real Git conflict invocation, cancellation/unresolved exit behavior, explicit output safety and session round trips; no automatic global Git changes. |
| 3 | Folder trees, safe reviewed synchronization and duplicates | Test recursive scope, stale-plan refusal, root confinement, reviewed writes/backups/recovery and byte-verified duplicate groups; no automatic deletion. |
| 4 | Paged binary browsing, smart alignment and table merge | Test bounds, offsets, cancellation, large fixtures, explicit incomplete states and table round-trip preservation. |
| 5 | Free local document, spreadsheet and archive parsers; remaining integrations | Test supported formats, malformed/encrypted inputs, limits and actionable unsupported cases. No mandatory installed Office, paid codec/library/software or paid service. Verify dependency sources, versions and free local operation before implementation. |

## Full Replacement Readiness

Run real daily workflows across all required modes and integrations. Comparisons and saves must never silently succeed with incomplete data; file safety and recovery must pass failure tests. Complete accessible keyboard operation, DPI/display layout and representative large-file checks, then independently review the final implementation and evidence. Current core/native smoke passes are bounded evidence, not full replacement acceptance. Optional cloud/AI work remains Deferred unless the zero-cost policy is verified; it cannot introduce a mandatory paid path.

Granular slice inventory: Text now has Line/Word/Character detail and optional bidirectional synchronized scrolling for fresh exact alignment. Existing text edit/save/block merge, folders, binary, tables and three-way workflows retain their scope. Remote transports, virtualized multi-GB editing, installers, signing/updaters and roadmap integrations remain absent. This slice adds no packages or network calls.

Audit corrections implemented: decoration preserves native state through TOM suspension; invalidation clears stale color/detail/maps; free-text merge prevalidates output before undo/mutation; zero-sample completed comparisons remain report-ready; session v3 retains separator/preferences; first Previous starts at the last difference. Recorded native smoke passes preservation and scrolling checks. Manual gestures, physical DPI/text scaling, live high contrast and full accessibility acceptance remain outstanding. Toggling WordWrap between typing and Undo can lose typing undo history. See [verification evidence](VERIFICATION.md).
