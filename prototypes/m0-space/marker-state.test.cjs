const test = require("node:test");
const assert = require("node:assert/strict");
const m = require("./marker-state.js");
const edit = (state, raw) => m.transition(state, { type: "edit", raw });
function flush(state) { for (const task of [...state.pending]) state = m.transition(state, { type: "commit", task }); return state; }

test("default lc[ ... ] waits for full closure and preserves prose outside replacement", () => {
  let state = edit(m.create(), "Ta có lc[x^2");
  assert.equal(state.pending.length, 0);
  assert.equal(state.issues[0].code, "UNCLOSED_MARKER");
  state = flush(edit(state, "Ta có lc[x^2] và tiếp tục viết."));
  assert.equal(state.equations.length, 1);
  assert.equal(state.equations[0].selected.id, "power");
  assert.deepEqual(m.parts(state).filter((item) => item.kind === "text").map((item) => item.text), ["Ta có ", " và tiếp tục viết."]);
  assert.equal(state.raw, "Ta có lc[x^2] và tiếp tục viết.");
});
test("multi-character close marker must finish before any task is created", () => {
  let state = m.transition(m.create(), { type: "config", config: { open: "<<", close: ">>" } });
  state = edit(state, "<<x^2>");
  assert.equal(state.pending.length, 0);
  state = flush(edit(state, "<<x^2>>"));
  assert.equal(state.equations[0].originalReplacement, "<<x^2>>");
});
test("literal special characters are not treated as regex syntax", () => {
  let state = m.transition(m.create(), { type: "config", config: { open: "$(", close: ")$" } });
  state = flush(edit(state, "A $(1 trên 2)$ B"));
  assert.equal(state.equations[0].selected.id, "half");
});
test("invalid configs preserve active config and invalidate queued tasks", () => {
  const invalid = [{ open: "", close: "]" }, { open: "#", close: "#" }, { open: "<", close: "<<" }, { open: "a\nb", close: "c" }, { open: "a", close: "\\" }];
  for (const config of invalid) {
    let state = edit(m.create(), "lc[x^2]"); const oldTask = state.pending[0];
    state = m.transition(state, { type: "config", config });
    assert.ok(state.configError);
    assert.deepEqual(state.config, { open: "lc[", close: "]" });
    assert.equal(state.pending.length, 0);
    state = m.transition(state, { type: "commit", task: oldTask });
    assert.equal(state.equations.length, 0);
  }
});
test("changing delimiter config invalidates pending task and does not auto scan on its own", () => {
  let state = edit(m.create(), "lc[x^2]"); const task = state.pending[0];
  state = m.transition(state, { type: "config", config: { open: "<<", close: ">>" } });
  state = m.transition(state, { type: "commit", task });
  assert.equal(state.equations.length, 0);
  assert.equal(state.pending.length, 0);
  assert.match(state.log.at(-1).message, /Từ chối/);
});
test("editing before a pending region rejects the old offset/revision", () => {
  let state = edit(m.create(), "lc[x^2]"); const task = state.pending[0];
  state = edit(state, "A lc[x^2]");
  state = m.transition(state, { type: "commit", task });
  assert.equal(state.equations.length, 0);
  state = flush(state);
  assert.deepEqual(state.equations[0].span, [2, 9]);
});
test("nested region rejects the whole outer region and never converts the child", () => {
  const state = flush(edit(m.create(), "lc[x^2 + lc[1 trên 2]]"));
  assert.equal(state.equations.length, 0);
  assert.equal(state.issues[0].code, "NESTED_MARKER_UNSUPPORTED");
  assert.deepEqual(state.issues[0].span, [0, state.raw.length]);
});
test("backslash before either marker refuses conversion and preserves source", () => {
  for (const raw of ["\\lc[x^2]", "lc[x^2\\]"]) {
    const state = flush(edit(m.create(), raw));
    assert.equal(state.equations.length, 0);
    assert.equal(state.issues[0].code, "MARKER_ESCAPE_UNSUPPORTED");
    assert.equal(state.raw, raw);
  }
});
test("markers inside URL, email and common absolute paths are protected", () => {
  const samples = [["https://example.test/lc[x^2]", "PROTECTED_URL"], ["lc[x^2]@example.test", "PROTECTED_EMAIL"], ["C:\\bai\\lc[x^2].txt", "PROTECTED_PATH"], ["/tmp/lc[x^2]", "PROTECTED_PATH"]];
  for (const [raw, code] of samples) {
    const state = flush(edit(m.create(), raw));
    assert.equal(state.equations.length, 0);
    assert.equal(state.issues[0].code, code);
  }
});
test("empty, multiline and out-of-fixture content never fabricate a result", () => {
  for (const [raw, code] of [["lc[ ]", "EMPTY_MARKED_REGION"], ["lc[x^2\n]", "UNSUPPORTED_MULTILINE_EXPRESSION"], ["lc[x^3]", "OUTSIDE_FIXTURES"]]) {
    const state = flush(edit(m.create(), raw));
    assert.equal(state.equations.length, 0);
    assert.equal(state.issues[0].code, code);
  }
});
test("repair stays behind explicit choice, keeping direct first", () => {
  let state = flush(edit(m.create(), "lc[căn x cộng 1]"));
  assert.equal(state.equations.length, 0);
  const task = state.suggestions[0];
  assert.deepEqual(task.result.candidates.map((item) => item.kind), ["direct", "repair"]);
  state = m.transition(state, { type: "confirm", taskId: task.id, candidateId: "root-sum" });
  assert.equal(state.equations[0].selected.kind, "repair");
  assert.equal(state.equations[0].candidates.length, 2);
});
test("true ambiguity cannot auto-convert and old manual choice is invalid after edit", () => {
  let state = flush(edit(m.create(), "lc[2/3x]")); const task = state.suggestions[0];
  assert.equal(state.equations.length, 0);
  state = edit(state, "lc[2/3x] B");
  state = m.transition(state, { type: "confirm", taskId: task.id, candidateId: "implicit-denominator" });
  assert.equal(state.equations.length, 0);
});
test("restore includes both wrappers and whitespace, with no reconversion on restore or unrelated edit", () => {
  let state = flush(edit(m.create(), "A lc[ x^2 ] B"));
  state = m.transition(state, { type: "restore", id: state.equations[0].id });
  assert.equal(state.equations.length, 0);
  assert.equal(m.parts(state)[0].text, "A lc[ x^2 ] B");
  assert.equal(state.pending.length, 0);
  state = flush(edit(state, state.raw + " C"));
  assert.equal(state.equations.length, 0);
  state = m.transition(state, { type: "analyze" });
  state = flush(state);
  assert.equal(state.equations.length, 1, "explicit analyze can reconsider restored region");
});
test("editing restored content removes its suppression and can create a fresh conversion", () => {
  let state = flush(edit(m.create(), "lc[x^2]"));
  state = m.transition(state, { type: "restore", id: state.equations[0].id });
  state = flush(edit(state, "lc[1 trên 2]"));
  assert.equal(state.equations[0].selected.id, "half");
});
test("text edits before an equation remap the overlay; restore still uses the exact wrapper", () => {
  let state = flush(edit(m.create(), "lc[x^2] B"));
  state = edit(state, "A lc[x^2] B");
  assert.deepEqual(state.equations[0].span, [2, 9]);
  state = m.transition(state, { type: "restore", id: state.equations[0].id });
  assert.equal(state.raw, "A lc[x^2] B");
});
test("two independent regions convert independently and restoring one keeps the other", () => {
  let state = flush(edit(m.create(), "lc[x^2] và lc[1 trên 2]"));
  assert.equal(state.equations.length, 2);
  state = m.transition(state, { type: "restore", id: state.equations[0].id });
  assert.equal(state.equations.length, 1);
  assert.equal(state.equations[0].selected.id, "half");
});
test("composition blocks pending commits until its end and never uses the stale task", () => {
  let state = edit(m.create(), "lc[x^2]"); const task = state.pending[0];
  state = m.transition(state, { type: "composition-start" });
  state = m.transition(state, { type: "commit", task });
  assert.equal(state.equations.length, 0);
  state = edit(state, "A lc[x^2]");
  assert.equal(state.pending.length, 0);
  state = m.transition(state, { type: "composition-end" });
  state = flush(state);
  assert.equal(state.equations.length, 1);
});
test("Undo of conversion restores raw wrapper and does not immediately replay conversion", () => {
  let state = flush(edit(m.create(), "lc[x^2]"));
  state = m.transition(state, { type: "undo" });
  assert.equal(state.equations.length, 0);
  assert.equal(state.raw, "lc[x^2]");
  assert.equal(state.pending.length, 0);
});
test("pure transitions do not mutate snapshots already rendered", () => {
  const state = edit(m.create(), "lc[x^2]"); const before = structuredClone(state);
  flush(state);
  assert.deepEqual(state, before);
});
