"""Reproduce the 16 M1 preview HTTP checks against an already running local server.

Run: python tools/m1/verify_http.py --port 4180
Only fixed synthetic inputs are sent. The script never starts or stops a server.
"""
from __future__ import annotations

import argparse
import datetime
import json
import pathlib
import urllib.error
import urllib.request


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--port", type=int, default=4180)
    parser.add_argument("--output", type=pathlib.Path)
    args = parser.parse_args()
    if not 1024 <= args.port <= 65535:
        parser.error("port must be between 1024 and 65535")
    base = f"http://127.0.0.1:{args.port}"
    results: list[dict] = []

    def call(path="/", data=None, extra=None):
        headers = {"Origin": base}
        if data is not None:
            headers["Content-Type"] = "application/json"
        for key, value in (extra or {}).items():
            if value is None:
                headers.pop(key, None)
            else:
                headers[key] = value
        request = urllib.request.Request(base + path, data=data, headers=headers)
        try:
            with urllib.request.urlopen(request, timeout=10) as response:
                return response.status, response.headers, response.read()
        except urllib.error.HTTPError as error:
            return error.code, error.headers, error.read()

    def check(name, ok, details=None):
        results.append({"id": name, "status": "PASS" if ok else "FAIL", "details": details})

    for path, mime in [("/", "text/html"), ("/app.js", "text/javascript"), ("/app.css", "text/css")]:
        code, headers, body = call(path)
        check("asset" + path, code == 200 and mime in headers.get("Content-Type", "") and len(body) > 100,
              {"status": code, "mime": headers.get("Content-Type")})
        check("headers" + path, headers.get("Cache-Control") == "no-store" and
              headers.get("X-Content-Type-Options") == "nosniff" and
              "frame-ancestors 'none'" in headers.get("Content-Security-Policy", ""))

    for name, payload, count, original in [
        ("ambiguity", {"input": "2/3x", "mode": "explicit", "revision": 19}, 2, "2/3x"),
        ("custom-marker-nfd", {"input": "\U0001f642 \u27e6x mu\u0303 2\u27e7", "mode": "marked",
                               "open": "\u27e6", "close": "\u27e7", "revision": 21},
         1, "\u27e6x mu\u0303 2\u27e7"),
    ]:
        code, _, body = call("/analyze", json.dumps(payload).encode("utf-8"))
        value = json.loads(body)
        candidates = value.get("regions", [{}])[0].get("candidates", [])
        check(name, code == 200 and len(candidates) == count and
              value["source"]["revision"] == payload["revision"] and value["source"]["raw"] == payload["input"] and
              all(candidate["id"] == candidate["exports"]["candidateId"] and
                  candidate["exports"]["originalText"] == original for candidate in candidates),
              {"status": code, "candidates": len(candidates)})

    body = json.dumps({"input": "x^2", "mode": "explicit"}).encode("utf-8")
    for name, path, data, extra, expected in [
        ("bad-host", "/", None, {"Host": f"evil.example:{args.port}"}, 403),
        ("bad-origin", "/analyze", body, {"Origin": "https://evil.example"}, 403),
        ("no-origin", "/analyze", body, {"Origin": None}, 403),
        ("form-type", "/analyze", body, {"Content-Type": "application/x-www-form-urlencoded"}, 415),
        ("bad-json", "/analyze", b"{", None, 400),
        ("oversize-body", "/analyze", json.dumps({"input": "a" * 262145}).encode("utf-8"), None, 413),
        ("oversize-source", "/analyze", json.dumps({"input": "a" * 65537}).encode("utf-8"), None, 400),
        ("unlisted-file", "/Program.cs", None, None, 404),
    ]:
        code, _, _ = call(path, data, extra)
        check(name, code == expected, {"expected": expected, "actual": code})

    output = args.output or pathlib.Path(__file__).resolve().parents[2] / "artifacts/m1/http-smoke.json"
    output.parent.mkdir(parents=True, exist_ok=True)
    summary = {"passed": sum(r["status"] == "PASS" for r in results), "failed": sum(r["status"] == "FAIL" for r in results)}
    output.write_text(json.dumps({
        "capturedAtUtc": datetime.datetime.now(datetime.timezone.utc).isoformat(),
        "url": base, "summary": summary, "results": results,
    }, indent=2), encoding="utf-8")
    print(f"HTTP verification: {summary['passed']}/{len(results)} passed. Report: {output}")
    return 1 if summary["failed"] else 0


if __name__ == "__main__":
    raise SystemExit(main())
