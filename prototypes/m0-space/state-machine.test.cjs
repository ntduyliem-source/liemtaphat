const test = require("node:test");
const assert = require("node:assert/strict");
const { create, transition, view, samples, transcript } = require("./state-machine.js");

function run(policy, sample) {
  return sample.steps.reduce((state, token) => transition(state, token === " " ? { type: "space" } : { type: "insert", text: token }), create(policy));
}
function setSource(state, source, caret = source.length) {
  return transition(state, { type: "replace", source, caret });
}

test("A retains all of x mũ 2 cộng 1 through every Space and commits one equation on finish", () => {
  let state = run("retain", samples[0]);
  assert.equal(state.source, "x mũ 2 cộng 1 ");
  assert.equal(state.doc.length, 0);
  assert.equal(view(state).candidates[0].id, "power-sum");
  state = transition(state, { type: "finish" });
  assert.equal(state.doc.filter((part) => part.kind === "equation").length, 1);
  assert.equal(state.doc[0].candidate.id, "power-sum");
  assert.equal(transcript(state), "x mũ 2 cộng 1 ");
  assert.equal(state.caret, 0);
  assert.equal(state.phase, "text-after-equation");
});

test("B reproduces the split: x² is committed, cộng 1 remains plain source", () => {
  const state = run("commit", samples[0]);
  assert.equal(state.doc[0].candidate.id, "power");
  assert.equal(state.source, "cộng 1 ");
  assert.equal(view(state).status, "unsupported");
  assert.equal(transcript(state), "x mũ 2 cộng 1 ");
});

test("both policies preserve exact source for all sample steps", () => {
  for (const policy of ["retain", "commit"]) for (const sample of samples) {
    let state = create(policy);
    let source = "";
    for (const token of sample.steps) {
      source += token;
      state = transition(state, token === " " ? { type: "space" } : { type: "insert", text: token });
      assert.equal(transcript(state), source, `${policy}: ${JSON.stringify(source)}`);
    }
  }
});

test("1 trên is incomplete: Space and finish cannot silently fabricate a denominator", () => {
  for (const policy of ["retain", "commit"]) {
    let state = setSource(create(policy), "1 trên");
    state = transition(state, { type: "space" });
    state = transition(state, { type: "finish" });
    assert.equal(state.doc.length, 0);
    assert.equal(state.source, "1 trên ");
    assert.equal(view(state).status, "incomplete");
  }
});

test("fraction sample produces the same half candidate under both policies", () => {
  let retained = run("retain", samples[1]);
  const committed = run("commit", samples[1]);
  retained = transition(retained, { type: "finish" });
  assert.deepEqual(retained.doc, committed.doc);
  assert.equal(retained.doc[0].candidate.id, "half");
});

test("ambiguous source never commits automatically; a current explicit choice can finish", () => {
  let state = run("retain", samples[3]);
  assert.equal(view(state).status, "ambiguous");
  state = transition(state, { type: "finish" });
  assert.equal(state.doc.length, 0);
  state = transition(state, { type: "choose", id: "implicit-denominator" });
  state = transition(state, { type: "finish" });
  assert.equal(state.doc[0].candidate.id, "implicit-denominator");
});

test("B refuses ambiguous text supplied as one full source before Space", () => {
  let state = setSource(create("commit"), "2/3x");
  state = transition(state, { type: "space" });
  assert.equal(state.doc.length, 0);
  assert.equal(view(state).status, "ambiguous");
});

test("B commits root too early when entered step by step", () => {
  const state = run("commit", samples[2]);
  assert.equal(state.doc[0].candidate.id, "root");
  assert.equal(state.source, "cộng 1 ");
});

test("editing a source with a selected repair invalidates the old candidate", () => {
  let state = setSource(create("retain"), "căn x cộng 1");
  state = transition(state, { type: "choose", id: "root-sum" });
  state = transition(state, { type: "backspace" });
  assert.equal(state.selectedCandidate, null);
  state = transition(state, { type: "finish" });
  assert.equal(state.doc.length, 0);
  assert.equal(state.source, "căn x cộng ");
});

test("Undo of immediate commit restores pre-Space text and lets typing continue", () => {
  let state = setSource(create("commit"), "x mũ 2");
  const before = structuredClone(state);
  state = transition(state, { type: "space" });
  assert.equal(state.doc[0].candidate.id, "power");
  state = transition(state, { type: "undo" });
  assert.equal(state.source, before.source);
  assert.equal(state.caret, before.caret);
  assert.deepEqual(state.doc, before.doc);
  state = transition(state, { type: "insert", text: " cộng 1" });
  assert.equal(view(state).candidates[0].id, "power-sum");
});

test("Undo of finish restores selected repair, exact whitespace and caret", () => {
  let state = setSource(create("retain"), "  căn x cộng 1  ");
  state = transition(state, { type: "choose", id: "root-sum" });
  const before = structuredClone(state);
  state = transition(state, { type: "finish" });
  assert.equal(transcript(state), before.source);
  state = transition(state, { type: "undo" });
  assert.equal(state.source, before.source);
  assert.equal(state.caret, before.caret);
  assert.equal(state.selectedCandidate, "root-sum");
  assert.equal(state.doc.length, 0);
});

test("Space in the middle cannot commit B even when fixture remains valid", () => {
  let state = setSource(create("commit"), "x mũ 2", 1);
  state = transition(state, { type: "space" });
  assert.equal(state.source, "x  mũ 2");
  assert.equal(view(state).status, "ready");
  assert.equal(state.doc.length, 0);
  assert.equal(state.caret, 2);
});

test("Backspace and Undo preserve edit location inside the source", () => {
  let state = setSource(create("retain"), "x mũ 2 cộng 1", 6);
  state = transition(state, { type: "backspace" });
  assert.equal(state.source, "x mũ  cộng 1");
  assert.equal(state.caret, 5);
  state = transition(state, { type: "undo" });
  assert.equal(state.source, "x mũ 2 cộng 1");
  assert.equal(state.caret, 6);
});

test("Esc preserves exact input as plain text; Undo restores the source session", () => {
  let state = setSource(create("retain"), "  x mũ 2  ");
  state = transition(state, { type: "escape" });
  assert.deepEqual(state.doc, [{ kind: "text", text: "  x mũ 2  " }]);
  assert.equal(state.source, "");
  state = transition(state, { type: "undo" });
  assert.equal(state.source, "  x mũ 2  ");
  assert.equal(state.doc.length, 0);
});

test("DOM composition guard blocks commit actions and preview until composition ends", () => {
  let state = setSource(create("commit"), "x mũ 2");
  state = transition(state, { type: "composition-start" });
  for (const type of ["space", "finish", "escape", "backspace", "undo"]) {
    state = transition(state, { type });
    assert.equal(state.source, "x mũ 2");
    assert.equal(state.doc.length, 0);
    assert.equal(view(state).status, "composing");
  }
  state = transition(state, { type: "replace", source: "x mũ 2 ", caret: 7 });
  state = transition(state, { type: "composition-end" });
  assert.equal(view(state).status, "ready");
  assert.equal(state.doc.length, 0, "composition-end is not a commit trigger");
});

test("unsupported user input stays text and is never interpreted as markup", () => {
  const source = '<img src=x onerror="alert(1)">';
  let state = setSource(create("commit"), source);
  state = transition(state, { type: "space" });
  assert.equal(view(state).status, "unsupported");
  assert.equal(state.doc.length, 0);
  assert.equal(state.source, source + " ");
});

test("transitions do not mutate previously rendered states", () => {
  const state = setSource(create("commit"), "x mũ 2");
  const before = structuredClone(state);
  transition(state, { type: "space" });
  assert.deepEqual(state, before);
});

test("replacing a selected range with Space is one undoable action", () => {
  let state = setSource(create("retain"), "x mũ 2 cộng 1", 6);
  state = transition(state, { type: "space", rangeEnd: 13 });
  assert.equal(state.source, "x mũ 2 ");
  state = transition(state, { type: "undo" });
  assert.equal(state.source, "x mũ 2 cộng 1");
  assert.equal(state.caret, 6);
});

test("Backspace deletes the selected source range without removing the preceding character", () => {
  let state = setSource(create("retain"), "x mũ 2 cộng 1", 6);
  state = transition(state, { type: "backspace", rangeEnd: 13 });
  assert.equal(state.source, "x mũ 2");
  assert.equal(state.caret, 6);
});

test("root scope keeps direct first and labels enlargement as repair, never automatic", () => {
  for (const policy of ["retain", "commit"]) {
    let state = setSource(create(policy), "căn x cộng 1");
    state = transition(state, { type: "space" });
    assert.equal(view(state).status, "review");
    assert.deepEqual(view(state).candidates.map((item) => [item.id, item.kind]), [["root-plus", "direct"], ["root-sum", "repair"]]);
    state = transition(state, { type: "finish" });
    assert.equal(state.doc.length, 0, "experiment requires explicit review, not automatic repair");
    state = transition(state, { type: "choose", id: "root-plus" });
    state = transition(state, { type: "finish" });
    assert.equal(state.doc[0].candidate.id, "root-plus");
  }
});
