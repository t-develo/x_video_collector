// components/filterPanel.js — フィルターパネルコンポーネント

import { createElement } from '../utils/dom.js';

/** @typedef {{ tagIds: string[], categoryId: string|null, status: string|null }} FilterState */

/** ステータス選択肢 */
const STATUS_OPTIONS = [
  { value: 'Ready', label: '準備完了' },
  { value: 'Pending', label: '処理中' },
  { value: 'Failed', label: 'エラー' },
];

/**
 * フィルターパネルコンポーネントを生成する
 * @param {object} options
 * @param {Array<{id: string, name: string, color: string}>} options.tags - 選択可能なタグ一覧
 * @param {Array<{id: string, name: string}>} options.categories - 選択可能なカテゴリ一覧
 * @param {FilterState} options.initialFilters - 初期フィルター状態
 * @param {function(FilterState): void} options.onFilter - フィルター変更時のコールバック
 * @returns {HTMLElement}
 */
export function createFilterPanel({ tags, categories, initialFilters, onFilter }) {
  /** @type {FilterState} */
  const state = {
    tagIds: initialFilters.tagIds ? [...initialFilters.tagIds] : [],
    categoryId: initialFilters.categoryId ?? null,
    status: initialFilters.status ?? null,
  };

  const panel = createElement('div', { className: 'filter-panel' });

  // ステータスフィルター
  const statusSection = createElement('div', { className: 'filter-section' });
  const statusLabel = createElement('p', { className: 'filter-section__label', textContent: 'ステータス' });
  const statusSelect = createElement('select', { className: 'filter-section__select' });

  const statusAllOption = createElement('option', { value: '', textContent: 'すべて' });
  statusSelect.appendChild(statusAllOption);

  STATUS_OPTIONS.forEach(({ value, label }) => {
    const opt = createElement('option', { value, textContent: label });
    if (state.status === value) opt.selected = true;
    statusSelect.appendChild(opt);
  });

  statusSelect.addEventListener('change', () => {
    state.status = statusSelect.value || null;
    onFilter({ ...state });
  });

  statusSection.appendChild(statusLabel);
  statusSection.appendChild(statusSelect);

  // カテゴリフィルター
  const categorySection = createElement('div', { className: 'filter-section' });
  const categoryLabel = createElement('p', { className: 'filter-section__label', textContent: 'カテゴリ' });
  const categorySelect = createElement('select', { className: 'filter-section__select' });

  const categoryAllOption = createElement('option', { value: '', textContent: 'すべて' });
  categorySelect.appendChild(categoryAllOption);

  categories.forEach(cat => {
    const opt = createElement('option', { value: cat.id, textContent: cat.name });
    if (state.categoryId === cat.id) opt.selected = true;
    categorySelect.appendChild(opt);
  });

  categorySelect.addEventListener('change', () => {
    state.categoryId = categorySelect.value || null;
    onFilter({ ...state });
  });

  categorySection.appendChild(categoryLabel);
  categorySection.appendChild(categorySelect);

  // タグフィルター
  const tagSection = createElement('div', { className: 'filter-section' });
  const tagLabel = createElement('p', { className: 'filter-section__label', textContent: 'タグ' });
  const tagList = createElement('div', { className: 'filter-section__tag-list' });

  tags.forEach(tag => {
    const tagBtn = createElement('button', {
      className: 'filter-tag-btn',
      type: 'button',
      textContent: tag.name,
    });
    if (state.tagIds.includes(tag.id)) {
      tagBtn.classList.add('filter-tag-btn--active');
    }
    tagBtn.dataset.tagId = tag.id;
    tagBtn.addEventListener('click', () => {
      const idx = state.tagIds.indexOf(tag.id);
      if (idx === -1) {
        state.tagIds.push(tag.id);
        tagBtn.classList.add('filter-tag-btn--active');
      } else {
        state.tagIds.splice(idx, 1);
        tagBtn.classList.remove('filter-tag-btn--active');
      }
      onFilter({ ...state, tagIds: [...state.tagIds] });
    });
    tagList.appendChild(tagBtn);
  });

  tagSection.appendChild(tagLabel);
  tagSection.appendChild(tagList);

  // リセットボタン
  const resetBtn = createElement('button', {
    className: 'filter-panel__reset',
    type: 'button',
    textContent: 'フィルターをリセット',
  });
  resetBtn.addEventListener('click', () => {
    state.tagIds = [];
    state.categoryId = null;
    state.status = null;

    statusSelect.value = '';
    categorySelect.value = '';
    tagList.querySelectorAll('.filter-tag-btn--active').forEach(btn => {
      btn.classList.remove('filter-tag-btn--active');
    });

    onFilter({ ...state });
  });

  panel.appendChild(statusSection);
  panel.appendChild(categorySection);
  panel.appendChild(tagSection);
  panel.appendChild(resetBtn);

  return panel;
}
