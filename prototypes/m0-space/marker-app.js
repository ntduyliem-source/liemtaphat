"use strict";
const markerEngine = LocusMarkers;
let markerState = markerEngine.create();
let heldTasks = [];
let markerStep = 0;
let markerSteps = [];
let openFx = new Set();
const byId = (id) => document.getElementById(id);
const issueLabels = { UNCLOSED_MARKER: "Chưa đóng dấu: đang chờ nhập tiếp.", NESTED_MARKER_UNSUPPORTED: "Vùng lồng chưa hỗ trợ: giữ nguyên toàn vùng.", MARKER_ESCAPE_UNSUPPORTED: "Escape chưa hỗ trợ: giữ nguyên dấu và backslash.", PROTECTED_URL: "Giữ nguyên URL, không lấy marker bên trong.", PROTECTED_EMAIL: "Giữ nguyên email, không lấy marker bên trong.", PROTECTED_PATH: "Giữ nguyên đường dẫn, không lấy marker bên trong.", EMPTY_MARKED_REGION: "Vùng rỗng: không chuyển.", UNSUPPORTED_MULTILINE_EXPRESSION: "Vùng nhiều dòng chưa hỗ trợ.", OUTSIDE_FIXTURES: "Nội dung chưa có trong bộ mẫu demo: giữ nguyên nguồn." };
function mText(text, className) { const node = document.createElement("span"); node.textContent = text; if (className) node.className = className; return node; }
function mMath(candidate) {
  const node = document.createElement("span"); node.className = "committed";
  // Candidate markup is from the local fixture table, never from source input.
  node.innerHTML = '<math xmlns="http://www.w3.org/1998/Math/MathML" aria-label="' + candidate.label + '">' + candidate.math + "</math>";
  return node;
}
function mButton(label, onClick) { const button = document.createElement("button"); button.textContent = label; button.addEventListener("click", onClick); return button; }
function markerDispatch(action, flush = true) {
  markerState = markerEngine.transition(markerState, action);
  if (markerState.pending.length) {
    if (byId("hold-commits").checked) {
      const ids = new Set(heldTasks.map((task) => task.id));
      heldTasks.push(...markerState.pending.filter((task) => !ids.has(task.id)));
    } else if (flush) {
      for (const task of [...markerState.pending]) markerState = markerEngine.transition(markerState, { type: "commit", task });
    }
  }
  renderMarkers();
}
function renderMarkers() {
  if (byId("marker-source").value !== markerState.raw) byId("marker-source").value = markerState.raw;
  byId("config-state").textContent = markerState.configError || `Đang dùng ${markerState.config.open}…${markerState.config.close} · phiên cấu hình ${markerState.configRevision}`;
  byId("config-state").className = markerState.configError ? "error" : "";
  byId("marker-composition").checked = markerState.composing;
  byId("source-state").textContent = `Nguồn ${markerState.raw.length} UTF-16 · phiên nguồn ${markerState.sourceRevision} · ${heldTasks.length} tác vụ được giữ${markerState.composing ? " · đang ghép ký tự" : ""}`;
  byId("flush-tasks").disabled = !heldTasks.length;
  const preview = byId("marker-preview"); preview.replaceChildren();
  if (!markerState.raw) preview.append(mText("Kết quả sẽ xuất hiện khi bạn gõ.", "empty"));
  else markerEngine.parts(markerState).forEach((part) => preview.append(part.kind === "text" ? mText(part.text, "plain") : mMath(part.selected)));
  byId("marker-status").textContent = markerState.composing ? "Đang ghép ký tự DOM: không nhận diện hoặc chuyển." : `${markerState.equations.length} công thức đã dựng mô phỏng · ${markerState.suggestions.length} vùng cần chọn · ${markerState.suppressed.length} vùng đang giữ nguồn sau restore/Undo.`;
  byId("marker-issues").replaceChildren(...markerState.issues.map((issue) => mText(issueLabels[issue.code] || issue.code, "issue")));
  const choices = byId("marker-choices"); choices.replaceChildren();
  markerState.suggestions.forEach((task) => {
    const card = document.createElement("div"); card.className = "marker-card review";
    card.append(mText(task.originalReplacement), mButton("fx · Xem phương án", () => { openFx.has(task.id) ? openFx.delete(task.id) : openFx.add(task.id); renderMarkers(); }));
    const direct = task.result.candidates.find((candidate) => candidate.kind === "direct");
    if (direct) { const note = document.createElement("p"); note.textContent = "Theo chuỗi đã gõ: " + direct.label + ". Chưa tự chuyển."; card.append(note); }
    if (openFx.has(task.id)) task.result.candidates.forEach((candidate) => {
      const prefix = { direct: "Chọn theo nguồn: ", interpretation: "Chọn cách hiểu khác: ", repair: "Chọn sửa phạm vi: " }[candidate.kind];
      card.append(mButton(prefix + candidate.label, () => markerDispatch({ type: "confirm", taskId: task.id, candidateId: candidate.id })));
    });
    choices.append(card);
  });
  const equations = byId("marker-equations"); equations.replaceChildren();
  markerState.equations.forEach((equation) => {
    const card = document.createElement("div"); card.className = "marker-card";
    card.append(mText(`${equation.selected.label} ← ${equation.originalReplacement}`), mButton("Khôi phục nguồn", () => markerDispatch({ type: "restore", id: equation.id }, false)));
    equations.append(card);
  });
  byId("marker-state").textContent = JSON.stringify({ sourceRevision: markerState.sourceRevision, configRevision: markerState.configRevision, raw: markerState.raw, config: markerState.config, pending: markerState.pending, equations: markerState.equations, suppressed: markerState.suppressed }, null, 2);
  byId("marker-log").replaceChildren(...markerState.log.slice().reverse().map((event) => { const row = document.createElement("li"); row.textContent = `#${event.sequence} · ${event.action} · ${event.message}`; return row; }));
  byId("marker-progress").textContent = `Bước ${markerStep}/${markerSteps.length}`;
  byId("marker-next").disabled = markerStep >= markerSteps.length;
  byId("marker-run").disabled = markerStep >= markerSteps.length;
  byId("marker-steps").replaceChildren(...markerSteps.map((step, index) => mText(step, "step " + (index < markerStep ? "done" : index === markerStep ? "next" : ""))));
}
function resetMarkerSample() {
  const config = { ...markerState.config }, configRevision = markerState.configRevision;
  markerState = markerEngine.create(); markerState.config = config; markerState.configRevision = configRevision;
  heldTasks = []; markerStep = 0; openFx = new Set();
  const { open, close } = config;
  markerSteps = {
    prose: ["Ta có ", open, "x^2", close, " và tiếp tục viết."],
    unclosed: ["Kết quả ", open, "1 trên 2"],
    repair: ["Ta có ", open, "căn x cộng 1", close, "."],
    ambiguous: [open, "2/3x", close],
    nested: [open, "x^2 + ", open, "1 trên 2", close, close],
    url: ["Xem https://example.test/", open, "x^2", close],
    path: ["C:\\bai\\", open, "x^2", close, ".txt"],
    escape: ["\\", open, "x^2", close],
  }[byId("marker-sample").value];
  renderMarkers();
}
function nextMarkerStep() {
  if (markerStep >= markerSteps.length) return;
  markerDispatch({ type: "edit", raw: markerState.raw + markerSteps[markerStep++] });
}
byId("apply-config").addEventListener("click", () => markerDispatch({ type: "config", config: { open: byId("open-marker").value, close: byId("close-marker").value } }, false));
byId("marker-source").addEventListener("input", (event) => markerDispatch({ type: "edit", raw: event.target.value }));
byId("marker-source").addEventListener("compositionstart", () => markerDispatch({ type: "composition-start" }, false));
byId("marker-source").addEventListener("compositionend", (event) => { markerDispatch({ type: "edit", raw: event.target.value }, false); markerDispatch({ type: "composition-end" }); });
byId("marker-composition").addEventListener("change", (event) => markerDispatch({ type: event.target.checked ? "composition-start" : "composition-end" }));
byId("marker-undo").addEventListener("click", () => markerDispatch({ type: "undo" }, false));
byId("marker-analyze").addEventListener("click", () => markerDispatch({ type: "analyze" }));
byId("flush-tasks").addEventListener("click", () => { const tasks = heldTasks; heldTasks = []; for (const task of tasks) markerState = markerEngine.transition(markerState, { type: "commit", task }); renderMarkers(); });
byId("marker-next").addEventListener("click", nextMarkerStep);
byId("marker-run").addEventListener("click", () => { while (markerStep < markerSteps.length) nextMarkerStep(); });
byId("marker-reset").addEventListener("click", resetMarkerSample);
byId("marker-sample").addEventListener("change", resetMarkerSample);
resetMarkerSample();
