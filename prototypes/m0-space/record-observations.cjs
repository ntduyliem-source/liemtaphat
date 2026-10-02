const fs = require("node:fs");
const path = require("node:path");
const engine = require("./state-machine.js");
const observations = [];
for (const policy of ["retain", "commit"]) for (const sample of engine.samples) {
  let state = engine.create(policy);
  const trace = [];
  for (const token of sample.steps) {
    const action = token === " " ? { type: "space" } : { type: "insert", text: token };
    state = engine.transition(state, action);
    const result = engine.view(state);
    trace.push({ action, source: state.source, caretUtf16: state.caret, phase: state.phase, status: result.status, candidates: result.candidates.map(({ id, kind }) => ({ id, kind })), committed: state.doc, sourceTranscript: engine.transcript(state) });
  }
  observations.push({ policy, sample: sample.label, trace });
}
const report = { evidenceType: "synthetic-state-machine-trace", generatedAt: new Date().toISOString(), runtime: process.version, platform: process.platform, warning: "Browser UX model only. No Word, native equation, real IME, participant observation or production parser was exercised by this trace.", observations };
const target = path.join(__dirname, "observations.json");
fs.writeFileSync(target, JSON.stringify(report, null, 2) + "\n");
process.stdout.write(target + "\n");
