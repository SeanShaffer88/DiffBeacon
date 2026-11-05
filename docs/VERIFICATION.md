# Local Verification

## Grouped commands and editor layout — 2026-11-05

Current layout verification supersedes the older layout screenshots below. The approved native implementation defaults to side-by-side editors, offers a stacked toggle, moves the active comparison's grouped Home/Merge/View deck above session tabs, adds pane path headers and two independent startup targets, and retains all existing comparison modes.

- Release build: process/runner exit 0, zero warnings/errors. `C:/Users/Sean Shaffer/Documents/DevTools/runs/20261105T065315782Z-c32d0ab4f20f412e9bff0677de51f89d`.
- Full native `--smoke`: process/runner exit 0; empty stderr. `C:/Users/Sean Shaffer/Documents/DevTools/runs/20261105T065318167Z-f663f2dcd68a4901808fc783f7977c85`.
- App DLL SHA256: `4C568EDCEA0C19AF48C36563CEDF70C06279238EC64C8DAAC642EDF57AC0D166`; `src/DiffBeacon.App/bin/Release/net10.0-windows/DiffBeacon.dll`, 264704 bytes. SDK 10.0.403, Windows x64.

New native assertions exercise actual hosted Swap clicks after repeated session switches, active/inactive session disposal, three-way deck isolation, default orientation, repeated orientation changes with wrapped/unwrapped text, exact typing Undo/Redo and merge Undo, content/selection/dirty-state/map/selected-difference preservation, logical viewport anchors, results collapse/restore and cancellation while switching layout. They inspect every command page through ancestor bounds, all comparison modes and 900/1140/1260×620 dimensions. Typing fixtures are established after setting Word Wrap: these assertions certify orientation switching, not undo retention across the existing native WordWrap property change. Existing native decoration, lazy navigation/scroll/wrap, stale highlighted-edit Undo/Redo, generation, horizontal-scroll, prompt and caption checks also pass.

The first live pass exposed an unattached initial command deck. Subsequent assertions caught zero-width group content and auto-resize SplitterMoved notifications overwriting saved user proportions. Fixes attach the initial deck explicitly, size native groups from their controls and update proportions only from user splitter movement. Home's Compare/Session/Options groups fit the minimum width in two control rows for Text; native bounds assertions remain enabled for all modes.

The coordinator's regular startup check subsequently found a 276px command deck despite content ending at 142px. Earlier smoke had visited every tab before captures, inadvertently correcting hidden-page layout measurements. A new assertion and `.startup.png` capture run on the first Shown, before caption tests, resizing, session switching or command-tab visits. It reproduced the exact failure with exit 2 in `C:/Users/Sean Shaffer/Documents/DevTools/runs/20261105T065242526Z-c6d7f81c0a24483b89de4488ddd56f3d`. The fix measures only the selected command page and refits when native controls load or their font changes. Final smoke reports `Fresh startup deck: 1260x158, content bottom 142.`; its screenshot was inspected. A new regular-startup live check and independent correction review remain coordinator gates.

Before that bounded sizing correction, the coordinator's full suite passed 107 tests and five isolated consumers of the five packed modules, exit 0: `C:/Users/Sean Shaffer/Documents/DevTools/runs/20261105T064736943Z-490d7d70ef23483f8cdc523e69fe0e68`. Live Computer Use opened the real fixture through the native session picker and ran Compare; Ctrl+End moved both editors to row 150, reverse wheel input moved both to row 149, word highlights remained visible, and normal close required no dirty prompt. Computer Use provides no process exit code. The initial independent layout review passed but did not detect startup sizing; its pass does not substitute for the new correction review.

Known existing limitation: changing the native RichTextBox `WordWrap` property between typing and Undo did not preserve that typing undo fixture. This layout increment does not fix that setter behavior. Orientation changes preserve typing Undo/Redo when fixtures are established with wrap either enabled or disabled; the two claims are intentionally separate.

Actual screenshots reside at `C:/Users/Sean Shaffer/Documents/Codex/2026-11-03/files-pasted-by-the-user-here/work/DiffBeacon/artifacts/`: `VaporGrid-Layout.png`, `.narrow.png`, `.medium.png`, `.wide.png`, `.commands-home.png`, `.commands-merge.png`, `.commands-view.png`, `.side-by-side.png`, `.stacked.png`, `.empty.png`, `.folders.png`, `.block-merge.png`, `.three-way.png` and prompt captures. Narrow, stacked, Home, Merge and View images were inspected. The approved illustrative mockup is `docs/VaporGrid-Comparison-Layout.png`; it is not runtime evidence.

The source worker's Ruflo terminal timed out after 120 seconds without a completed record. The coordinator stopped its exact owned process tree and explicitly handed the saved partial source to the native fallback worker. XERJ remained blocked by its disk floodstage guard; no override, reindex or new dependencies were introduced. Further DPI/text scaling, live high contrast and full keyboard/accessibility acceptance remain unperformed. No performance numbers from the prior renderer build are asserted for this layout binary.

Current alpha/core-preview evidence was collected on Windows x64 with .NET SDK 10.0.403 on 2026-11-03 local time (2026-11-04 UTC). No third-party packages were added. This documents bounded checks, not full replacement readiness.

## Final Responsive Layout And Caption Input Evidence

The final Release build exited 0 with zero warnings/errors. Core tests passed 75/75 (exit 0). Final native smoke exited 0, including 900/1140/1260-pixel widths at 620-pixel height, full-width three-way review commands, a 200px conflict pane, minimum source/review rows and feasible splitter preservation.

DevTools run directories each contain `stdout.log`, `stderr.log` and `result.json`:

- Build: `C:/Users/Sean Shaffer/Documents/DevTools/runs/20261104T001110714Z-29231167a5b64883879498c0bc2c9155` (process and runner exit 0).
- Core tests: `C:/Users/Sean Shaffer/Documents/DevTools/runs/20261104T000450911Z-6bb0c22aeb374594876d9acc841525aa` (exit 0; 75 passed, 0 failed).
- Final native smoke: `C:/Users/Sean Shaffer/Documents/DevTools/runs/20261104T001134446Z-7e93a1d96bd74eac8f25be98e74fd24c` (process and runner exit 0).

Final app DLL: `C:/Users/Sean Shaffer/Documents/Codex/2026-11-03/files-pasted-by-the-user-here/work/DiffBeacon/src/DiffBeacon.App/bin/Release/net10.0-windows/DiffBeacon.dll`; SHA256 `7D6AB6CD8231117E44A3D7A54102CE05C3250DCF1C0F6863EAC84136C66092E1`.

Command bars now wrap without horizontal scrolling. Three-way review actions share the full lower-workspace width beneath both review panes. Maximized caption presses wait for the system drag threshold before restoring beneath the pointer and handing off native movement. Smoke covers signed-coordinate geometry, threshold/capture cancellation and resize cycles.

Live Computer Use observed maximized caption drag restoring 1260×850 beneath the pointer, wrapped commands at 900×620, Shift+Tab/Tab across adjacent command rows, caption single/double-click behavior and ordinary caption movement. Exact observations: `C:/Users/Sean Shaffer/Documents/Codex/2026-11-03/files-pasted-by-the-user-here/work/DiffBeacon/artifacts/drag-toolbar-runtime.md`. Computer Use supplies no process exit code. No user files were loaded or saved in those live input checks.

The controller inspected actual 900px and 1140px main-window captures and the 900px three-way capture. Native output paths:

- `C:/Users/Sean Shaffer/Documents/Codex/2026-11-03/files-pasted-by-the-user-here/work/DiffBeacon/artifacts/DiffBeacon-Responsive.narrow.png`
- `C:/Users/Sean Shaffer/Documents/Codex/2026-11-03/files-pasted-by-the-user-here/work/DiffBeacon/artifacts/DiffBeacon-Responsive.medium.png`
- `C:/Users/Sean Shaffer/Documents/Codex/2026-11-03/files-pasted-by-the-user-here/work/DiffBeacon/artifacts/DiffBeacon-Responsive.three-way.narrow.png`

Documentation copies: [wrapped text commands](DiffBeacon-Responsive-Toolbar.png) and [shared three-way review commands](DiffBeacon-Responsive-Three-Way.png). Their absolute paths are `C:/Users/Sean Shaffer/Documents/Codex/2026-11-03/files-pasted-by-the-user-here/work/DiffBeacon/docs/DiffBeacon-Responsive-Toolbar.png` and `C:/Users/Sean Shaffer/Documents/Codex/2026-11-03/files-pasted-by-the-user-here/work/DiffBeacon/docs/DiffBeacon-Responsive-Three-Way.png`.

The design mockup was created before code at `C:/Users/Sean Shaffer/Documents/Codex/2026-11-03/files-pasted-by-the-user-here/work/DiffBeacon/docs/UI-Mockup-Responsive-Toolbar.html`. Its HTML was not browser-rendered; native captures were inspected. Patch tools provide no exit code.

Local static inspection evidence is retained at `C:/Users/Sean Shaffer/Documents/Codex/2026-11-03/files-pasted-by-the-user-here/work/DiffBeacon/artifacts/reference-pe-evidence.json` and `C:/Users/Sean Shaffer/Documents/Codex/2026-11-03/files-pasted-by-the-user-here/work/DiffBeacon/artifacts/reference-observations.md`. REA tools expose evidence identifiers, not process exit codes. Static imports do not establish runtime behavior.

Independent final code review found no actionable findings: `C:/Users/Sean Shaffer/AppData/Local/Ruflo/state/runs/20261104T001201Z-29884263b444/result.txt` (owned worker exit 0). Ruflo terminal monitoring timed out at 120 seconds; the owned worker completed with exit 0. Later workers used the authenticated local launcher fallback. The documentation-only reconciliation did not rerun application builds or tests.

## Remaining Acceptance

Release/deactivation between queued native movement and dispatch remains timing-unverified; the review found no code-backed defect. Mixed DPI, physical negative-coordinate monitors, touch and full accessibility remain untested. Signed-coordinate smoke assertions are not physical multi-monitor acceptance. Limited Tab checks do not establish complete keyboard accessibility. Windows Snap hover/flyout and real high-contrast switching remain untested. Dialog-driven flows and representative large-file performance still need acceptance. Full replacement readiness requires the ordered workflow and safety gates in FEATURES.md; no completion claim is made.

## Earlier Core And Feature Checks

The following records describe earlier bounded checks; the final build identity and current responsive evidence above supersede earlier build/screenshot identities.
| Command / Gate | Result |
|---|---|
| `dotnet build -c Release` | Exit 0, 0 warnings/errors |
| `dotnet run --project tests/DiffBeacon.Tests -c Release --no-build` | Exit 0, 75 passed / 0 failed |
| `DiffBeacon.exe --smoke <existing-output-directory>/DiffBeacon-Preview.png` | Exit 0; actual native rendering captured |
| `DiffBeacon.exe --smoke <nonexistent-directory>/smoke.png` | Exit 2; real DirectoryNotFoundException confirms failure propagation |
| `DiffBeacon.exe --check SAME SAME` | Exit 0 |
| `DiffBeacon.exe --check DIFFERENT_A DIFFERENT_B` | Exit 1 |
| `DiffBeacon.exe --check EXISTING MISSING` | Exit 2 |
| `./scripts/Publish.ps1 -OutputDirectory ../../outputs` | Exit 0; framework-dependent win-x64 portable ZIP |

Smoke asserts changed-text counts, merge content and result invalidation, undo restoration, edit invalidation, suppression of mid-flight stale results, snippet Swap/New Snippets invalidation, mode changes and resize/render. Core harness verifies alignment, raw preservation under ignore filters, bounds, Unicode/BOM round trips, malformed encodings, mixed-newline refusal, atomic save/backups, stale/locked destination failures, streamed binary edge cases/sample limits, recursion/missing files/cancellation, CSV quoting/multiline/ragged rows, sessions and HTML escaping.

Actual screenshot: `DiffBeacon-Preview.png`. Separate visual design mockup: `UI-Mockup.html`. Five icon concepts are under `Icon-Concepts/`; the selected lighthouse is embedded in the executable/form icon.

Three-way core adds 17 regression tests for disjoint/adjacent/identical edits, insertions, deletion boundaries, transitive overlap, stable IDs, missing/stale choices, strict fallback rejection, limits and cancellation. Three-way native smoke verifies disabled/refused unresolved saves, Accept Both/Remote, choice undo and its 100-entry cap, local BOM/newline-preserving output, stale-source refusal, and suppression of in-flight results when a path changes. Source-path editing retains the prior review/choices, blocks saving, and waits for Compare's one-time discard confirmation. Actual three-way screenshot: `DiffBeacon-Three-Way.png`; design mockup: `UI-Mockup-ThreeWay.html`. Empty startup screenshot: `DiffBeacon-Empty-State.png`.

Folder-filter slice adds 13 core tests for blank compatibility, OR/NOT precedence, case/Unicode, simple-glob semantics including literal `*.*`, parser bounds/rejections, exact directory pruning, root preservation, scoped counts/zero matches, error-only incomplete-scope wording, filtering locked files before content reads, cancellation, v1 migration and v2 validated round trips. Native folder smoke verifies actual filtered results/counts, scope edits invalidate/cancel work, mode/session scope retention, and zero-match wording. Actual screenshot: `DiffBeacon-Folder-Filters.png`; separate prior-layout mockup: `UI-Mockup-Folder-Filters.html`. The 1,000,000 visited-entry branch and privileged access fixtures have not been exercised locally; no stress result is claimed. An independent native junction audit verified in-scope reparse incompleteness, ignored-directory pruning, and reparse-root refusal. Rooted paths are read only.

Aligned block merge adds 18 core fixtures: contiguous replacements, difference-ordinal mapping through equal rows, insertion/deletion, start/end/reverse direction, exact trailing/empty/newline-only tokens, CRLF canonical editor output, separated/repeated-line hunks, Unicode, strict-option/fallback/error/stale/no-op selection rejection, stale apply, cancellation, combined output budgets and complementary hunks exceeding the 2 million character destination-editor cap. Native smoke verifies overview/Previous/Next selection maps to the full hunk, only destination changes, exact undo, disabled/no-mutation rejection guards and unchanged free-text/all merge behavior. Block smoke uses editor snippets and performs no file writes except its screenshot. Mockup: `UI-Mockup-Block-Merge.html`; actual UI: `DiffBeacon-Block-Merge.png`.

Limits: dialog-driven Save As/session/export flows have not been end-to-end automated; their file engines have tests. An independent native junction audit covered reparse handling; privileged inaccessible-tree fixtures remain untested. No multi-GB performance result is claimed. Full native keyboard/high-contrast/accessibility acceptance and remaining professional features require further testing. CI must pass independently before merge.

Artifact command paths outside the repository (local outputs):

- `C:\Users\Sean Shaffer\Documents\Codex\2026-11-03\files-pasted-by-the-user-here\outputs\DiffBeacon-Windows-x64.zip`
- `C:\Users\Sean Shaffer\Documents\Codex\2026-11-03\files-pasted-by-the-user-here\outputs\DiffBeacon-Preview.png`

Files were created with apply_patch (no exit code provided by that tool); all shell build/test/publish commands above returned the stated actual process exit codes.

## Native renderer and VaporGrid build verification — 2026-11-05

The unrestricted SDK 10.0.403 Release build completed with exit 0 and zero warnings/errors: `C:/Users/Sean Shaffer/Documents/DevTools/runs/20261105T062045479Z-2d3d2f9305b44fb3ad7771bcd1c7fe5a`. The resulting `DiffBeacon.dll` SHA256 is `140B42029D60A828C9137FA195EB5F48826B42390AAAA2B3AE1F8107D14B0298`.

The current full native `--smoke` completed with exit 0 and empty stderr: `C:/Users/Sean Shaffer/Documents/DevTools/runs/20261105T062047259Z-431b9cd844cd468c8efdfeae7dae0399`. It covers native typing undo/redo; highlighted replacement, insertion and deletion with repeated undo/redo; nested app-owned undo suspension; externally disabled recording; paint-exception cleanup; lazy offscreen navigation and scrolling in both panes; synchronized/independent scrolling; wrap reflow; selection/modified-state preservation; stale edits; cancellation; superseding comparison and mode generations; sessions; 32-bit horizontal scrolling; responsive layouts; folders; block merge; and three-way merge. Initial failures reproduced missing highlights after navigation and wrap reflow; both now explicitly request visible decoration. Cancel is disabled when idle and after cancellation/completion, and enabled only while the current main comparison owns its cancellation source; native assertions exercise those states.

Actual screenshots are `artifacts/VaporGrid-Native.png`, `.wide.png`, `.narrow.png`, `.medium.png`, `.prompt.png`, `.prompt.default-button.png`, `.three-way.png` and the corresponding three-way responsive variants. The main, narrow and prompt captures were visually inspected. Review corrections give passive labels/checks transparent parent surfaces, preserve a default-button border independently of focus, and choose a contrasting high-contrast grid outline from the selected background. Native smoke focuses the prompt's message, checks the unfocused safe button's rendered border pixel, and preserves Enter/Escape behavior. Child background and high-contrast color-policy assertions run without changing OS settings. These captures and policy assertions do not establish physical DPI, live high-contrast switching, screen-reader or live gesture acceptance.

Three cold processes per fixture completed successfully against styled renderer build `70FC01B8D5F3495AEBAB50C76BA9F56ABEC51EED2EABD70E3B3C9897AB443AA1` and its extracted modules (`artifacts/performance-probe/vaporgrid-native-process-results.json`; all nine child exit codes 0). This precedes the final passive-background/default-button/grid-outline/Cancel-state corrections; renderer algorithms were unchanged by those corrections, but timings were not rerun for the final binary. Each result records all `DiffBeacon*.dll` hashes, retained/painted span counts, difference counts, and grid counts. The fallback fixture verifies the 20,000 retained-span ceiling and 10,000 grid-row ceiling. Summary: `artifacts/performance-probe/vaporgrid-native-summary.json`.

| Fixture per pane | Median comparison and initial-viewport render | Median maximum UI timer gap | Median managed allocation |
|---|---:|---:|---:|
| 2,000 changed short rows | 85.5 ms | 45.7 ms | 12.43 MiB |
| One 20,000-character changed line | 40.1 ms | 30.2 ms | 1.24 MiB |
| 25,000 changed rows, positional fallback | 346.7 ms | 205.2 ms | 89.66 MiB |

The largest observed short-case gap was 48.7 ms; fallback was 206.5 ms. These are message-pump timer observations, not measured input/cancel acknowledgement latency. Three runs are descriptive, not a percentile or confidence/SLA claim. Startup/input assignment are excluded; managed allocation includes all threads. The offscreen probe awaits actual comparison and retained rendering tasks, then updates the form. It measures initial-viewport readiness: offscreen native ranges are deliberately deferred until scrolling while full bounded detail remains retained. Earlier eager-render measurements therefore performed more offscreen work. No multi-GB, all-scroll-path throughput or endurance result is claimed.

Core/package suites, CLI exit-code checks and independent review are reconciled separately by the coordinator; this section records only commands and native/probe gates run for the renderer changes. Remaining manual gates include live wheel/scrollbar/keyboard gestures and rapid reversals, physical 100/150/200% DPI and text scaling, high contrast, screen readers, and measured input/cancel latency. No reusable module API or pilot source changed in this renderer work.

## Historical restricted granular implementation gate (superseded by the native results above)

New --smoke assertions cover native selection/focus/modified/viewport and typing undo preservation, wrapped raw-index decoration, stale detail cancellation and rapid granularity switching, initial Previous, bidirectional mapped scrolling, horizontal synchronization/clamping, disabled/toggled sync, wrapped logical anchors, applied session preferences, equal binary report readiness and empty table report readiness. These are executable assertions added to the existing smoke, not claims of a passed run.

The delegated sandbox build stopped before compilation with MSB4184: ToolLocationHelper.GetPlatformSDKDisplayName(Windows, 7.0) could not access C:/Users/Sean Shaffer/AppData/Local/Microsoft SDKs. No SDK configuration was changed. Parent must run the ordinary DevTools build, core harness and native --smoke outside that restriction, then audit the final diff.

Remaining acceptance: live wheel/scrollbar/keyboard gestures on both panes, narrow/high-contrast rendering, large-document viewport/undo behavior, and fixture measurements (2,000 changed rows; long changed line; 50,000-line positional fallback) including render time, peak memory and responsive scroll/cancel. Existing earlier screenshots and exit codes above apply to prior slices and do not establish these new gates.

A fallback direct Roslyn source compile exited 0 with nullable and warnings-as-errors enabled, using installed .NET 10.0.14 core/WindowsDesktop reference assemblies and SDK 10.0.403 csc.dll. It compiled App/Core source excluding unchanged Program.cs (whose ApplicationConfiguration bootstrap is SDK generated), writing src/DiffBeacon.App/obj/granular-compile.dll. This is source/type verification only, not the ordinary application build or a native smoke pass. Scoped git diff --check exited 0.

Follow-up native audit fixes (pending unrestricted rerun):

- The prior native run `C:/Users/Sean Shaffer/Documents/DevTools/runs/20261104T004006651Z-a2da5b3394ba466587a858e3ebedea65` completed with process/runner exit 2. Its stderr reports `Granular decoration changed native editor state`; its screenshot was missing. It is a failed gate.
- Installed SDK `10.0.26100.0/um/TOM.h` confirms ITextDocument IID `8CC497C0-A1DF-11CE-8098-00AA0047BE5D`, IDispatch inheritance, Undo slot 22, and tomSuspend/tomResume values -9999995/-9999994. Real Windows probes established that successful suspend and resume return S_FALSE with actual count 0; HRESULT does not identify prior suspension ownership. Native suspension is not nested. The corrected editor coordinates all app-owned scopes with managed depth and balances the outermost native suspend/resume; negative HRESULTs still fail. Externally disabled recording remains disabled. Unrelated callers directly suspending raw COM are outside the editor API because TOM exposes no prior-suspension query. Native smoke verifies nested app scopes, typed undo/redo and exception recovery. [Microsoft's Undo reference](https://learn.microsoft.com/en-us/windows/win32/api/tom/nf-tom-itextdocument-undo) documents general HRESULT semantics but omits these extended transitions.
- TextChanged invalidates comparison immediately but defers decoration clearing until the native edit notification returns. Formatting rejects reentrant edits. Native selection-state failures now include selection, focus, modified flag, undo availability, viewport and suspend HRESULT/actual diagnostics.
- Horizontal source positions use GetScrollInfo SIF_POS (32 bits), with a native >65,535-pixel synchronization/preservation assertion. Tab sessions use actual tab metadata and literal `\t` in the control; smoke saves/loads a captured UI session and parses tab-separated table input. A 10,002-line fixture checks that a granularity refresh retains the 10,000-row grid warning. Both text panes have explicit accessible names.
- Smoke-owned comparisons propagate errors, asynchronous refresh errors mark the owning smoke failed, and smoke-owned close/discard paths bypass prompts. Normal GUI unsaved-change protection remains active. Argument routing assertions cover GUI paths, explicit modes, smoke, help and check; valid GUI path launches no longer attach a console.
- Ordinary `dotnet build src/DiffBeacon.App/DiffBeacon.App.csproj -c Release --no-restore` still exited 1 at MSB4184, before compilation, because this sandbox denies the user Microsoft SDKs directory. A direct installed Roslyn compile subsequently exited 0 with nullable/warnings-as-errors and both installed WinForms analyzer assemblies, including Program.cs and the real ApplicationConfiguration generator. Output: `src/DiffBeacon.App/obj/granular-compile.dll`. This checks compilation only; it does not pass native or ordinary Release gates.
