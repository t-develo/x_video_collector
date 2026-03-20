// pages/tagManage.js — タグ管理ページ

import { createElement, clearChildren } from '../utils/dom.js';
import { api, ApiError } from '../api.js';
import { toast } from '../components/toast.js';

/** TagColor enum 値と表示用マッピング */
const TAG_COLORS = [
  { value: 0, name: 'Red', label: '赤', css: '#ff4757' },
  { value: 1, name: 'Orange', label: 'オレンジ', css: '#ffa502' },
  { value: 2, name: 'Yellow', label: '黄', css: '#ffd43b' },
  { value: 3, name: 'Green', label: '緑', css: '#2ed573' },
  { value: 4, name: 'Cyan', label: 'シアン', css: '#00d4aa' },
  { value: 5, name: 'Blue', label: '青', css: '#3b82f6' },
  { value: 6, name: 'Purple', label: '紫', css: '#a855f7' },
  { value: 7, name: 'Pink', label: 'ピンク', css: '#ec4899' },
  { value: 8, name: 'Gray', label: 'グレー', css: '#888888' },
];

/**
 * 色の CSS 値を取得する
 * @param {number} colorValue
 * @returns {string}
 */
export function getTagColorCss(colorValue) {
  return TAG_COLORS.find(c => c.value === colorValue)?.css ?? '#888888';
}

/**
 * カラーセレクターを構築する
 * @param {number} selectedValue
 * @param {function(number): void} onChange
 * @returns {HTMLElement}
 */
function buildColorSelector(selectedValue, onChange) {
  const container = createElement('div', { className: 'tag-color-selector' });

  TAG_COLORS.forEach(color => {
    const swatch = createElement('button', {
      className: `tag-color-swatch${color.value === selectedValue ? ' tag-color-swatch--selected' : ''}`,
      type: 'button',
      title: color.label,
    });
    swatch.dataset.color = String(color.value);
    swatch.setAttribute('aria-label', color.label);

    const inner = createElement('span', { className: 'tag-color-swatch__inner' });
    swatch.appendChild(inner);

    swatch.addEventListener('click', () => {
      container.querySelectorAll('.tag-color-swatch').forEach(s =>
        s.classList.remove('tag-color-swatch--selected'));
      swatch.classList.add('tag-color-swatch--selected');
      onChange(color.value);
    });

    container.appendChild(swatch);
  });

  return container;
}

/**
 * タグ作成フォームを構築する
 * @param {function(): Promise<void>} onCreated
 * @returns {HTMLElement}
 */
function buildCreateForm(onCreated) {
  let selectedColor = 8; // デフォルト: Gray

  const form = createElement('form', { className: 'tag-create-form' });

  const nameInput = createElement('input', {
    className: 'tag-create-input',
    type: 'text',
    placeholder: 'タグ名を入力',
    autocomplete: 'off',
  });

  const colorSelector = buildColorSelector(selectedColor, (val) => {
    selectedColor = val;
  });

  const submitBtn = createElement('button', {
    className: 'tag-create-btn',
    type: 'submit',
    textContent: '追加',
  });

  form.appendChild(nameInput);
  form.appendChild(colorSelector);
  form.appendChild(submitBtn);

  form.addEventListener('submit', async (e) => {
    e.preventDefault();
    const name = nameInput.value.trim();
    if (!name) {
      toast.warning('タグ名を入力してください');
      return;
    }

    submitBtn.disabled = true;
    try {
      await api.post('/tags', { name, color: selectedColor });
      toast.success('タグを作成しました');
      nameInput.value = '';
      await onCreated();
    } catch (err) {
      if (err instanceof ApiError) {
        toast.error(`タグの作成に失敗しました (${err.status})`);
      } else {
        toast.error('ネットワークエラーが発生しました');
      }
    } finally {
      submitBtn.disabled = false;
    }
  });

  return form;
}

/**
 * タグ一覧行を構築する（表示モード）
 * @param {object} tag
 * @param {function(): Promise<void>} onRefresh
 * @returns {HTMLElement}
 */
function buildTagRow(tag, onRefresh) {
  const row = createElement('div', { className: 'tag-row' });

  const chip = createElement('span', { className: 'tag-chip tag-chip--colored' });
  chip.dataset.color = String(tag.color);
  chip.textContent = tag.name;

  const actions = createElement('div', { className: 'tag-row__actions' });

  const editBtn = createElement('button', {
    className: 'tag-action-btn',
    textContent: '編集',
    type: 'button',
  });

  const deleteBtn = createElement('button', {
    className: 'tag-action-btn tag-action-btn--danger',
    textContent: '削除',
    type: 'button',
  });

  editBtn.addEventListener('click', () => {
    enterEditMode(row, tag, onRefresh);
  });

  deleteBtn.addEventListener('click', async () => {
    if (!confirm(`タグ「${tag.name}」を削除しますか？`)) return;
    try {
      await api.delete(`/tags/${tag.id}`);
      toast.success('タグを削除しました');
      await onRefresh();
    } catch (err) {
      if (err instanceof ApiError) {
        toast.error(`削除に失敗しました (${err.status})`);
      } else {
        toast.error('ネットワークエラーが発生しました');
      }
    }
  });

  actions.appendChild(editBtn);
  actions.appendChild(deleteBtn);

  row.appendChild(chip);
  row.appendChild(actions);

  return row;
}

/**
 * インライン編集モードに切り替える
 * @param {HTMLElement} row
 * @param {object} tag
 * @param {function(): Promise<void>} onRefresh
 */
function enterEditMode(row, tag, onRefresh) {
  clearChildren(row);
  row.classList.add('tag-row--editing');

  let editColor = tag.color;

  const nameInput = createElement('input', {
    className: 'tag-edit-input',
    type: 'text',
    value: tag.name,
  });

  const colorSelector = buildColorSelector(editColor, (val) => {
    editColor = val;
  });

  const saveBtn = createElement('button', {
    className: 'tag-action-btn tag-action-btn--save',
    textContent: '保存',
    type: 'button',
  });

  const cancelBtn = createElement('button', {
    className: 'tag-action-btn',
    textContent: 'キャンセル',
    type: 'button',
  });

  saveBtn.addEventListener('click', async () => {
    const newName = nameInput.value.trim();
    if (!newName) {
      toast.warning('タグ名を入力してください');
      return;
    }
    saveBtn.disabled = true;
    cancelBtn.disabled = true;
    try {
      await api.put(`/tags/${tag.id}`, { name: newName, color: editColor });
      toast.success('タグを更新しました');
      await onRefresh();
    } catch (err) {
      if (err instanceof ApiError) {
        toast.error(`更新に失敗しました (${err.status})`);
      } else {
        toast.error('ネットワークエラーが発生しました');
      }
      saveBtn.disabled = false;
      cancelBtn.disabled = false;
    }
  });

  cancelBtn.addEventListener('click', () => onRefresh());

  const editActions = createElement('div', { className: 'tag-row__actions' });
  editActions.appendChild(saveBtn);
  editActions.appendChild(cancelBtn);

  row.appendChild(nameInput);
  row.appendChild(colorSelector);
  row.appendChild(editActions);
}

/**
 * タグ管理ページを描画する
 * @param {HTMLElement} container
 */
export async function renderTagManagePage(container) {
  const page = createElement('div', { className: 'tag-manage-page' });

  const title = createElement('h1', { className: 'manage-title', textContent: 'タグ管理' });
  page.appendChild(title);

  const listContainer = createElement('div', { className: 'tag-list' });

  async function refreshList() {
    clearChildren(listContainer);
    try {
      const tags = await api.get('/tags');
      if (tags.length === 0) {
        const empty = createElement('p', {
          className: 'manage-empty',
          textContent: 'タグがまだありません',
        });
        listContainer.appendChild(empty);
      } else {
        tags.forEach(tag => {
          listContainer.appendChild(buildTagRow(tag, refreshList));
        });
      }
    } catch (_err) {
      const errEl = createElement('p', {
        className: 'manage-error',
        textContent: 'タグの取得に失敗しました',
      });
      listContainer.appendChild(errEl);
    }
  }

  const createForm = buildCreateForm(refreshList);
  page.appendChild(createForm);
  page.appendChild(listContainer);
  container.appendChild(page);

  await refreshList();
}
