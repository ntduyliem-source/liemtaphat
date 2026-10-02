# Fixed Word candidates for M0

These 20 fixtures exercise native Word equation structures. They do **not** parse
Vietnamese input and do not define the production MathDocument schema. Each
candidate stores its fixed tree and the exact OMML emitted from that tree. The
precedence example includes a separate repair candidate, so metadata preservation
can be tested with more than one candidate.

Regenerate the JSON without starting Word:

```powershell
python .\fixtures\m0\word\generate_candidates.py
```

Run from the repository root. `-ExecutionPolicy Bypass` applies only to the launched
PowerShell process; the probe does not change machine/user execution policy.

```powershell
# XML, fixture and PowerShell loading checks only. Does not instantiate COM.
powershell.exe -NoProfile -ExecutionPolicy Bypass -STA -File .\tools\m0\word_probe.ps1 -ValidateOnly

# First real smoke test: one fixed candidate.
powershell.exe -NoProfile -ExecutionPolicy Bypass -STA -File .\tools\m0\word_probe.ps1 -Suite Native -CaseId 02-fraction

# All 20 native structures, save/reopen and COM edits inside equations.
powershell.exe -NoProfile -ExecutionPolicy Bypass -STA -File .\tools\m0\word_probe.ps1 -Suite Native

# Custom XML + tagged control, single Undo/Redo, source restore and detach.
powershell.exe -NoProfile -ExecutionPolicy Bypass -STA -File .\tools\m0\word_probe.ps1 -Suite Lifecycle

# Inject failures after native insert, control creation, metadata and selection.
powershell.exe -NoProfile -ExecutionPolicy Bypass -STA -File .\tools\m0\word_probe.ps1 -Suite Faults

# Actual synthetic document mutations plus injected admission state flags.
powershell.exe -NoProfile -ExecutionPolicy Bypass -STA -File .\tools\m0\word_probe.ps1 -Suite Stale

# Complete Tag snapshots: small/1k/4k/16k/64k, faults, lifecycle and Unicode.
powershell.exe -NoProfile -ExecutionPolicy Bypass -STA -File .\tools\m0\word_probe.ps1 -Suite Tag

# All automated suites. Real IME/focus/keyboard UI criteria remain separate.
powershell.exe -NoProfile -ExecutionPolicy Bypass -STA -File .\tools\m0\word_probe.ps1
```

On the inspected baseline, the 64-bit COM registry view resolves Word.Application
to WPS while the 32-bit view resolves it to Microsoft Word. The script now checks
the active registry view before creating COM and refuses a non-Microsoft server.
Use the verified 32-bit launcher on that machine:

```powershell
& 'C:\Windows\SysWOW64\WindowsPowerShell\v1.0\powershell.exe' -NoProfile -ExecutionPolicy Bypass -STA -File .\tools\m0\word_probe.ps1 -Suite Native
```

This is a baseline-specific activation finding, not a general requirement that
every Word COM client match Office bitness. The probe does not change registry
entries, application associations or WPS settings.

Close all Word processes before a real run. The script refuses existing WINWORD
processes, creates a new Word.Application, and resolves a newly created synthetic
document window to its process before claiming ownership. It never attaches to an
existing ActiveDocument. All files it opens and saves are generated under that
run's `artifacts/m0/word/run-*` directory. It only closes its own synthetic
documents and only calls Quit after process ownership verification. It does not
force-kill processes or change user documents.

Each run writes:

- `report.json`: per-criterion PASS, FAIL or UNTESTED, expected/actual observations,
  exception chains and environment/ownership information.
- `progress.json`: last entered stage and elapsed time, useful if COM blocks.
- `ownership.json`: the proven Word process identity, when available.
- `activation-preflight.json`: the current-process COM registration and executable
  identity, read before any COM creation.
- `candidates-used.json`: the exact fixture input for reproduction.
- Synthetic DOCX, OOXML and snapshot JSON artifacts from completed steps.

The timeout is advisory at stage boundaries. A synchronous COM call may hang;
inspect ownership and progress from an external supervisor before acting. Never
terminate every WINWORD process to recover one probe.

Interpret results by criterion. The 20 `.fixture` results validate fixture XML;
they are not 20 native Word successes. Canonical comparison ignores run splitting
and presentation styling while preserving tree structure and selected semantic
properties. A structural mismatch is reported for examination, even if it might
later prove to be an equivalent Word normalization. No pixel-level equality is
claimed. Native edits here use Word-provided character ranges inside an OMath;
actual keyboard editing must be observed separately. Observed mathematical italic
characters in `OMath.Range.Text` can differ from plain letters in OMML. The probe
uses compatibility normalization only to match a known native fixture token, then
edits the actual character Range returned by Word. It neither normalizes the raw
source nor calculates Word offsets from a string length.

The Undo probe intentionally avoids reading `WordOpenXML` before its Undo/Redo
sequence. It compares text, native equation count, control tags and custom XML
contents, and reports a metadata orphan as a failure even if text is restored.
The Tag suite retains the full source, version and candidate snapshot; rejected or
truncated payloads are failures, not an invitation to omit required fields.
The tested tag payload sizes are observations for the recorded host, not a
universal maximum. The extra tag suite tests fault rollback, restore/detach with
Undo/Redo, Save As, and a COM `Range.FormattedText` copy across synthetic documents.
That copy test does not touch or prove the system Clipboard. The Unicode storage
case preserves deliberately injected NFC/NFD, emoji and whitespace history; it
does not claim that the injected text is a valid formula or prove body-text
normalization behavior.

Source restoration first captures the metadata and a live Word range collapsed
at the managed control's start. It deletes that complete control and its contents
with COM `ContentControl.Delete(true)`, then inserts the stored source through
that same live collapsed range, all in one custom Undo record. Report observations
include control/anchor positions and Word-provided body slices before deletion,
after deletion and after insertion. Detach uses
`Delete(false)` to retain current content. See
[ContentControl.Delete](https://learn.microsoft.com/en-us/office/vba/api/word.contentcontrol.delete).

Earlier restore experiments are retained in reports: assigning `Range.Text`
inside an equation kept the OMath container; removing OMath first caused a
duplicated range to shrink and left old text outside it. Neither result met the
exact-source criterion. Recreating a range from a stored integer start after full
control deletion then inserted one character too late on the inspected host.
The current live-anchor experiment records Word's actual boundary changes rather
than subtracting an assumed control-marker width, and is still judged by exact
text and native/control counts.

The cross-document `FormattedText` experiment records its actual result even when
it copies the equation without the surrounding content control. A failed wrapper
copy is not relabeled as successful metadata preservation and is not generalized
to other routes such as the system Clipboard, which remains a separate test.

Stale admission tests do make real changes in synthetic documents. Input state,
focus state, config and session tokens are deliberately injected test values.
They do not prove detection of UniKey composition, Ribbon/Find focus, or races in
the final host-to-Word commit. Those criteria remain UNTESTED in the report.

## Recorded baseline evidence, 2026-09-12

These are observations from Windows 10 Pro build 19045 x64 with Microsoft Word
16.0.14026.20302 x86, activated by the verified 32-bit COM registry view. They do
not establish support for every Office version, architecture or installation.

| Report | Recorded outcome | Interpretation |
| --- | --- | --- |
| [All, e620d0](../../../artifacts/m0/word/run-20260912T051305104Z-e620d0/report.json) | 173 PASS / 7 FAIL / 10 UNTESTED | Includes **100 native assertions** across 20 fixed structures: native OMath, candidate structure, surrounding text/formatting, save/reopen, and native COM edit. All 100 passed. Its two source-restore failures were superseded by the targeted final runs below; its other failures remain evidence. |
| [Final Lifecycle, ff4424](../../../artifacts/m0/word/run-20260912T052645343Z-ff4424/report.json) | 26 PASS / 2 FAIL / 10 UNTESTED | Exact original-source restore now passes with no OMath, control or Locus XML remaining. Custom XML single-Undo rollback and the separate strict-selection experiment still fail. |
| [Final Tag, 4da475](../../../artifacts/m0/word/run-20260912T052718842Z-4da475/report.json) | 54 PASS / 1 FAIL / 10 UNTESTED | Complete Tag snapshot, fault rollback, source restore/detach and their one-Undo/Redo tests pass. The tested cross-document `FormattedText` route still drops the control and Tag. |

Every row's total includes the same 20 fixture-only XML checks. Do not sum these
totals as unique product tests or count fixture checks as native Word operations.
The final targeted runs did not rerun the unchanged 20-case native suite.

The Tag suite accepted exact payloads of **775, 1,024, 4,096, 16,384 and 65,536
UTF-16 code units**, preserving each through write/readback, one Undo/Redo and
DOCX save/reopen. The larger size probes add padding to a complete snapshot; they
do not prove performance with a large semantic tree. The separate 4,096-unit
lifecycle case preserves both direct and repair candidates through Save As and
reopen. Four injected conversion failures also roll back the native content and
Tag together. NFC/NFD, emoji and escaped whitespace storage passes separately.
These are tested sizes, not an API maximum or a production storage budget.

The live restore anchor was observed moving from Word position 27 to 26 in the
Custom XML case, and 32 to 31 in the Tag case, when Word deleted the wrapper.
Both final reports retain the boundary traces and exact unchanged surrounding
text. Tag restoration after native editing also passes a single Undo back to the
edited native equation with its original source/candidate history, followed by a
single Redo to the exact restored source.

Custom XML remains a rejected transaction-storage approach for the tested path:
one Undo restores source and removes the control but leaves one orphan XML part;
faults after metadata insertion reproduce the orphan. No compensating cleanup is
hidden in those assertions. The separate strict-selection test observes the
original selection 30..35 becoming a caret at 30 after Undo. Passing Tag
content/metadata rollback does not resolve that selection finding.

The cross-document copy failure is equally retained: assigning the managed
control's `Range.FormattedText` produces one native equation and zero content
controls in the destination. Therefore that exact copy route cannot promise
metadata preservation. System Clipboard copy, keyboard editing, actual IME
composition, actual editor focus, Space continuation, inline fx positioning,
add-in startup/lifecycle, application-level Save As identity and multi-document
or coauthoring behavior remain outside this automated probe. Additional host/UI
and version-matrix evidence is required before enabling automatic conversion.
