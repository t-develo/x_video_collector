// components/spinner.js — ローディングスピナーコンポーネント

import { createElement } from '../utils/dom.js';

/**
 * ローディングスピナーを生成する
 * @returns {HTMLElement}
 */
export function createSpinner() {
  const overlay = createElement('div', { className: 'spinner-overlay' });
  const spinner = createElement('div', { className: 'spinner' });
  overlay.appendChild(spinner);
  return overlay;
}

/**
 * コンテナにスピナーを表示する
 * @param {HTMLElement} container
 * @returns {function(): void} スピナーを削除する関数
 */
export function showSpinner(container) {
  const spinner = createSpinner();
  container.appendChild(spinner);
  return () => {
    if (spinner.parentNode === container) {
      container.removeChild(spinner);
    }
  };
}
