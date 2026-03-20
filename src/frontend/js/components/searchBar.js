// components/searchBar.js — 検索バーコンポーネント

import { createElement } from '../utils/dom.js';
import { debounce } from '../utils/debounce.js';

const DEBOUNCE_DELAY = 300;

/**
 * 検索バーコンポーネントを生成する
 * @param {object} options
 * @param {string} [options.initialValue=''] - 初期検索キーワード
 * @param {function(string): void} options.onSearch - 検索キーワード変更時のコールバック
 * @returns {HTMLElement}
 */
export function createSearchBar({ initialValue = '', onSearch }) {
  const wrapper = createElement('div', { className: 'search-bar' });

  const icon = createElement('span', { className: 'search-bar__icon', textContent: '🔍' });
  icon.setAttribute('aria-hidden', 'true');

  const input = createElement('input', {
    className: 'search-bar__input',
    type: 'search',
    placeholder: 'タイトルで検索...',
    value: initialValue,
  });
  input.setAttribute('aria-label', 'タイトルで検索');

  const clearBtn = createElement('button', {
    className: 'search-bar__clear',
    type: 'button',
    textContent: '✕',
  });
  clearBtn.setAttribute('aria-label', 'クリア');
  clearBtn.hidden = initialValue === '';

  const debouncedSearch = debounce(onSearch, DEBOUNCE_DELAY);

  input.addEventListener('input', () => {
    const value = input.value;
    clearBtn.hidden = value === '';
    debouncedSearch(value);
  });

  clearBtn.addEventListener('click', () => {
    input.value = '';
    clearBtn.hidden = true;
    onSearch('');
  });

  wrapper.appendChild(icon);
  wrapper.appendChild(input);
  wrapper.appendChild(clearBtn);

  return wrapper;
}
