#!/usr/bin/env python3
"""Validate M0 fixture data only; this is NOT a parser or Word test runner."""

from __future__ import annotations

import argparse
import copy
from collections import Counter
import json
from pathlib import Path
import re
import sys


class InvalidCorpus(ValueError):
    pass


def unique_object(pairs):
    result = {}
    for key, value in pairs:
        if key in result:
            raise InvalidCorpus(f"duplicate JSON object key: {key}")
        result[key] = value
    return result


def reject_nonfinite(value):
    raise InvalidCorpus(f"nonstandard JSON numeric constant: {value}")


def require(condition, location, message):
    if not condition:
        raise InvalidCorpus(f"{location}: {message}")


def object_keys(value, required, optional, location):
    require(isinstance(value, dict), location, "expected an object")
    require(set(required) <= set(value), location, f"missing keys: {set(required) - set(value)}")
    require(set(value) <= set(required) | set(optional), location,
            f"unknown keys: {set(value) - set(required) - set(optional)}")


def utf16_boundaries(raw):
    require(isinstance(raw, str), "source.raw", "expected a string")
    positions = {0: 0}
    offset = 0
    for index, char in enumerate(raw):
        require(not 0xD800 <= ord(char) <= 0xDFFF, "source.raw", "unpaired surrogate is forbidden")
        offset += 2 if ord(char) > 0xFFFF else 1
        positions[offset] = index + 1
    return positions


def check_span(span, boundaries, location):
    require(isinstance(span, list) and len(span) == 2, location, "expected [start, end)")
    require(all(type(value) is int for value in span), location, "offsets must be integers, not booleans")
    start, end = span
    require(0 <= start <= end, location, "negative or reversed span")
    require(start in boundaries and end in boundaries, location,
            "span outside source or splitting a UTF-16 surrogate pair")


def check_diagnostics(items, boundaries, location):
    require(isinstance(items, list), location, "expected diagnostics array")
    for index, item in enumerate(items):
        loc = f"{location}[{index}]"
        object_keys(item, {"code", "severity", "span", "message"}, set(), loc)
        require(isinstance(item["code"], str) and re.fullmatch(r"[A-Z][A-Z0-9_]+", item["code"]), loc, "invalid diagnostic code")
        require(item["severity"] in {"info", "warning", "error"}, loc, "invalid severity")
        require(isinstance(item["message"], str) and bool(item["message"].strip()), loc, "empty diagnostic message")
        check_span(item["span"], boundaries, loc + ".span")


def check_ast(node, location, depth=0):
    require(depth <= 64, location, "fixture AST exceeds depth 64")
    require(isinstance(node, dict), location, "AST node must be an object")
    kind = node.get("type")
    fields = {
        "Number": {"type", "value"}, "Symbol": {"type", "name"},
        "Unary": {"type", "operator", "operand"},
        "Binary": {"type", "operator", "left", "right"},
        "Power": {"type", "base", "exponent"}, "Sqrt": {"type", "radicand"},
        "Relation": {"type", "operator", "left", "right"},
    }
    require(kind in fields, location, f"unknown AST node type: {kind!r}")
    object_keys(node, fields[kind], set(), location)
    if kind == "Number":
        require(isinstance(node["value"], str) and re.fullmatch(r"(?:0|[1-9][0-9]*)(?:\.[0-9]+)?", node["value"]), location, "Number must be a nonnegative canonical decimal string")
    elif kind == "Symbol":
        require(isinstance(node["name"], str) and re.fullmatch(r"[A-Za-z]", node["name"]), location, "v0 Symbol must be a single ASCII letter")
    else:
        operator_sets = {"Unary": {"plus", "minus"}, "Binary": {"add", "subtract", "multiply", "divide"}, "Relation": {"eq", "lt", "gt", "le", "ge"}}
        if kind in operator_sets:
            require(node["operator"] in operator_sets[kind], location, "invalid operator")
        children = {"Unary": ["operand"], "Binary": ["left", "right"], "Relation": ["left", "right"], "Power": ["base", "exponent"], "Sqrt": ["radicand"]}[kind]
        for child in children:
            check_ast(node[child], location + "." + child, depth + 1)


def validate_case(case, global_ids):
    loc = case.get("id", "<case>") if isinstance(case, dict) else "<case>"
    object_keys(case, {"id", "title", "status", "expectationBasis", "personas", "tags", "source", "context", "expected", "pendingDecisions", "notes"}, set(), loc)
    require(re.fullmatch(r"M0-[0-9]{3}", case["id"]) is not None, loc, "invalid case ID")
    require(case["id"] not in global_ids, loc, "duplicate case ID")
    global_ids.add(case["id"])
    require(isinstance(case["title"], str) and case["title"].strip(), loc, "title is required")
    require(case["status"] in {"specified", "pending"}, loc, "invalid status")
    require(case["expectationBasis"] in {"product_requirement", "grammar_baseline", "open_decision"}, loc, "invalid expectationBasis")
    require(isinstance(case["personas"], list) and bool(case["personas"]), loc, "personas required")
    require(len(case["personas"]) == len(set(case["personas"])) and set(case["personas"]) <= {"teacher", "student", "technical_writer"}, loc, "invalid/duplicate persona")
    require(isinstance(case["tags"], list) and all(isinstance(tag, str) and tag for tag in case["tags"]), loc, "invalid tags")
    require(isinstance(case["notes"], str), loc, "notes must be a string")
    object_keys(case["source"], {"raw"}, set(), loc + ".source")
    raw = case["source"]["raw"]
    boundaries = utf16_boundaries(raw)
    ctx = case["context"]
    object_keys(ctx, {"channel", "inputMode", "domain", "autoMode", "inputState", "focus", "documentState", "regionClosed", "eventOrigin", "action"}, {"delimiters"}, loc + ".context")
    enums = {
        "channel": {"desktop", "word"}, "inputMode": {"explicit", "passive", "marked"},
        "domain": {"math", "physics", "chemistry"}, "autoMode": {"suggest", "marked", "space"},
        "inputState": {"stable", "composing", "unknown"},
        "focus": {"editor", "find", "ribbon", "dialog", "other_window", "unknown"},
        "documentState": {"current", "stale_source", "shifted_range", "changed_selection", "closed", "settings_changed", "connector_reloaded", "read_only", "protected", "native_changed", "metadata_missing", "metadata_future", "duplicate_metadata_id", "parser_upgraded", "unsupported_story"},
        "eventOrigin": {"user", "locus", "restore"},
        "action": {"analyze", "fx_open", "restore_source", "reopen_snapshot"},
    }
    for field, allowed in enums.items():
        require(ctx[field] in allowed, loc, f"invalid context.{field}")
    require(type(ctx["regionClosed"]) is bool, loc, "regionClosed must be boolean")
    if "delimiters" in ctx:
        object_keys(ctx["delimiters"], {"open", "close"}, set(), loc + ".context.delimiters")
        require(all(isinstance(value, str) for value in ctx["delimiters"].values()), loc, "delimiters must be strings; invalid configs remain valid negative fixtures")

    exp = case["expected"]
    object_keys(exp, {"detection", "regions", "diagnostics", "auto", "presentation", "invariants"}, set(), loc + ".expected")
    require(exp["detection"] in {"accept", "reject", "defer", "pending"}, loc, "invalid detection")
    require(exp["presentation"] in {"direct_only", "repair_notice", "candidate_list", "hidden", "pending"}, loc, "invalid presentation")
    require(isinstance(exp["invariants"], list) and "SOURCE_IMMUTABLE" in exp["invariants"], loc, "source immutability invariant required")
    require(all(isinstance(item, str) and item for item in exp["invariants"]), loc, "invalid invariants")
    check_diagnostics(exp["diagnostics"], boundaries, loc + ".expected.diagnostics")
    require(isinstance(exp["regions"], list), loc, "regions must be an array")
    seen_candidate_ids = set()
    all_candidates = []
    all_diagnostics = list(exp["diagnostics"])
    previous_region_end = -1
    for region_index, region in enumerate(exp["regions"]):
        rloc = f"{loc}.regions[{region_index}]"
        object_keys(region, {"id", "sourceSpan", "sourceText", "replacementSpan", "replacementText", "domain", "candidates", "diagnostics"}, set(), rloc)
        require(region["id"] == f"{loc}-r{region_index + 1}", rloc, "region ID must match order")
        require(region["domain"] == ctx["domain"], rloc, "region domain mismatch")
        for field, text_field in [("sourceSpan", "sourceText"), ("replacementSpan", "replacementText")]:
            check_span(region[field], boundaries, rloc + "." + field)
            start, end = region[field]
            require(start < end, rloc, "regions must not be empty")
            require(region[text_field] == raw[boundaries[start]:boundaries[end]], rloc, f"{text_field} does not exactly match raw UTF-16 span")
        source_start, source_end = region["sourceSpan"]
        replacement_start, replacement_end = region["replacementSpan"]
        require(replacement_start <= source_start <= source_end <= replacement_end, rloc, "replacement must enclose source content")
        require(replacement_start >= previous_region_end, rloc, "regions must be ordered and non-overlapping")
        previous_region_end = replacement_end
        check_diagnostics(region["diagnostics"], boundaries, rloc + ".diagnostics")
        all_diagnostics.extend(region["diagnostics"])
        candidates = region["candidates"]
        require(isinstance(candidates, list) and 1 <= len(candidates) <= 3, rloc, "a parsed region needs 1-3 candidates")
        direct_count = 0
        semantic_seen = set()
        for candidate_index, candidate in enumerate(candidates):
            cloc = f"{rloc}.candidates[{candidate_index}]"
            object_keys(candidate, {"id", "kind", "mathDocument", "diagnostics", "edits"}, set(), cloc)
            require(candidate["id"] == f"{region['id']}-c{candidate_index + 1}" and candidate["id"] not in seen_candidate_ids, cloc, "invalid/duplicate candidate ID")
            seen_candidate_ids.add(candidate["id"])
            require(candidate["kind"] in {"direct", "interpretation", "repair"}, cloc, "invalid candidate kind")
            if candidate["kind"] == "direct":
                direct_count += 1
                require(candidate_index == 0, cloc, "direct candidate must be first")
            if candidate["kind"] == "interpretation":
                require(direct_count == 1, cloc, "interpretation requires a preceding direct candidate")
            document = candidate["mathDocument"]
            object_keys(document, {"schemaVersion", "domain", "root"}, set(), cloc + ".mathDocument")
            require(document["schemaVersion"] == "math-document/0.1" and document["domain"] == "math", cloc, "unsupported MathDocument version/domain")
            require(region["domain"] == "math", cloc, "math AST cannot impersonate a future domain")
            check_ast(document["root"], cloc + ".root")
            semantic_key = json.dumps(document["root"], sort_keys=True)
            require(semantic_key not in semantic_seen, cloc, "duplicate semantic candidate would pad the list")
            semantic_seen.add(semantic_key)
            check_diagnostics(candidate["diagnostics"], boundaries, cloc + ".diagnostics")
            all_diagnostics.extend(candidate["diagnostics"])
            require(isinstance(candidate["edits"], list), cloc, "edits must be an array")
            require(bool(candidate["edits"]) == (candidate["kind"] == "repair"), cloc, "only repairs have nonempty edits")
            previous_edit_end = -1
            previous_edit_span = None
            for edit in candidate["edits"]:
                object_keys(edit, {"span", "replacement"}, set(), cloc + ".edit")
                check_span(edit["span"], boundaries, cloc + ".edit.span")
                a, b = edit["span"]
                require(source_start <= a <= b <= source_end, cloc, "repair edit outside source content")
                require(isinstance(edit["replacement"], str), cloc, "repair replacement must be a string")
                require(a >= previous_edit_end and edit["span"] != previous_edit_span, cloc, "repair edits must be ordered, disjoint and not duplicate insertions")
                require(a != b or bool(edit["replacement"]), cloc, "empty insertion is not a repair")
                previous_edit_end, previous_edit_span = b, edit["span"]
            all_candidates.append(candidate)
        require(direct_count <= 1, rloc, "more than one direct candidate")

    if exp["detection"] in {"reject", "defer", "pending"}:
        require(not exp["regions"], loc, "rejected/deferred/pending detection must not claim parsed regions")
    if exp["detection"] == "accept":
        require(bool(exp["regions"]), loc, "accepted analysis requires regions")
    if exp["presentation"] == "direct_only":
        require(bool(exp["regions"]) and all(region["candidates"][0]["kind"] == "direct" for region in exp["regions"]), loc, "direct presentation needs direct candidates")
    if exp["presentation"] == "repair_notice":
        require(bool(exp["regions"]) and all(candidate["kind"] == "repair" for candidate in all_candidates), loc, "repair-only notice cannot conceal a direct parse")
    if exp["presentation"] == "candidate_list":
        require(ctx["action"] == "fx_open" and bool(exp["regions"]), loc, "candidate list requires fx action in this corpus")
    auto = exp["auto"]
    object_keys(auto, {"contentEligibility", "writeExpectation", "reasons"}, set(), loc + ".auto")
    require(auto["contentEligibility"] in {"eligible", "blocked", "pending"}, loc, "invalid content eligibility")
    require(auto["writeExpectation"] in {"forbidden", "may_proceed_after_revalidation", "pending"}, loc, "invalid write expectation")
    require(isinstance(auto["reasons"], list) and all(isinstance(reason, str) and reason for reason in auto["reasons"]), loc, "invalid auto reasons")
    if auto["contentEligibility"] == "eligible":
        require(exp["detection"] == "accept" and bool(all_candidates), loc, "eligible content needs accepted candidates")
        require(all(len(region["candidates"]) == 1 and region["candidates"][0]["kind"] == "direct" for region in exp["regions"]), loc, "eligible content cannot contain alternatives or repairs")
        require(not any(diag["severity"] in {"warning", "error"} for diag in all_diagnostics), loc, "eligible content cannot contain warnings/errors")
    if auto["writeExpectation"] == "may_proceed_after_revalidation":
        require(case["status"] == "specified" and auto["contentEligibility"] == "eligible", loc, "auto requires specified eligible content")
        require(ctx["channel"] == "word" and ctx["autoMode"] == "marked" and ctx["inputMode"] == "marked", loc, "only marked Word fixtures can permit auto in v0")
        require(ctx["regionClosed"] and ctx["inputState"] == "stable" and ctx["focus"] == "editor" and ctx["documentState"] == "current" and ctx["eventOrigin"] == "user", loc, "unsafe context cannot permit auto")
        require(len(exp["regions"]) == 1 and not auto["reasons"], loc, "auto requires one target and no blockers")
        delimiters = ctx.get("delimiters", {"open": "lc[", "close": "]"})
        opening, closing = delimiters["open"], delimiters["close"]
        require(bool(opening) and bool(closing) and not opening.startswith(closing) and not closing.startswith(opening), loc, "auto cannot use invalid delimiter config")
        require("\n" not in opening + closing and "\r" not in opening + closing, loc, "delimiter cannot cross lines")
        region = exp["regions"][0]
        require(region["replacementText"] == opening + region["sourceText"] + closing, loc, "auto replacement must be exactly one marked region")
        require(opening not in region["sourceText"] and "\\" not in region["replacementText"], loc, "nested/escaped marker cannot permit auto")
    if auto["writeExpectation"] == "forbidden":
        require(bool(auto["reasons"]), loc, "forbidden auto needs at least one reason")
    require(isinstance(case["pendingDecisions"], list), loc, "pendingDecisions must be an array")
    if case["status"] == "pending":
        require(case["expectationBasis"] == "open_decision" and bool(case["pendingDecisions"]), loc, "pending case needs decision provenance")
        require(exp["detection"] == "pending" and not exp["regions"] and auto["writeExpectation"] != "may_proceed_after_revalidation", loc, "pending behavior must not claim a chosen AST or auto approval")
        require(exp["presentation"] in {"hidden", "pending"}, loc, "pending UX cannot claim a chosen presentation")
    else:
        require(not case["pendingDecisions"] and case["expectationBasis"] != "open_decision", loc, "specified case cannot leave its own expected behavior pending")


def validate_document(data):
    object_keys(data, {"schemaVersion", "grammarVersion", "contractVersion", "spanEncoding", "spanConvention", "status", "description", "cases"}, set(), "corpus")
    require(data["schemaVersion"] == "locus-corpus/0.1", "corpus", "unsupported schemaVersion")
    require(data["grammarVersion"] == "vi-math-m0-proposal-0.1", "corpus", "unexpected grammarVersion")
    require(data["contractVersion"] == "locus-core-contract/0.1", "corpus", "unexpected contractVersion")
    require(data["spanEncoding"] == "utf16-code-unit" and data["spanConvention"] == "half-open", "corpus", "wrong offset contract")
    require(data["status"] == "baseline_for_review", "corpus", "fixture status must not claim implementation success")
    require(isinstance(data["description"], str), "corpus", "description must be a string")
    require(isinstance(data["cases"], list) and len(data["cases"]) >= 50, "corpus", "M0 corpus requires at least 50 cases")
    errors = []
    ids = set()
    for case in data["cases"]:
        try:
            validate_case(case, ids)
        except (InvalidCorpus, TypeError, KeyError) as error:
            errors.append(str(error))
    if errors:
        raise InvalidCorpus("\n".join(errors))
    represented = set().union(*(set(case["personas"]) for case in data["cases"]))
    require(represented == {"teacher", "student", "technical_writer"}, "corpus", "all three user groups must be represented")
    return Counter(case["status"] for case in data["cases"])


def self_check(data):
    """Negative controls for the validator, not tests of any product parser."""
    accepted = next(i for i, case in enumerate(data["cases"]) if case["expected"]["regions"])
    emoji = next(i for i, case in enumerate(data["cases"]) if "😀" in case["source"]["raw"])
    repairing = next(i for i, case in enumerate(data["cases"]) if any(c["kind"] == "repair" for r in case["expected"]["regions"] for c in r["candidates"]))
    auto_allowed = next(i for i, case in enumerate(data["cases"]) if case["expected"]["auto"]["writeExpectation"] == "may_proceed_after_revalidation")
    pending = next(i for i, case in enumerate(data["cases"]) if case["status"] == "pending")

    def set_at(root, path, value):
        for key in path[:-1]:
            root = root[key]
        root[path[-1]] = value

    base_region = ["cases", accepted, "expected", "regions", 0]
    mutations = [
        ("UTF-16 surrogate split", ["cases", emoji, "expected", "regions", 0, "sourceSpan"], [1, 2]),
        ("out-of-bounds span", base_region + ["sourceSpan"], [0, 99999]),
        ("boolean offset", base_region + ["sourceSpan"], [False, 4]),
        ("source text mismatch", base_region + ["sourceText"], "not the captured source"),
        ("unknown AST", base_region + ["candidates", 0, "mathDocument", "root"], {"type": "Unknown"}),
        ("non-string Number", base_region + ["candidates", 0, "mathDocument", "root"], {"type": "Number", "value": 2.0}),
        ("four candidates", base_region + ["candidates"], copy.deepcopy(data["cases"][accepted]["expected"]["regions"][0]["candidates"]) * 4),
        ("repair marked eligible", ["cases", repairing, "expected", "auto", "contentEligibility"], "eligible"),
        ("stale focus permitting auto", ["cases", auto_allowed, "context", "focus"], "find"),
        ("invalid markers permitting auto", ["cases", auto_allowed, "context", "delimiters", "open"], ""),
        ("duplicate case ID", ["cases", 1, "id"], data["cases"][0]["id"]),
        ("pending case permitting auto", ["cases", pending, "expected", "auto", "writeExpectation"], "may_proceed_after_revalidation"),
        ("foreign-domain math candidate", base_region + ["domain"], "chemistry"),
        ("repair edits on direct", base_region + ["candidates", 0, "edits"], [{"span": [0, 0], "replacement": "("}]),
        ("ambiguous candidate with no direct", base_region + ["candidates", 0, "kind"], "interpretation"),
    ]
    for label, path, value in mutations:
        invalid = copy.deepcopy(data)
        set_at(invalid, path, value)
        try:
            validate_document(invalid)
        except InvalidCorpus:
            continue
        raise InvalidCorpus(f"validator negative control was not rejected: {label}")
    print(f"VALIDATOR NEGATIVE CONTROLS: {len(mutations)} intentionally invalid copies rejected; corpus unchanged.")


def main():
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8")
        sys.stderr.reconfigure(encoding="utf-8")
    default_path = Path(__file__).resolve().parents[2] / "corpus" / "m0" / "cases.json"
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("path", nargs="?", type=Path, default=default_path)
    parser.add_argument("--self-test", action="store_true", help="run in-memory negative controls for this validator only")
    args = parser.parse_args()
    try:
        data = json.loads(args.path.read_text(encoding="utf-8"), object_pairs_hook=unique_object, parse_constant=reject_nonfinite)
        counts = validate_document(data)
        if args.self_test:
            self_check(data)
    except (OSError, UnicodeError, json.JSONDecodeError, InvalidCorpus) as error:
        print(f"INVALID FIXTURE DATA\n{error}", file=sys.stderr)
        return 1
    print(f"VALID FIXTURE DATA: {len(data['cases'])} cases; {counts['specified']} specified; {counts['pending']} pending.")
    print("Validated schema, UTF-16 spans, AST shape, candidate kinds/order/count, and declared auto-policy invariants.")
    print("NOT RUN: parser, detector, renderer, production source mappings, Word, IME, or runtime roundtrip tests.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
