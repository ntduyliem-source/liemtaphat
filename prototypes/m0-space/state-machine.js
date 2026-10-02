(function (root, factory) {
  const api = factory();
  if (typeof module === "object" && module.exports) module.exports = api;
  else root.LocusSpace = api;
})(typeof globalThis !== "undefined" ? globalThis : this, function () {
  "use strict";

  // A finite fixture mapper, deliberately not a production parser.
  const fixtures = {
    "x^2": [{ id: "power", kind: "direct", label: "x²", math: "<msup><mi>x</mi><mn>2</mn></msup>" }],
    "x mũ 2": [{ id: "power", kind: "direct", label: "x²", math: "<msup><mi>x</mi><mn>2</mn></msup>" }],
    "x mũ 2 cộng 1": [{ id: "power-sum", kind: "direct", label: "x² + 1", math: "<mrow><msup><mi>x</mi><mn>2</mn></msup><mo>+</mo><mn>1</mn></mrow>" }],
    "1 trên 2": [{ id: "half", kind: "direct", label: "½", math: "<mfrac><mn>1</mn><mn>2</mn></mfrac>" }],
    "căn x": [{ id: "root", kind: "direct", label: "√x", math: "<msqrt><mi>x</mi></msqrt>" }],
    "căn x cộng 1": [
      { id: "root-plus", kind: "direct", label: "√x + 1", math: "<mrow><msqrt><mi>x</mi></msqrt><mo>+</mo><mn>1</mn></mrow>" },
      { id: "root-sum", kind: "repair", label: "√(x + 1)", math: "<msqrt><mrow><mi>x</mi><mo>+</mo><mn>1</mn></mrow></msqrt>" },
    ],
    "2/3x": [
      { id: "divide-then-multiply", kind: "direct", label: "(2/3) × x", math: "<mrow><mfrac><mn>2</mn><mn>3</mn></mfrac><mo>×</mo><mi>x</mi></mrow>" },
      { id: "implicit-denominator", kind: "interpretation", label: "2/(3x)", math: "<mfrac><mn>2</mn><mrow><mn>3</mn><mi>x</mi></mrow></mfrac>" },
    ],
  };
  const samples = [
    { id: "power", label: "x mũ 2 cộng 1", steps: ["x", " ", "mũ", " ", "2", " ", "cộng", " ", "1", " "] },
    { id: "fraction", label: "1 trên 2", steps: ["1", " ", "trên", " ", "2", " "] },
    { id: "root", label: "căn x cộng 1", steps: ["căn", " ", "x", " ", "cộng", " ", "1", " "] },
    { id: "ambiguity", label: "2/3x · nhiều cách hiểu", steps: ["2/3x", " "] },
  ];
  const copy = (value) => JSON.parse(JSON.stringify(value));
  const normalize = (source) => source.normalize("NFC").trim().replace(/\s+/g, " ");

  function inspect(source) {
    const key = normalize(source);
    if (!key) return { status: "empty", candidates: [], message: "Chưa có nguồn nhập." };
    if (fixtures[key]) {
      const candidates = copy(fixtures[key]);
      const hasRepair = candidates.some((candidate) => candidate.kind === "repair");
      return {
        status: candidates.length === 1 ? "ready" : hasRepair ? "review" : "ambiguous",
        candidates,
        message: candidates.length === 1 ? "Một kết quả trong bộ mẫu." : hasRepair ? "√x + 1 theo nguồn. √(x + 1) là sửa phạm vi, không phải cách hiểu ngang hàng. Demo yêu cầu chọn trước khi chốt." : "Chia và nhân ngầm có nhiều cách hiểu trong bộ mẫu. Cần chọn trước khi chốt.",
      };
    }
    if (Object.keys(fixtures).some((full) => full.startsWith(key + " "))) {
      return { status: "incomplete", candidates: [], message: "Chờ nhập tiếp; chưa đủ điều kiện chuyển." };
    }
    return { status: "unsupported", candidates: [], message: "Ngoài bộ mẫu. Giữ nguyên nguồn, không suy đoán." };
  }

  function create(policy) {
    if (!["retain", "commit"].includes(policy)) throw new Error("Unknown policy");
    return { policy, doc: [], source: "", caret: 0, composing: false, selectedCandidate: null, phase: "idle", history: [], log: [], sequence: 0 };
  }
  function snapshot(state) {
    const { history, log, sequence, policy, ...content } = state;
    return copy(content);
  }
  function record(state, event, message) {
    state.sequence += 1;
    state.log.push({ sequence: state.sequence, event, message, caret: state.caret, phase: state.phase });
    state.log = state.log.slice(-60);
    return state;
  }
  function appendText(state, text) {
    if (!text) return;
    const last = state.doc[state.doc.length - 1];
    if (last && last.kind === "text") last.text += text;
    else state.doc.push({ kind: "text", text });
  }
  function commitSource(state, candidate) {
    const leading = state.source.match(/^\s*/)[0];
    const trailing = state.source.match(/\s*$/)[0];
    appendText(state, leading);
    state.doc.push({ kind: "equation", source: state.source.slice(leading.length, state.source.length - trailing.length), candidate: copy(candidate) });
    appendText(state, trailing);
    state.source = "";
    state.caret = 0;
    state.selectedCandidate = null;
    state.phase = "text-after-equation";
  }

  function transition(previous, action) {
    const state = copy(previous);
    if (action.type === "caret") {
      state.caret = Math.max(0, Math.min(action.caret, state.source.length));
      return state;
    }
    if (action.type === "composition-start") {
      state.composing = true;
      return record(state, action.type, "DOM composition bắt đầu; chặn Space/Enter/thoát và không dựng preview.");
    }
    if (action.type === "composition-end") {
      state.composing = false;
      return record(state, action.type, "DOM composition kết thúc; chỉ cập nhật preview, chưa chốt.");
    }
    if (state.composing && !["replace", "insert"].includes(action.type)) {
      return record(state, action.type, "Đã chặn trong composition mô phỏng; nguồn không đổi.");
    }
    if (action.type === "undo") {
      const last = state.history.pop();
      if (!last) return record(state, action.type, "Không có thao tác để hoàn tác.");
      Object.assign(state, last);
      // Undo never re-enters a browser composition that has already ended.
      state.composing = false;
      return record(state, action.type, "Khôi phục trạng thái trước thao tác: nguồn, kết quả, con trỏ và lựa chọn.");
    }
    if (action.type === "choose") {
      const result = inspect(state.source);
      if (!result.candidates.some((candidate) => candidate.id === action.id)) {
        return record(state, action.type, "Lựa chọn không còn khớp nguồn; không áp dụng.");
      }
      state.history.push(snapshot(state));
      state.selectedCandidate = action.id;
      return record(state, action.type, "Đã chọn cách hiểu; Enter để chốt theo lựa chọn này.");
    }
    const supported = ["replace", "insert", "space", "backspace", "finish", "escape"];
    if (!supported.includes(action.type)) throw new Error("Unknown action: " + action.type);
    state.history.push(snapshot(state));
    if (["replace", "insert", "space", "backspace"].includes(action.type)) {
      const oldSource = state.source;
      if (action.type === "replace") {
        state.source = action.source;
        state.caret = Math.max(0, Math.min(action.caret ?? state.source.length, state.source.length));
      } else if (action.type === "backspace" && action.rangeEnd > state.caret) {
        state.source = state.source.slice(0, state.caret) + state.source.slice(Math.min(action.rangeEnd, state.source.length));
      } else if (action.type === "backspace") {
        const before = Array.from(state.source.slice(0, state.caret));
        const removed = before.pop();
        if (removed) {
          state.source = before.join("") + state.source.slice(state.caret);
          state.caret -= removed.length;
        }
      } else {
        const text = action.type === "space" ? " " : action.text;
        const rangeEnd = Math.max(state.caret, Math.min(action.rangeEnd ?? state.caret, state.source.length));
        state.source = state.source.slice(0, state.caret) + text + state.source.slice(rangeEnd);
        state.caret += text.length;
      }
      if (oldSource !== state.source) state.selectedCandidate = null;
      state.phase = state.source ? "editing-source" : "idle";
      const result = state.composing ? { status: "composing", candidates: [], message: "Đang ghép ký tự DOM." } : inspect(state.source);
      if (action.type === "space" && state.policy === "commit" && state.caret === state.source.length && result.status === "ready") {
        commitSource(state, result.candidates[0]);
        return record(state, action.type, "Đã chốt ở Space. Con trỏ sang văn bản tiếp theo.");
      }
      return record(state, action.type, state.composing ? "Giữ nguồn đang ghép; không phân tích preview." : action.type === "space" ? "Space giữ nguồn trong phiên; " + result.message : "Cập nhật nguồn; " + result.message);
    }
    if (action.type === "escape") {
      appendText(state, state.source);
      state.source = "";
      state.caret = 0;
      state.selectedCandidate = null;
      state.phase = "plain-text";
      return record(state, action.type, "Thoát phiên: giữ nguyên nguồn thành văn bản. Không dựng equation.");
    }
    const result = inspect(state.source);
    const selected = result.candidates.find((candidate) => candidate.id === state.selectedCandidate);
    if (result.status === "ready" || selected) {
      commitSource(state, selected || result.candidates[0]);
      return record(state, action.type, "Đã chốt mô phỏng và ra văn bản. Không phải thao tác Word.");
    }
    state.history.pop();
    return record(state, action.type, "Chưa chốt: " + result.message + " Esc để giữ nguồn thành văn bản.");
  }
  function view(state) {
    return state.composing ? { status: "composing", candidates: [], message: "Đang ghép ký tự DOM — chưa dựng preview." } : inspect(state.source);
  }
  function transcript(state) {
    return state.doc.map((part) => part.kind === "text" ? part.text : part.source).join("") + state.source;
  }
  return { samples, inspect, create, transition, view, transcript };
});
