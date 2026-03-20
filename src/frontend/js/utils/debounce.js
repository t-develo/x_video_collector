// utils/debounce.js — デバウンスユーティリティ

/**
 * 関数をデバウンスする
 * @template {(...args: unknown[]) => void} T
 * @param {T} fn - デバウンス対象の関数
 * @param {number} delay - 待機時間（ミリ秒）
 * @returns {T} デバウンスされた関数
 */
export function debounce(fn, delay) {
  let timer;
  return (/** @type {unknown[]} */ ...args) => {
    clearTimeout(timer);
    timer = setTimeout(() => fn(...args), delay);
  };
}
