# Native host integration checks

Run on Windows with Word installed, after publishing Desktop. Close Word before running; the runner refuses an existing WINWORD process and an existing output directory. Use only a new disposable report directory.

```powershell
dotnet run --project tests/Locus.HostAcceptance.Tests -c Release -- <published-desktop-directory> <new-report-directory>
```

The STA runner loads `NativeClipboard` from the published `Locus.Desktop.Shared.dll`, records its SHA-256, writes synthetic text/pixels to the real Windows clipboard, and has its own Word instance paste/save/reopen. It backs up supported clipboard formats in memory and restores them in `finally`; it refuses unsupported formats instead of risking loss. It does not record the original clipboard contents. Word documents and output files are owned by the test.

These checks cover the adapter and Word's paste API. They do not cover Desktop button wiring, physical keyboard/IME input, tray actions, canvas pointer behavior or the Word connector.

## Optional real Save As dialog probe

```powershell
dotnet run --project tests/Locus.HostAcceptance.Tests -c Release -- --file-picker <published-desktop-directory> <synthetic-fixture.locus> <new-report-directory>
```

The probe opens three real Save As dialogs: cancel, save to the exact path displayed by the test window/`step.json`, then save after the source becomes stale (two seconds after the third dialog opens). The dialogs must be operated externally through an approved computer-use tool or by a person. The runner does not synthesize input.

The stale step cannot distinguish Escape from pressing Save solely from a `Cancelled` adapter result. It therefore reports `NEEDS_SAVE_ACTION_EVIDENCE`, never automatic PASS, until a separate observation confirms the actual Save action. Escape for cleanup is not acceptance of the stale guard. `report.json` records raw results; keep a separate acceptance receipt when tool failures prevent the intended interaction.

The first attempt on 28/09 predates this correction. See `artifacts/host-review/20260928/native-file-picker/acceptance.json`; its raw stale `passed=true` is explicitly not accepted.
