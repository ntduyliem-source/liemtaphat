const key = 'locus.theme.v1';
export function current() {
  return document.documentElement.dataset.theme === 'dark' ? 'dark' : 'light';
}
export function apply(theme) {
  const next = theme === 'dark' ? 'dark' : 'light';
  document.documentElement.dataset.theme = next;
  try { localStorage.setItem(key, next); } catch { /* Theme still works without storage. */ }
  return next;
}
let saved;
try { saved = localStorage.getItem(key); } catch { /* Private storage can be unavailable. */ }
document.documentElement.dataset.theme = saved === 'dark' ? 'dark' : 'light';
