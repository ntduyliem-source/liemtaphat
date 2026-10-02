(function (root, factory) {
  const api = factory(typeof module === "object" && module.exports ? require("./state-machine.js") : root.LocusSpace);
  if (typeof module === "object" && module.exports) module.exports = api;
  else root.LocusMarkers = api;
})(typeof globalThis !== "undefined" ? globalThis : this, function (fixtures) {
  "use strict";
  const copy = (value) => JSON.parse(JSON.stringify(value));
  function validateConfig(config) {
    if (typeof config.open !== "string" || typeof config.close !== "string" || !config.open.length || !config.close.length) return "Hai dấu phải là chuỗi không rỗng.";
    if (/[\r\n\\]/.test(config.open + config.close)) return "Dấu không được chứa xuống dòng hoặc backslash.";
    if (config.open.startsWith(config.close) || config.close.startsWith(config.open)) return "Hai dấu phải khác nhau và không là phần đầu của nhau.";
    return null;
  }
  function create() {
    return { raw: "", sourceRevision: 0, config: { open: "lc[", close: "]" }, configRevision: 0, configError: null, composing: false, equations: [], suppressed: [], pending: [], suggestions: [], issues: [], history: [], log: [], sequence: 0 };
  }
  function record(state, action, message) {
    state.sequence += 1;
    state.log.push({ sequence: state.sequence, action, message, sourceRevision: state.sourceRevision, configRevision: state.configRevision });
    state.log = state.log.slice(-60);
    return state;
  }
  function snapshot(state) {
    return copy({ raw: state.raw, equations: state.equations, suppressed: state.suppressed });
  }
  function overlaps(a, b) { return a[0] < b[1] && b[0] < a[1]; }
  function protectedTokens(raw) {
    const result = [];
    const pattern = /\S+/gu;
    for (const match of raw.matchAll(pattern)) {
      const token = match[0];
      let code;
      if (/^(?:https?:\/\/|www\.)/i.test(token)) code = "PROTECTED_URL";
      else if (/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(token)) code = "PROTECTED_EMAIL";
      else if (/^(?:[A-Za-z]:[\\/]|\\\\|\/(?!\/)|\.\.?\/|~\/)/.test(token)) code = "PROTECTED_PATH";
      if (code) result.push({ span: [match.index, match.index + token.length], code });
    }
    return result;
  }
  function scan(raw, config, excluded = []) {
    const regions = [], issues = [];
    const configError = validateConfig(config);
    if (configError) return { regions, issues: [{ code: "INVALID_DELIMITER_CONFIG", span: [0, raw.length] }] };
    const { open, close } = config;
    const protectedRanges = protectedTokens(raw);
    let cursor = 0;
    while (cursor < raw.length) {
      const start = raw.indexOf(open, cursor);
      if (start < 0) break;
      const protectedRange = protectedRanges.find((part) => start >= part.span[0] && start < part.span[1]);
      if (protectedRange) { issues.push(protectedRange); cursor = protectedRange.span[1]; continue; }
      let next = start + open.length, depth = 1, nested = false, escaped = raw[start - 1] === "\\", end = -1;
      while (next <= raw.length) {
        const nextOpen = raw.indexOf(open, next), nextClose = raw.indexOf(close, next);
        if (nextClose < 0) break;
        if (nextOpen >= 0 && nextOpen < nextClose) { depth += 1; nested = true; next = nextOpen + open.length; }
        else {
          if (raw[nextClose - 1] === "\\") escaped = true;
          depth -= 1;
          next = nextClose + close.length;
          if (depth === 0) { end = next; break; }
        }
      }
      const span = [start, end < 0 ? raw.length : end];
      cursor = end < 0 ? raw.length : end;
      if (excluded.some((item) => overlaps(span, item))) continue;
      if (end < 0) { issues.push({ code: nested ? "NESTED_MARKER_UNSUPPORTED" : "UNCLOSED_MARKER", span }); continue; }
      if (nested || escaped) { issues.push({ code: nested ? "NESTED_MARKER_UNSUPPORTED" : "MARKER_ESCAPE_UNSUPPORTED", span }); continue; }
      const contentSpan = [start + open.length, end - close.length];
      const content = raw.slice(...contentSpan);
      if (!content.trim()) { issues.push({ code: "EMPTY_MARKED_REGION", span }); continue; }
      if (/[\r\n]/.test(content)) { issues.push({ code: "UNSUPPORTED_MULTILINE_EXPRESSION", span }); continue; }
      const result = fixtures.inspect(content);
      if (!result.candidates.length) { issues.push({ code: "OUTSIDE_FIXTURES", span }); continue; }
      regions.push({ span, contentSpan, originalReplacement: raw.slice(...span), originalContent: content, result });
    }
    return { regions, issues };
  }
  function analyze(state) {
    state.pending = []; state.suggestions = []; state.issues = [];
    if (state.composing) return;
    const output = scan(state.raw, state.config, [...state.equations.map((item) => item.span), ...state.suppressed]);
    state.issues = output.issues;
    for (const region of output.regions) {
      const task = { ...region, id: `${state.sourceRevision}:${state.configRevision}:${region.span.join(":")}`, sourceRevision: state.sourceRevision, configRevision: state.configRevision, config: copy(state.config) };
      if (region.result.status === "ready" && region.result.candidates.length === 1 && region.result.candidates[0].kind === "direct") state.pending.push(task);
      else state.suggestions.push(task);
    }
  }
  function remapList(list, oldRaw, newRaw, key) {
    let start = 0;
    while (start < oldRaw.length && start < newRaw.length && oldRaw[start] === newRaw[start]) start++;
    let oldEnd = oldRaw.length, newEnd = newRaw.length;
    while (oldEnd > start && newEnd > start && oldRaw[oldEnd - 1] === newRaw[newEnd - 1]) { oldEnd--; newEnd--; }
    const delta = newRaw.length - oldRaw.length;
    return list.flatMap((item) => {
      const span = key ? item[key] : item;
      if (span[1] <= start) return [item];
      if (span[0] >= oldEnd) {
        const mapped = [span[0] + delta, span[1] + delta];
        return [key ? { ...item, [key]: mapped } : mapped];
      }
      return [];
    });
  }
  function validTask(state, task) {
    return !state.composing && task && task.sourceRevision === state.sourceRevision && task.configRevision === state.configRevision && task.originalReplacement === state.raw.slice(...task.span) && !state.equations.some((item) => overlaps(item.span, task.span)) && !state.suppressed.some((span) => overlaps(span, task.span));
  }
  function commit(state, task, candidate) {
    state.history.push(snapshot(state));
    state.equations.push({ id: `equation-${state.sequence + 1}`, span: task.span, originalReplacement: task.originalReplacement, originalContent: task.originalContent, config: task.config, candidates: task.result.candidates, selected: candidate });
    state.equations.sort((a, b) => a.span[0] - b.span[0]);
    state.pending = state.pending.filter((item) => item.id !== task.id);
    state.suggestions = state.suggestions.filter((item) => item.id !== task.id);
  }
  function transition(previous, action) {
    const state = copy(previous);
    if (action.type === "config") {
      const error = validateConfig(action.config);
      state.configError = error;
      state.pending = []; state.suggestions = []; state.issues = [];
      if (error) return record(state, action.type, error + " Giữ cấu hình hợp lệ đang dùng.");
      state.config = copy(action.config); state.configRevision++;
      state.pending = []; state.suggestions = []; state.issues = [];
      return record(state, action.type, "Đã áp dụng dấu mới, hủy mọi tác vụ chờ. Chờ nhập tiếp hoặc lệnh nhận diện lại.");
    }
    if (action.type === "composition-start") {
      state.composing = true; state.pending = []; state.suggestions = []; state.issues = [];
      return record(state, action.type, "Đang ghép ký tự DOM; không nhận diện hoặc chuyển.");
    }
    if (action.type === "composition-end") {
      state.composing = false; analyze(state);
      return record(state, action.type, "Đã kết thúc composition DOM; xét nguồn hiện tại.");
    }
    if (action.type === "edit") {
      if (action.raw === state.raw) return state;
      state.history.push(snapshot(state));
      state.equations = remapList(state.equations, state.raw, action.raw, "span");
      state.suppressed = remapList(state.suppressed, state.raw, action.raw);
      state.raw = action.raw; state.sourceRevision++;
      analyze(state);
      return record(state, action.type, state.composing ? "Giữ nguồn đang ghép; chưa nhận diện." : `Cập nhật nguồn: ${state.pending.length} vùng đủ điều kiện, ${state.suggestions.length} vùng cần chọn.`);
    }
    if (state.composing) return record(state, action.type, "Đã chặn trong composition DOM.");
    if (action.type === "analyze") {
      state.suppressed = [];
      analyze(state);
      return record(state, action.type, "Lệnh chủ động xét lại vùng đã restore; chưa ghi nếu tác vụ chưa được xác minh.");
    }
    if (action.type === "commit") {
      const task = action.task;
      if (!validTask(state, task) || !state.pending.some((item) => item.id === task.id)) return record(state, action.type, "Từ chối tác vụ cũ: nguồn/cấu hình/trạng thái hoặc vùng không còn khớp.");
      const latest = fixtures.inspect(state.raw.slice(...task.contentSpan));
      if (latest.status !== "ready" || latest.candidates.length !== 1 || latest.candidates[0].kind !== "direct" || latest.candidates[0].id !== task.result.candidates[0].id) return record(state, action.type, "Từ chối: không còn đúng một direct đủ điều kiện.");
      commit(state, task, latest.candidates[0]);
      return record(state, action.type, "Đã tự dựng vùng đóng đủ trong preview mô phỏng; giữ nguyên phần ngoài vùng.");
    }
    if (action.type === "confirm") {
      const task = state.suggestions.find((item) => item.id === action.taskId);
      const candidate = task && task.result.candidates.find((item) => item.id === action.candidateId);
      if (!validTask(state, task) || !candidate) return record(state, action.type, "Lựa chọn đã cũ; không thay vùng.");
      commit(state, task, candidate);
      return record(state, action.type, `Đã chuyển theo lựa chọn ${candidate.kind}; không phải tự chọn repair.`);
    }
    if (action.type === "restore") {
      const equation = state.equations.find((item) => item.id === action.id);
      if (!equation || state.raw.slice(...equation.span) !== equation.originalReplacement) return record(state, action.type, "Không restore từ nguồn không còn khớp.");
      state.history.push(snapshot(state));
      state.equations = state.equations.filter((item) => item.id !== action.id);
      state.suppressed.push(equation.span);
      state.pending = []; state.suggestions = [];
      return record(state, action.type, "Đã khôi phục nguyên văn cả hai dấu. Sự kiện restore không tự chuyển lại.");
    }
    if (action.type === "undo") {
      const before = state.history.pop();
      if (!before) return record(state, action.type, "Chưa có thao tác để hoàn tác.");
      Object.assign(state, before); state.sourceRevision++;
      state.pending = []; state.suggestions = []; state.issues = [];
      // Undo must not immediately replay the conversion it just undid.
      const output = scan(state.raw, state.config, state.equations.map((item) => item.span));
      state.suppressed.push(...output.regions.map((item) => item.span));
      return record(state, action.type, "Undo mô phỏng về trước thao tác; không chạy lại tự chuyển từ sự kiện Undo.");
    }
    throw new Error("Unknown marker action: " + action.type);
  }
  function parts(state) {
    const result = []; let cursor = 0;
    for (const equation of state.equations) {
      if (equation.span[0] > cursor) result.push({ kind: "text", text: state.raw.slice(cursor, equation.span[0]) });
      result.push({ kind: "equation", ...equation }); cursor = equation.span[1];
    }
    if (cursor < state.raw.length || !result.length) result.push({ kind: "text", text: state.raw.slice(cursor) });
    return result;
  }
  return { create, validateConfig, scan, transition, parts };
});
