// components/toast.js — トースト通知コンポーネント

import { createElement } from '../utils/dom.js';

const DEFAULT_DURATION = 4000;

/**
 * @typedef {'success'|'error'|'warning'|'info'} ToastType
 */

/**
 * トースト通知を表示する
 * @param {string} message
 * @param {ToastType} type
 * @param {number} duration - 表示時間（ms）
 */
export function showToast(message, type = 'info', duration = DEFAULT_DURATION) {
  const container = document.getElementById('toast-container');
  if (!container) return;

  const toast = buildToast(message, type);
  container.appendChild(toast);

  const remove = () => {
    if (toast.parentNode === container) {
      container.removeChild(toast);
    }
  };

  const timer = setTimeout(remove, duration);

  const closeBtn = toast.querySelector('.toast-close');
  if (closeBtn) {
    closeBtn.addEventListener('click', () => {
      clearTimeout(timer);
      remove();
    });
  }
}

/**
 * トースト要素を構築する
 * @param {string} message
 * @param {ToastType} type
 * @returns {HTMLElement}
 */
function buildToast(message, type) {
  const toast = createElement('div', { className: `toast toast-${type}` });

  const indicator = createElement('span', { className: 'toast-indicator' });
  const msg = createElement('span', { className: 'toast-message', textContent: message });
  const closeBtn = createElement('button', {
    className: 'toast-close',
    'aria-label': '閉じる',
    textContent: '×',
  });

  toast.appendChild(indicator);
  toast.appendChild(msg);
  toast.appendChild(closeBtn);

  return toast;
}

export const toast = {
  success: (msg, duration) => showToast(msg, 'success', duration),
  error: (msg, duration) => showToast(msg, 'error', duration),
  warning: (msg, duration) => showToast(msg, 'warning', duration),
  info: (msg, duration) => showToast(msg, 'info', duration),
};
