"""Collect existing acceptance evidence; never treats an unexecuted check as passed."""
import json
from datetime import datetime, timezone
from pathlib import Path

root = Path(__file__).resolve().parents[2]
out = root / "artifacts/m2"
clipboard = sorted((out / "clipboard").glob("run-*/report.json"))[-1]
report = json.loads(clipboard.read_text(encoding="utf-8-sig"))
desktop = json.loads((out / "verification.json").read_text(encoding="utf-8-sig"))
summary = {
    "capturedAtUtc": datetime.now(timezone.utc).isoformat(),
    "desktop": desktop["summary"],
    "clipboardReport": str(clipboard.relative_to(root)),
    "clipboardCases": [{"id": r["id"], "status": r["status"]} for r in report["results"]],
    "wordBaseline": {"version": report["wordVersion"], "build": report["wordBuild"]},
    "nativeFollowup": "artifacts/m2/native-followup.json",
    "imageVerification": str((clipboard.parent / "image-verification.json").relative_to(root)),
    "limitations": [
        "Native screenshot capture and common-file-dialog focus are unreliable in Computer Use; no completed native Save As claim.",
        "ImageFileExporter real file writes, locked destination preservation and cleanup are tested separately.",
        "Word Flat OPC exposes a PNG after SVG import on this baseline; preserved vector data is not claimed.",
        "Word interprets MathML text as an equation; verified canonical structure is equal in the tested fixture.",
        "Word SaveAs2 timed out in initial probe attempts. Final clipboard comparison uses in-memory WordOpenXML, not save/reopen.",
        "No Word connector, Word write guard, broad IME certification or clean-machine certification."
    ]
}
(out / "acceptance.json").write_text(json.dumps(summary, ensure_ascii=False, indent=2), encoding="utf-8")
print(json.dumps(summary, ensure_ascii=True, indent=2))
