# Locus Word — W0 research connector

COM add-in `.NET Framework 4.8 x86`, ProgID `Locus.Word.W0`. Uses the real `Locus.Core` project. This is a research adapter; it is not the M3 production Word feature.

Build with `dotnet build Locus.Word.sln -c Release`. The separate solution keeps Word build prerequisites out of the independent Desktop solution. Requires the installed Word/Office 15 PIAs under `%WINDIR%\assembly\GAC_MSIL` (override `OfficePiaDirectory`) and the installed Extensibility interop at `%ProgramFiles(x86)%\Common Files\Microsoft Shared\MSEnv\PublicAssemblies`. No Office assemblies are downloaded or redistributed.

Run `./tools/w0/verify.ps1` when Word is closed. It builds, registers only three owned HKCU registry trees in the 32-bit view, exercises fresh synthetic Word documents, and unregisters on completion. It does not change Word's COM application association, other add-ins, Trust Center or document protection. Registration is local development codebase registration for this workspace, not a signed shipping installer.

For an interactive research session:

```powershell
./tools/w0/register-probe.ps1 -Action Install
& C:/Windows/SysWOW64/WindowsPowerShell/v1.0/powershell.exe -NoProfile -STA -ExecutionPolicy Bypass -File tools/w0/start-probe.ps1
./tools/w0/read-probe.ps1 -Label observation
```

Close Word, then `./tools/w0/register-probe.ps1 -Action Uninstall`. The add-in writes only to documents it explicitly creates as sandboxes. Reconnection invalidates session IDs and does not re-arm existing documents. Canceling a close also leaves the document unarmed, conservatively. It never watches source from ordinary documents and never intercepts Undo. The explicitly started B trial updates native on Space only in its owned fixture.

Desktop can read the trial connector with `Locus.Desktop.exe --word-probe <report-directory>` from a source build. This diagnostic entry point has no write commands and does not start Word or enable a disabled add-in. It is not included in the already-issued M2 portable artifact. Pipe access is restricted to the current Windows user, with bounded replies and connection timeouts.

The adapter accepts manually selected main-story text only; it refuses tracked, protected, read-only, table, existing equation/field/shape/control and unsupported source contexts. Native/source/history are stored together in a versioned Tag, with the core serializer's checksum. Duplicate, absent, corrupt or future metadata is refused. Native drift is compared within the M1 OMML subset, ignoring formatting/run boundaries; that is not a general OMML semantic equivalence algorithm.

All adapter mutations require the Word UI thread. Out-of-process callers must use the add-in dispatcher: multiple separate COM calls did not preserve custom Undo reliably after clipboard operations. `RunSandboxResearchOperation` is a bounded fixture/fault-injection entry point for owned research documents, not the production conversion API.

Production automatic marker/Space conversion is disabled. A 250 ms quiet period plus zero IMM composition is only an explicit-trigger research check: UniKey Telex can change text without standard composition events. It does not prove input completion or unlock auto. The B source-session prototype uses an experimental 300 ms check in a dedicated sandbox, never ordinary documents.

The caret after explicit conversion inherits plain-text insertion mode from the next Word character. Metadata validation compares the full document-level equation as well as the content-control view, which can clip hidden boundaries. See [continuation research](../../docs/w0/CONTINUATION.md) for the API matrix and physical VNI result; this does not decide auto-Space session behavior.

See [W0 report](../../docs/w0/REPORT.md) for exact pass/fail, artifacts, UI limitations and remaining gates. CW is open for implementing the manual M3 flow on the tested baseline; product release gates remain closed.

Run `./tools/w0/prepare-trial.ps1` with Word closed to open the fx test. The **Locus W0** Ribbon tab provides the source-preserving A trial and native-live B trial; each starts a new owned document. [Trial guide](../../docs/w0/TRIAL-GUIDE.md), [native Space findings](../../docs/w0/SPACE-NATIVE.md). The panel previews the same candidate tree that is committed; it is a research renderer, not the Desktop production renderer.

`./tools/w0/verify.ps1 -IncludeUxResearch` adds seven UX research groups. When the final fx document opens, activate its real Word editor within 30 seconds without typing; the test refuses an outside-editor focus. Geometry assertions use the actual Word/WinForms window. DPI layout cases are synthetic and do not certify multiple physical displays. The default runner remains suitable for the API/lifecycle verification without this interactive focus prerequisite.
