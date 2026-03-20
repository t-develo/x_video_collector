// pages/categoryManage.js — カテゴリ管理ページ

import { createElement, clearChildren } from '../utils/dom.js';
import { api, ApiError } from '../api.js';
import { toast } from '../components/toast.js';

/**
 * カテゴリ作成フォームを構築する
 * @param {function(): Promise<void>} onCreated
 * @returns {HTMLElement}
 */
function buildCreateForm(onCreated) {
  const form = createElement('form', { className: 'category-create-form' });

  const nameInput = createElement('input', {
    className: 'category-create-input',
    type: 'text',
    placeholder: 'カテゴリ名を入力',
    autocomplete: 'off',
  });

  const sortInput = createElement('input', {
    className: 'category-sort-input',
    type: 'number',
    placeholder: '並び順',
    value: '0',
  });

  const submitBtn = createElement('button', {
    className: 'category-create-btn',
    type: 'submit',
    textContent: '追加',
  });

  form.appendChild(nameInput);
  form.appendChild(sortInput);
  form.appendChild(submitBtn);

  form.addEventListener('submit', async (e) => {
    e.preventDefault();
    const name = nameInput.value.trim();
    if (!name) {
      toast.warning('カテゴリ名を入力してください');
      return;
    }
    const sortOrder = parseInt(sortInput.value, 10) || 0;

    submitBtn.disabled = true;
    try {
      await api.post('/categories', { name, sortOrder });
      toast.success('カテゴリを作成しました');
      nameInput.value = '';
      sortInput.value = '0';
      await onCreated();
    } catch (err) {
      if (err instanceof ApiError) {
        toast.error(`カテゴリの作成に失敗しました (${err.status})`);
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
 * カテゴリ行を構築する（表示モード）
 * @param {object} category
 * @param {object[]} allCategories
 * @param {number} index
 * @param {function(): Promise<void>} onRefresh
 * @returns {HTMLElement}
 */
function buildCategoryRow(category, allCategories, index, onRefresh) {
  const row = createElement('div', { className: 'category-row' });

  const nameEl = createElement('span', {
    className: 'category-row__name',
    textContent: category.name,
  });

  const sortEl = createElement('span', {
    className: 'category-row__sort',
    textContent: `#${category.sortOrder}`,
  });

  // 上下移動ボタン
  const moveActions = createElement('div', { className: 'category-row__move' });

  const upBtn = createElement('button', {
    className: 'category-move-btn',
    textContent: '↑',
    type: 'button',
    title: '上に移動',
  });
  upBtn.disabled = index === 0;

  const downBtn = createElement('button', {
    className: 'category-move-btn',
    textContent: '↓',
    type: 'button',
    title: '下に移動',
  });
  downBtn.disabled = index === allCategories.length - 1;

  upBtn.addEventListener('click', async () => {
    if (index === 0) return;
    const prev = allCategories[index - 1];
    try {
      await api.put(`/categories/${category.id}`, { name: category.name, sortOrder: prev.sortOrder });
      await api.put(`/categories/${prev.id}`, { name: prev.name, sortOrder: category.sortOrder });
      await onRefresh();
    } catch (_err) {
      toast.error('並び順の変更に失敗しました');
    }
  });

  downBtn.addEventListener('click', async () => {
    if (index === allCategories.length - 1) return;
    const next = allCategories[index + 1];
    try {
      await api.put(`/categories/${category.id}`, { name: category.name, sortOrder: next.sortOrder });
      await api.put(`/categories/${next.id}`, { name: next.name, sortOrder: category.sortOrder });
      await onRefresh();
    } catch (_err) {
      toast.error('並び順の変更に失敗しました');
    }
  });

  moveActions.appendChild(upBtn);
  moveActions.appendChild(downBtn);

  // 編集・削除
  const actions = createElement('div', { className: 'category-row__actions' });

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
    enterEditMode(row, category, onRefresh);
  });

  deleteBtn.addEventListener('click', async () => {
    if (!confirm(`カテゴリ「${category.name}」を削除しますか？`)) return;
    try {
      await api.delete(`/categories/${category.id}`);
      toast.success('カテゴリを削除しました');
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

  row.appendChild(nameEl);
  row.appendChild(sortEl);
  row.appendChild(moveActions);
  row.appendChild(actions);

  return row;
}

/**
 * インライン編集モードに切り替える
 * @param {HTMLElement} row
 * @param {object} category
 * @param {function(): Promise<void>} onRefresh
 */
function enterEditMode(row, category, onRefresh) {
  clearChildren(row);
  row.classList.add('category-row--editing');

  const nameInput = createElement('input', {
    className: 'category-edit-input',
    type: 'text',
    value: category.name,
  });

  const sortInput = createElement('input', {
    className: 'category-sort-input',
    type: 'number',
    value: String(category.sortOrder),
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
      toast.warning('カテゴリ名を入力してください');
      return;
    }
    const newSort = parseInt(sortInput.value, 10) || 0;
    saveBtn.disabled = true;
    cancelBtn.disabled = true;
    try {
      await api.put(`/categories/${category.id}`, { name: newName, sortOrder: newSort });
      toast.success('カテゴリを更新しました');
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

  const editActions = createElement('div', { className: 'category-row__actions' });
  editActions.appendChild(saveBtn);
  editActions.appendChild(cancelBtn);

  row.appendChild(nameInput);
  row.appendChild(sortInput);
  row.appendChild(editActions);
}

/**
 * カテゴリ管理ページを描画する
 * @param {HTMLElement} container
 */
export async function renderCategoryManagePage(container) {
  const page = createElement('div', { className: 'category-manage-page' });

  const title = createElement('h1', { className: 'manage-title', textContent: 'カテゴリ管理' });
  page.appendChild(title);

  const listContainer = createElement('div', { className: 'category-list' });

  async function refreshList() {
    clearChildren(listContainer);
    try {
      const categories = await api.get('/categories');
      if (categories.length === 0) {
        const empty = createElement('p', {
          className: 'manage-empty',
          textContent: 'カテゴリがまだありません',
        });
        listContainer.appendChild(empty);
      } else {
        categories.forEach((cat, idx) => {
          listContainer.appendChild(buildCategoryRow(cat, categories, idx, refreshList));
        });
      }
    } catch (_err) {
      const errEl = createElement('p', {
        className: 'manage-error',
        textContent: 'カテゴリの取得に失敗しました',
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
