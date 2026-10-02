"use strict";
const input = document.getElementById("input"), mode = document.getElementById("mode");
const openMarker = document.getElementById("open"), closeMarker = document.getElementById("close");
const results = document.getElementById("results"), diagnostics = document.getElementById("diagnostics");
const status = document.getElementById("status"), analyzeButton = document.getElementById("analyze");
let revision = 0, timer, controller, composing = false, toastTimer;
const modeHelp = {explicit:"Phù hợp khi bạn nhập một công thức độc lập.",passive:"Tìm các vùng có dấu hiệu toán trong câu; bỏ qua URL, email và đường dẫn.",marked:"Chỉ phân tích vùng đã đóng bằng đúng cặp dấu bạn chọn."};
const messages = {
  AMBIGUOUS_IMPLICIT_DIVISION:"Phép chia cạnh phép nhân ngầm có nhiều cách hiểu. Bạn hãy chọn kết quả phù hợp.",
  AMBIGUITY_LIMIT:"Có quá nhiều vị trí mơ hồ. Hãy thêm dấu ngoặc để làm rõ.",
  MISSING_CLOSE_PAREN:"Nguồn thiếu dấu ngoặc đóng. Phương án sửa cần bạn cân nhắc.",
  MISSING_OPERAND:"Biểu thức còn thiếu một thành phần.",
  UNCLOSED_MARKER:"Cặp dấu chưa đóng. Locus đang chờ bạn viết tiếp.",
  INSUFFICIENT_MATH_EVIDENCE:"Chưa có đủ dấu hiệu để xem nội dung này là công thức.",
  EMPTY_EXPRESSION:"Hãy nhập một công thức để bắt đầu.",
  INVALID_MARKER_CONFIGURATION:"Cặp dấu mở và đóng chưa hợp lệ.",
  UNSUPPORTED_DOMAIN:"Miền này chưa được hỗ trợ trong bản Toán hiện tại."
};
function el(tag, text, className) { const node = document.createElement(tag); if (text !== undefined) node.textContent = text; if (className) node.className = className; return node; }
function toast(text) { const node = document.getElementById("toast"); node.textContent = text; node.hidden = false; clearTimeout(toastTimer); toastTimer = setTimeout(() => { node.hidden = true; }, 2200); }
function addDiagnostics(items, target) {
  for (const item of items || []) {
    const text = messages[item.code] || (item.message !== item.code ? item.message : `Chẩn đoán: ${item.code}`);
    target.append(el("div", text, "diagnostic " + (item.severity === "info" ? "info" : "")));
  }
}
function empty(text) { const node = el("div", undefined, "empty"); node.append(el("span", "ƒ", "icon"), el("div", text)); results.replaceChildren(node); }
function makeCandidate(candidate, first) {
  const card = el("article", undefined, "candidate" + (first ? " selected" : ""));
  card.dataset.candidateId = candidate.id;
  const labels = {direct:"Đúng theo chuỗi đã nhập",interpretation:"Cách hiểu khác",repair:"Đề nghị sửa"};
  const header = el("div", undefined, "candidate-head"), choose = el("button", first ? "Đang chọn ✓" : "Chọn", "pick");
  choose.type = "button"; choose.setAttribute("aria-pressed", String(first));
  choose.addEventListener("click", () => {
    for (const sibling of card.parentElement.querySelectorAll(".candidate")) {
      sibling.classList.toggle("selected", sibling === card);
      const button = sibling.querySelector(".pick"); button.textContent = sibling === card ? "Đang chọn ✓" : "Chọn"; button.setAttribute("aria-pressed", String(sibling === card));
    }
  });
  header.append(el("span", labels[candidate.kind] || candidate.kind, "candidate-label " + candidate.kind), choose);
  const formula = el("div", undefined, "formula");
  const parsed = new DOMParser().parseFromString(candidate.exports.mathMl, "application/xml");
  if (parsed.documentElement.localName !== "math" || parsed.documentElement.namespaceURI !== "http://www.w3.org/1998/Math/MathML" || parsed.querySelector("parsererror"))
    throw new Error("Không thể đọc bản xem trước công thức.");
  formula.append(document.importNode(parsed.documentElement, true));
  const copyRow = el("div", undefined, "copy-row");
  for (const [label, key] of [["Copy LaTeX", "latex"], ["Copy OMML", "omml"], ["Copy nguồn", "originalText"]]) {
    const button = el("button", label); button.type = "button";
    button.addEventListener("click", async () => {
      try { await navigator.clipboard.writeText(candidate.exports[key]); toast("Đã sao chép " + label.replace("Copy ", "")); }
      catch { toast("Trình duyệt chưa cho phép sao chép."); }
    });
    copyRow.append(button);
  }
  card.append(header, formula, copyRow); addDiagnostics(candidate.diagnostics, card);
  if (candidate.kind === "repair") card.append(el("p", "Đây là đề nghị thay đổi nguồn; không được tự động áp dụng.", "hint"));
  return card;
}
function render(data) {
  diagnostics.replaceChildren(); results.replaceChildren(); addDiagnostics(data.diagnostics, diagnostics);
  const count = data.regions.reduce((sum, region) => sum + region.candidates.length, 0);
  status.textContent = count ? `${data.regions.length} vùng · ${count} phương án` : data.isIncomplete ? "Chờ viết tiếp" : "Chưa có công thức";
  if (!count) { empty(input.value.trim() ? "Thử thêm dấu toán hoặc chọn chế độ phù hợp với nội dung." : "Gõ một công thức để xem kết quả ở đây."); return; }
  for (const region of data.regions) {
    const section = el("section", undefined, "region");
    const heading = el("div", undefined, "region-heading");
    heading.append(el("code", region.originalContent), el("span", `${region.candidates.length} phương án`));
    section.append(heading); addDiagnostics(region.diagnostics, section);
    region.candidates.forEach((candidate, index) => section.append(makeCandidate(candidate, index === 0 && candidate.kind === "direct")));
    const details = el("details"), span = region.contentSpan, replacement = region.replacementSpan;
    details.append(el("summary", "Vùng nguồn được giữ lại"), el("p", `Nội dung [${span.start}, ${span.end}) · Vùng thay thế [${replacement.start}, ${replacement.end}) · vị trí UTF-16`), el("code", region.originalReplacement));
    section.append(details); results.append(section);
  }
}
async function analyze() {
  clearTimeout(timer); controller?.abort();
  if (composing) return;
  const current = ++revision; controller = new AbortController(); const signal = controller.signal;
  const request = {input:input.value, mode:mode.value, open:openMarker.value, close:closeMarker.value, revision:current};
  diagnostics.replaceChildren(); empty("Đang phân tích nguồn hiện tại…");
  status.textContent = "Đang phân tích…"; analyzeButton.disabled = true;
  try {
    const response = await fetch("/analyze", {method:"POST",headers:{"Content-Type":"application/json"},body:JSON.stringify(request),signal});
    const data = await response.json();
    if (signal.aborted || current !== revision) return;
    if (!response.ok) throw new Error(data.error || "Không thể phân tích nội dung này.");
    if (data.source.revision !== current || data.source.raw !== request.input) throw new Error("Kết quả không khớp nguồn đang phân tích.");
    render(data);
  } catch (error) {
    if (signal.aborted || current !== revision || error.name === "AbortError") return;
    status.textContent = "Cần kiểm tra"; diagnostics.replaceChildren(el("div", error.message, "diagnostic")); empty("Bạn có thể sửa nội dung rồi thử lại.");
  } finally { if (current === revision) analyzeButton.disabled = false; }
}
function schedule() {
  ++revision; controller?.abort(); clearTimeout(timer);
  // Clear stale previews and their copy actions synchronously, before debounce.
  diagnostics.replaceChildren(); empty(composing ? "Đang chờ bạn ghép xong ký tự…" : "Đang phân tích nguồn hiện tại…");
  status.textContent = composing ? "Đang ghép ký tự…" : "Đang phân tích…";
  analyzeButton.disabled = true;
  document.getElementById("count").textContent = `${input.value.length} ký tự UTF-16`;
  document.getElementById("marker-fields").hidden = mode.value !== "marked";
  document.getElementById("mode-help").textContent = modeHelp[mode.value];
  if (!composing) timer = setTimeout(analyze, 180);
}
input.addEventListener("compositionstart", () => { composing = true; schedule(); });
input.addEventListener("compositionend", () => { composing = false; schedule(); });
for (const node of [input, openMarker, closeMarker]) node.addEventListener("input", schedule);
mode.addEventListener("change", schedule); analyzeButton.addEventListener("click", analyze);
for (const button of document.querySelectorAll("[data-example]")) button.addEventListener("click", () => { input.value = button.dataset.example; mode.value = button.dataset.mode || "explicit"; if (mode.value === "marked") {openMarker.value = "lc["; closeMarker.value = "]";} schedule(); input.focus(); });
schedule();
