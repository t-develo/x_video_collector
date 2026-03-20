// state.js — シンプルな Pub/Sub 状態管理

/** @type {Record<string, unknown>} */
const state = {};

/** @type {Map<string, Array<function(unknown): void>>} */
const listeners = new Map();

/**
 * 状態値を取得する
 * @param {string} key
 * @returns {unknown}
 */
export function getState(key) {
  return state[key];
}

/**
 * 状態値を更新し、購読者へ通知する
 * @param {string} key
 * @param {unknown} value
 */
export function setState(key, value) {
  state[key] = value;
  const keyListeners = listeners.get(key) ?? [];
  keyListeners.forEach(fn => fn(value));
}

/**
 * 状態変化を購読する
 * @param {string} key
 * @param {function(unknown): void} fn
 * @returns {function(): void} 購読解除関数
 */
export function subscribe(key, fn) {
  if (!listeners.has(key)) {
    listeners.set(key, []);
  }
  listeners.get(key).push(fn);

  return () => {
    const arr = listeners.get(key);
    if (!arr) return;
    const idx = arr.indexOf(fn);
    if (idx >= 0) arr.splice(idx, 1);
  };
}

/**
 * テスト用：すべての状態をリセットする
 */
export function resetState() {
  Object.keys(state).forEach(key => delete state[key]);
  listeners.clear();
}
