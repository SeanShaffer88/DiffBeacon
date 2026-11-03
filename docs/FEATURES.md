# Feature Matrix

Status describes this core preview only. Implemented means present in this build; Partial gives a specific narrower implementation; Planned means absent. No advanced feature is implied by a similar control.

| Requested Capability | Status | Current Behavior / Remaining Work |
|---|---|---|
| Large Text Files | Partial | Bounded text editing; full-file streamed binary summary for larger inputs. No virtualized GB text editor. |
| Two-Way Text Compare | Implemented | Aligned lines, bounded positional fallback, explicit status. |
| Three-Way Text Compare / Conflicts | Planned | Base/local/remote merge engine and conflict review. |
| Inline Text Editing / Merging | Partial | Editable raw panes; selected-text replacement, all-left-to-right, bounded merge undo, explicit Save As. No semantic block/word merge. |
| Character / Word / Line Highlight | Partial | Line coloring and status grid; character and word comparison planned. |
| Ignore Case / Whitespace / Blank Lines | Implemented | Comparison normalization preserves raw editor text. |
| Ignore Terminators / Strings / Ranges / Start-End Lines | Partial | Logical line comparison ignores terminator differences. Advanced strings/ranges/line exclusions planned. |
| Resync / Logical Matching / Tab Values / Columns | Planned | Manual resync and column-range matching. |
| Snippet Compare | Implemented | Blank path frames accept pasted text. |
| Differences Map / Relational Lines / Active Line / Counts | Partial | Timeline scrubber navigates line differences; status counts and row grid. Relational lines and detailed overview map planned. |
| Unicode Paths / UTF Data | Implemented | Strict UTF-8; BOM UTF-8/16/32; encoding-preserving saves. Legacy encodings rejected. |
| Bookmarks / Wrap / Line Compare / EBCDIC / DBCS | Partial | Wrap and line comparison; bookmarks and legacy encodings planned. |
| Web / URL Compare | Planned | No network fetching. |
| Two / Three Folders Recursive | Partial | Two-folder recursive content comparison. Third root planned. |
| Threaded Folder Engine | Partial | Background cancellable scanning, sequential streamed file comparison. Parallel traversal planned. |
| Expandable Folder Trees | Planned | Current view is a results grid, not a tree. |
| Include / Exclude / NOT / Ignore Directories | Planned | No folder filters yet. Reparse points skipped and reported. |
| FTP / SFTP / FTPS | Planned | Transport and credential integrations. |
| Password ZIP / RAR / JAR | Planned | Archive content comparison. |
| Folder Sync / Preview / Rules / Sessions / Schedule / Explorer | Planned | Folder mode is read-only. No copy/delete/sync actions. Session metadata is independent. |
| Time / Date / Size Criteria | Partial | Sizes displayed; comparison currently always content. Custom criteria/date formats planned. |
| Duplicate Finder | Planned | Content grouping and keep/delete workflow. |
| Folder Difference Summary / Binary-ASCII Label | Partial | Missing/changed/error status and sizes; type labels planned. |
| Binary Fast / Smart Modes | Partial | Whole-file positional streaming. Smart alignment planned. |
| Binary Hex / ASCII / Offsets | Partial | Up to 512 differing-byte samples with hex and decimal offsets. Full paged hex grid planned. |
| Binary Matching / Differing Filters | Planned | Current samples show differing bytes only. |
| CSV / TSV Grid | Partial | Positional cells in row/column result grid, robust quoting; spreadsheet-style multi-column panes planned. |
| Delimiter / Header Parsing | Partial | Custom one-character delimiter or literal `\t`; header semantics planned. |
| XLS / XLSX / Multi-Sheet | Planned | No Excel or COM integration. |
| Table Ignore / Filter / Merge / Save | Planned | Tables currently read-only. |
| DOC / DOCX / PDF / RTF | Planned | No document extraction path; not treated as plain text. |
| Multi-Mode / Folder / Table Merge | Partial | Text selection/all merging only. |
| Undo Merge / Review / Accept All | Partial | Up to 20 text-merge snapshots, all-left-to-right; review marks planned. |
| Git Status / Context Menu | Planned | No repository scanning or changes. |
| Git Difftool / Mergetool | Partial | CLI launch and documented opt-in difftool. Mergetool planned. |
| Git Conflicts / Version Diff / Blame / Dedicated Windows | Planned | Full Git workflow UI. |
| Themes / Skinning / Import-Export | Planned | Native light theme with teal/amber accents. |
| Syntax Highlight / Wordfiles | Planned | Content remains raw text. |
| Dockable Legend / Filters / Folder Preview | Planned | No docking framework. |
| Synchronized Scroll / Flip Frames / Ribbon-Classic Choice | Partial | Swap sides; synchronized scrolling and ribbon choice planned. |
| HTML Results | Implemented | Escaped retained rows, paths, summary and limits; no external scripts/assets. |
| Color Printing / Headers / Footers / Output Pane | Planned | No print integration. |
| Explorer Context Menu / Editor Integration | Partial | Path-based command-line launch; shell extension/external editor configuration planned. |
| Sessions / Workspace Manager | Partial | Named versioned JSON path/options sessions, no content storage; manager browser planned. |
| CLI / Automation | Partial | GUI launches and `--check` exit codes; scheduler/CLI reports planned. |
| Google Drive / OneDrive / S3 / Dropbox | Planned | No cloud connections or credentials. |
| Pieces AI Analysis | Planned | No AI calls or telemetry. |
| Visual Studio 2022 Plugin | Planned | No IDE extension. |
| Drag And Drop | Partial | Files/folders onto path fields; frame drops planned. |
| Tabbed Sessions | Implemented | Independent comparison tabs, close and unsaved-change prompts. |
| Installer | Planned | Framework-dependent portable ZIP only. |

Advanced integrations require their own architecture, dependency authorization where needed, and acceptance testing. See SPEC.md for safety and verification contracts.
