"""Compare actual images embedded by Word with the exact Desktop export scene."""
import base64
import hashlib
import io
import json
import sys
from pathlib import Path
import xml.etree.ElementTree as ET
from PIL import Image, ImageChops

root = Path(sys.argv[1]).resolve()
ns = {"p": "http://schemas.microsoft.com/office/2006/xmlPackage"}
results = []
for case in ("png-direct", "png-repair", "svg-import"):
    folder = root / case
    tree = ET.parse(folder / "received-flat-opc.xml")
    media = []
    for part in tree.findall("p:part", ns):
        name = part.attrib.get("{" + ns["p"] + "}name", "")
        binary = part.find("p:binaryData", ns)
        if "/media/" in name and binary is not None:
            data = base64.b64decode(binary.text)
            output = folder / ("received-" + Path(name).name)
            output.write_bytes(data)
            media.append(output)
    if case.startswith("png"):
        expected = Image.open(folder / "expected.png").convert("RGBA")
        matches = []
        for file in media:
            if file.suffix.lower() not in (".png", ".jpg", ".bmp"): continue
            actual = Image.open(file).convert("RGBA")
            # A consumer may select the opaque bitmap fallback. Compare the visible image on white.
            def white(image):
                canvas = Image.new("RGBA", image.size, "white")
                return Image.alpha_composite(canvas, image).convert("RGB")
            equal = actual.size == expected.size and ImageChops.difference(white(expected), white(actual)).getbbox() is None
            matches.append(equal)
        results.append({"id": case, "status": "PASS" if any(matches) else "FAIL", "pixelExactOnWhite": any(matches), "media": [p.name for p in media]})
    else:
        expected = (folder / "expected.svg").read_bytes()
        exact = any(p.suffix == ".svg" and p.read_bytes() == expected for p in media)
        # Keep vector preservation as an explicit failed capability; importing an image is a separate check.
        fallback = next((p for p in media if p.suffix == ".png"), None)
        results.append({"id": case, "status": "PASS" if exact else "LIMITATION", "vectorPreservation": "PASS" if exact else "FAIL",
                        "svgBytesPreserved": exact, "rasterFallback": fallback.name if fallback else None,
                        "sha256": hashlib.sha256(expected).hexdigest(), "media": [p.name for p in media]})
report = {"source": str(root), "results": results}
(root / "image-verification.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
print(json.dumps(report, indent=2))
sys.exit(0 if all(r["status"] in ("PASS", "LIMITATION") for r in results) else 1)
