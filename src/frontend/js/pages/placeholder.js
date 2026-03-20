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
      style: { padding: 'var(--space-xl)' },
    });

    const title = createElement('h2', {
      style: {
        fontFamily: 'var(--font-mono)',
        color: 'var(--color-accent)',
        marginBottom: 'var(--space-md)',
      },
      textContent: pageName,
    });

    const msg = createElement('p', {
      style: { color: 'var(--color-text-secondary)' },
      textContent: 'このページは Sprint 8 以降で実装予定です。',
    });

    wrapper.appendChild(title);
    wrapper.appendChild(msg);
    container.appendChild(wrapper);
  };
}
