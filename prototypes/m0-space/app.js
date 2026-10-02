"use strict";
const engine = LocusSpace;
const policies = {
  retain: { letter: "A", title: "Giữ phiên nguồn", description: "Space cập nhật preview. Vẫn gõ tiếp cùng công thức; Enter mới chốt." },
  commit: { letter: "B", title: "Chốt ngay ở Space", description: "Khi có kết quả đơn nghĩa, Space chốt. Chữ tiếp theo ở ngoài công thức." },
};
const statusLabels = { empty: "CHƯA NHẬP", ready: "MỘT KẾT QUẢ", incomplete: "CHỜ NHẬP TIẾP", ambiguous: "NHIỀU CÁCH HIỂU", review: "CÓ GỢI Ý SỬA", unsupported: "NGOÀI BỘ MẪU", composing: "ĐANG GHÉP KÝ TỰ" };
const panels = {};
let sampleIndex = 0;
let stepIndex = 0;
let manuallyEdited = false;

function dispatch(policy, action, isManual = true) {
  panels[policy].state = engine.transition(panels[policy].state, action);
  if (isManual && !["caret", "composition-start", "composition-end"].includes(action.type)) manuallyEdited = true;
  renderPanel(policy, !["caret", "replace", "composition-start", "composition-end"].includes(action.type));
  renderProgress();
}

function mathNode(candidate, className) {
  const wrapper = document.createElement("span");
  wrapper.className = className;
  // Markup comes only from the fixed local fixture table, never from user source.
  wrapper.innerHTML = '<math xmlns="http://www.w3.org/1998/Math/MathML" aria-label="' + candidate.label + '">' + candidate.math + "</math>";
  return wrapper;
}
function textNode(text, className = "raw") {
  const node = document.createElement("span");
  node.className = className;
  node.textContent = text;
  return node;
}

function renderPanel(policy, restoreCaret = true) {
  const { node, state } = panels[policy];
  const sourceInput = node.querySelector("textarea");
  if (sourceInput.value !== state.source) sourceInput.value = state.source;
  if (restoreCaret && !state.composing && document.activeElement === sourceInput) sourceInput.setSelectionRange(state.caret, state.caret);
  const result = engine.view(state);
  const status = node.querySelector(".status");
  status.textContent = statusLabels[result.status];
  status.dataset.status = result.status;
  node.querySelector(".caret").textContent = "Con trỏ nguồn: " + state.caret + "/" + state.source.length + " (UTF-16)";
  const preview = node.querySelector(".preview");
  preview.replaceChildren();
  state.doc.forEach((part) => preview.append(part.kind === "text" ? textNode(part.text) : mathNode(part.candidate, "committed")));
  if (result.candidates.length) {
    const candidate = result.candidates.find((item) => item.id === state.selectedCandidate) || result.candidates[0];
    preview.append(mathNode(candidate, "live " + result.status));
  } else if (state.source) preview.append(textNode(state.source));
  if (!state.doc.length && !state.source) preview.append(textNode("Kết quả sẽ xuất hiện ở đây.", "empty"));
  else preview.append(textNode("", "cursor"));
  node.querySelector(".message").textContent = result.message + (result.candidates.length ? " Khung nền là preview của phiên; gạch chân là kết quả đã chốt mô phỏng." : "");
  const candidates = node.querySelector(".candidates");
  candidates.replaceChildren();
  if (["ambiguous", "review"].includes(result.status)) result.candidates.forEach((candidate) => {
    const button = document.createElement("button");
    button.type = "button";
    button.textContent = ({ direct: "Theo chuỗi đã gõ: ", repair: "Sửa phạm vi căn: ", interpretation: "Cách hiểu khác: " })[candidate.kind] + candidate.label;
    button.setAttribute("aria-pressed", String(state.selectedCandidate === candidate.id));
    button.addEventListener("click", () => dispatch(policy, { type: "choose", id: candidate.id }));
    candidates.append(button);
  });
  node.querySelector(".composition-toggle").checked = state.composing;
  node.querySelector(".debug").textContent = JSON.stringify({ policy, phase: state.phase, source: state.source, caretUtf16: state.caret, composingDOM: state.composing, selectedCandidate: state.selectedCandidate, document: state.doc, undoDepth: state.history.length }, null, 2);
  const log = node.querySelector(".log");
  log.replaceChildren();
  state.log.slice().reverse().forEach((event) => {
    const item = document.createElement("li");
    item.textContent = "#" + event.sequence + " · " + event.event + " · " + event.message + " [" + event.phase + "; con trỏ " + event.caret + "]";
    log.append(item);
  });
}

Object.entries(policies).forEach(([policy, config]) => {
  const node = document.querySelector("#panel").content.firstElementChild.cloneNode(true);
  node.dataset.policy = policy;
  node.querySelector(".policy-letter").textContent = config.letter;
  node.querySelector("h2").textContent = config.title;
  node.querySelector(".policy-description").textContent = config.description;
  panels[policy] = { node, state: engine.create(policy) };
  const input = node.querySelector("textarea");
  input.setAttribute("aria-label", "Nguồn nhập phương án " + config.letter);
  input.addEventListener("input", () => dispatch(policy, { type: "replace", source: input.value, caret: input.selectionStart }));
  input.addEventListener("select", () => { panels[policy].state = engine.transition(panels[policy].state, { type: "caret", caret: input.selectionStart }); });
  input.addEventListener("keyup", () => {
    if (!panels[policy].state.composing) dispatch(policy, { type: "caret", caret: input.selectionStart });
  });
  input.addEventListener("click", () => dispatch(policy, { type: "caret", caret: input.selectionStart }));
  input.addEventListener("compositionstart", () => dispatch(policy, { type: "composition-start" }));
  input.addEventListener("compositionend", () => {
    dispatch(policy, { type: "replace", source: input.value, caret: input.selectionStart });
    dispatch(policy, { type: "composition-end" });
  });
  input.addEventListener("keydown", (event) => {
    if (event.isComposing || panels[policy].state.composing || event.keyCode === 229) return;
    let type;
    if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === "z" && !event.shiftKey) type = "undo";
    else if (event.key === "Enter") type = "finish";
    else if (event.key === "Escape") type = "escape";
    else if (event.key === " " && !event.ctrlKey && !event.altKey && !event.metaKey) type = "space";
    if (!type) return;
    event.preventDefault();
    panels[policy].state = engine.transition(panels[policy].state, { type: "caret", caret: input.selectionStart });
    dispatch(policy, { type, rangeEnd: input.selectionEnd });
  });
  node.querySelectorAll("[data-action]").forEach((button) => button.addEventListener("click", () => {
    panels[policy].state = engine.transition(panels[policy].state, { type: "caret", caret: input.selectionStart });
    dispatch(policy, { type: button.dataset.action, rangeEnd: input.selectionEnd });
    input.focus();
    input.setSelectionRange(panels[policy].state.caret, panels[policy].state.caret);
  }));
  node.querySelector(".composition-toggle").addEventListener("change", (event) => dispatch(policy, { type: event.target.checked ? "composition-start" : "composition-end" }));
  document.querySelector("#comparison").append(node);
  renderPanel(policy);
});

const sampleSelect = document.querySelector("#sample");
engine.samples.forEach((sample, index) => {
  const option = document.createElement("option");
  option.value = index;
  option.textContent = sample.label;
  sampleSelect.append(option);
});
function reset() {
  sampleIndex = Number(sampleSelect.value);
  stepIndex = 0;
  manuallyEdited = false;
  Object.keys(panels).forEach((policy) => { panels[policy].state = engine.create(policy); renderPanel(policy); });
  renderProgress();
}
function advance() {
  const steps = engine.samples[sampleIndex].steps;
  if (stepIndex >= steps.length) return;
  const token = steps[stepIndex++];
  Object.keys(panels).forEach((policy) => dispatch(policy, token === " " ? { type: "space" } : { type: "insert", text: token }, false));
  renderProgress();
}
function renderProgress() {
  const steps = engine.samples[sampleIndex].steps;
  document.querySelector("#progress").textContent = "Bước " + stepIndex + "/" + steps.length + (manuallyEdited ? " · có thao tác riêng" : "");
  document.querySelector("#next").disabled = stepIndex >= steps.length;
  document.querySelector("#run").disabled = stepIndex >= steps.length;
  document.querySelector("#steps").replaceChildren(...steps.map((token, index) => textNode(token === " " ? "Space" : token, "step " + (index < stepIndex ? "done" : index === stepIndex ? "next" : ""))));
}
document.querySelector("#next").addEventListener("click", advance);
document.querySelector("#run").addEventListener("click", () => { while (stepIndex < engine.samples[sampleIndex].steps.length) advance(); });
document.querySelector("#reset").addEventListener("click", reset);
sampleSelect.addEventListener("change", reset);
renderProgress();
