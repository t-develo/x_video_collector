// pages/placeholder.js — 未実装ページのプレースホルダー

import { createElement } from '../utils/dom.js';

/**
 * 未実装ページを描画するプレースホルダー
 * @param {string} pageName
 * @returns {function(HTMLElement): void}
 */
export function createPlaceholderPage(pageName) {
  return function renderPage(container) {
    const wrapper = createElement('div', {
      className: 'placeholder-wrapper',
    });

    const title = createElement('h2', {
      className: 'placeholder-title',
      textContent: pageName,
    });

    const msg = createElement('p', {
      className: 'placeholder-message',
      textContent: 'このページは Sprint 8 以降で実装予定です。',
    });

    wrapper.appendChild(title);
    wrapper.appendChild(msg);
    container.appendChild(wrapper);
  };
}
