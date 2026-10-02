"""Generate fixed M0 candidates without Word, a parser, or third-party libraries.

This small exporter is a discovery fixture builder, not the production MathDocument
schema or parser. Each saved candidate carries the exact tree and OMML used by the
COM probe. Run this file with Python 3 to regenerate candidates.json.
"""
from pathlib import Path
import json
from xml.sax.saxutils import escape, quoteattr

NS = "http://schemas.openxmlformats.org/officeDocument/2006/math"


def n(kind, *children, **props):
    return {"kind": kind, "children": list(children), **props}


def t(value):
    return n("text", value=str(value))


def row(*children):
    return n("row", *[t(c) if isinstance(c, (str, int)) else c for c in children])


def wrap(tag, children):
    return f"<m:{tag}>{children}</m:{tag}>"


def prop(tag, value):
    return f"<m:{tag} m:val={quoteattr(str(value))}/>"


def emit(node):
    kind = node["kind"]
    c = [emit(x) for x in node["children"]]
    if kind == "text":
        return wrap("r", wrap("t", escape(node["value"])))
    if kind == "row":
        return "".join(c)
    if kind == "fraction":
        return wrap("f", wrap("num", c[0]) + wrap("den", c[1]))
    if kind == "radical":
        degree = c[1] if len(c) > 1 else ""
        return wrap("rad", wrap("radPr", prop("degHide", 0 if degree else 1)) + wrap("deg", degree) + wrap("e", c[0]))
    if kind in ("power", "subscript"):
        tag, index = ("sSup", "sup") if kind == "power" else ("sSub", "sub")
        return wrap(tag, wrap("e", c[0]) + wrap(index, c[1]))
    if kind == "subsup":
        return wrap("sSubSup", wrap("e", c[0]) + wrap("sub", c[1]) + wrap("sup", c[2]))
    if kind == "delimiter":
        return wrap("d", wrap("dPr", prop("begChr", node.get("begin", "(")) + prop("endChr", node.get("end", ")"))) + wrap("e", c[0]))
    if kind == "nary":
        return wrap("nary", wrap("naryPr", prop("chr", node["operator"]) + prop("limLoc", node.get("limits", "undOvr"))) + wrap("sub", c[0]) + wrap("sup", c[1]) + wrap("e", c[2]))
    if kind == "matrix":
        cols = node["columns"]
        return wrap("m", "".join(wrap("mr", "".join(wrap("e", x) for x in c[i:i + cols])) for i in range(0, len(c), cols)))
    if kind == "function":
        return wrap("func", wrap("fName", c[0]) + wrap("e", c[1]))
    if kind == "limitlow":
        return wrap("limLow", wrap("e", c[0]) + wrap("lim", c[1]))
    if kind == "accent":
        return wrap("acc", wrap("accPr", prop("chr", node["accent"])) + wrap("e", c[0]))
    if kind == "equationArray":
        return wrap("eqArr", "".join(wrap("e", x) for x in c))
    raise ValueError(kind)


def fraction(a, b):
    return n("fraction", a, b)


def power(a, b):
    return n("power", a, b)


def candidate(cid, tree, kind="direct", label="According to the typed source"):
    return {
        "candidateId": cid,
        "kind": kind,
        "label": label,
        "mathDocument": {"schemaVersion": "m0-fixed-tree-1", "root": tree},
        "omml": f'<m:oMath xmlns:m="{NS}">{emit(tree)}</m:oMath>',
    }


def case(cid, source, tree, token="x", replacement="z", alternatives=()):
    primary = candidate(cid + ":direct", tree)
    return {
        "caseId": cid,
        "source": source,
        "selectedCandidateId": primary["candidateId"],
        "candidates": [primary, *alternatives],
        "nativeEdit": {"find": token, "replace": replacement},
    }


cases = [
    case("01-arithmetic", "x + 1", row("x", "+", 1)),
    case("02-fraction", "1 tr\u00ean 2", fraction(t(1), t(2)), "1", "9"),
    case("03-precedence", "x+1/2", row("x", "+", fraction(t(1), t(2))), alternatives=[candidate("03-precedence:repair", fraction(row("x", "+", 1), t(2)), "repair", "Put x+1 in the numerator")]),
    case("04-parenthesized-numerator", "(x+1)/2", fraction(row("x", "+", 1), t(2))),
    case("05-nested-fraction", "(1/2)/(x+1)", fraction(fraction(t(1), t(2)), row("x", "+", 1))),
    case("06-square-root-number", "can2", n("radical", t(2)), "2", "9"),
    case("07-square-root-expression", "c\u0103n (x+1)", n("radical", row("x", "+", 1))),
    case("08-cube-root", "c\u0103n b\u1eadc 3 c\u1ee7a x", n("radical", t("x"), t(3))),
    case("09-superscript", "x m\u0169 2", power(t("x"), t(2))),
    case("10-subscript", "x ch\u1ec9 s\u1ed1 1", n("subscript", t("x"), t(1))),
    case("11-subsup", "x_i^2", n("subsup", t("x"), t("i"), t(2))),
    case("12-delimiter-power", "(-x)^2", power(n("delimiter", row("-", "x")), t(2))),
    case("13-summation", "sum i=1..n i^2", n("nary", row("i", "=", 1), t("n"), power(t("i"), t(2)), operator="\u2211"), "i", "j"),
    case("14-integral", "integral 0..1 x dx", n("nary", t(0), t(1), row("x", "d", "x"), operator="\u222b", limits="subSup")),
    case("15-product", "product k=1..n k", n("nary", row("k", "=", 1), t("n"), t("k"), operator="\u220f"), "k", "j"),
    case("16-matrix", "matrix[[1,2],[3,4]]", n("matrix", t(1), t(2), t(3), t(4), columns=2), "1", "9"),
    case("17-function", "sin(x)", n("function", t("sin"), n("delimiter", t("x")))),
    case("18-limit", "lim x->0 sin(x)/x", row(n("limitlow", t("lim"), row("x", "\u2192", 0)), fraction(n("function", t("sin"), t("x")), t("x")))),
    case("19-vector", "vector x", n("accent", t("x"), accent="\u20d7")),
    case("20-equation-array", "system[[x+y=1],[x-y=0]]", n("delimiter", n("equationArray", row("x", "+", "y", "=", 1), row("x", "-", "y", "=", 0)), begin="{", end="")),
]

payload = {
    "fixtureVersion": "m0-word-candidates-1",
    "scope": "Fixed candidates for Word COM discovery; no natural-language parser claim.",
    "cases": cases,
}
out = Path(__file__).with_name("candidates.json")
out.write_text(json.dumps(payload, ensure_ascii=True, indent=2) + "\n", encoding="utf-8")
print(f"Wrote {len(cases)} fixed candidates to {out}")
