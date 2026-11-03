# Local Verification

Verified on Windows x64 with .NET SDK 10.0.403, 2026-11-03. All commands used real configured tools; no third-party packages added.

| Command / Gate | Result |
|---|---|
| `dotnet build -c Release` | Exit 0, 0 warnings/errors |
| `dotnet run --project tests/DiffBeacon.Tests -c Release --no-build` | Exit 0, 27 passed / 0 failed |
| `DiffBeacon.exe --smoke <existing-output-directory>/DiffBeacon-Preview.png` | Exit 0; actual native rendering captured |
| `DiffBeacon.exe --smoke <nonexistent-directory>/smoke.png` | Exit 2; real DirectoryNotFoundException confirms failure propagation |
| `DiffBeacon.exe --check SAME SAME` | Exit 0 |
| `DiffBeacon.exe --check DIFFERENT_A DIFFERENT_B` | Exit 1 |
| `DiffBeacon.exe --check EXISTING MISSING` | Exit 2 |
| `./scripts/Publish.ps1 -OutputDirectory ../../outputs` | Exit 0; framework-dependent win-x64 portable ZIP |

Smoke asserts changed-text counts, merge content and result invalidation, undo restoration, edit invalidation, suppression of mid-flight stale results, snippet Swap/New Snippets invalidation, mode changes and resize/render. Core harness verifies alignment, raw preservation under ignore filters, bounds, Unicode/BOM round trips, malformed encodings, mixed-newline refusal, atomic save/backups, stale/locked destination failures, streamed binary edge cases/sample limits, recursion/missing files/cancellation, CSV quoting/multiline/ragged rows, sessions and HTML escaping.

Actual screenshot: `DiffBeacon-Preview.png`. Separate visual design mockup: `UI-Mockup.html`. Five icon concepts are under `Icon-Concepts/`; the selected lighthouse is embedded in the executable/form icon.

Limits: dialog-driven Save As/session/export flows have not been end-to-end automated; their file engines have tests. Reparse avoidance is implemented, but privileged reparse/inaccessible-tree fixtures were not exercised locally. No multi-GB performance result is claimed. Full native keyboard/high-contrast/accessibility acceptance and remaining professional features require further testing. CI must pass independently before merge.

Artifact command paths outside the repository (local outputs):

- `C:\Users\Sean Shaffer\Documents\Codex\2026-11-03\files-pasted-by-the-user-here\outputs\DiffBeacon-Windows-x64.zip`
- `C:\Users\Sean Shaffer\Documents\Codex\2026-11-03\files-pasted-by-the-user-here\outputs\DiffBeacon-Preview.png`

Files were created with apply_patch (no exit code provided by that tool); all shell build/test/publish commands above returned the stated actual process exit codes.
