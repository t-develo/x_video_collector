// pages/videoDetail.js — 動画詳細ページ（タグ・カテゴリ設定）

import { createElement, clearChildren } from '../utils/dom.js';
import { api, ApiError } from '../api.js';
import { toast } from '../components/toast.js';
import { navigateTo } from '../router.js';
import { formatDate, formatDuration, formatFileSize } from '../utils/format.js';

/**
 * タグチップ（選択可能）を生成する
 * @param {object} tag
 * @param {boolean} selected
 * @param {function(object, boolean): void} onToggle
 * @returns {HTMLElement}
 */
function createSelectableTagChip(tag, selected, onToggle) {
  const chip = createElement('button', {
    className: `detail-tag-chip${selected ? ' detail-tag-chip--selected' : ''}`,
    type: 'button',
  });
  chip.dataset.tagId = tag.id;
  chip.dataset.color = String(tag.color);
  chip.textContent = tag.name;

  chip.addEventListener('click', () => {
    const isNowSelected = !chip.classList.contains('detail-tag-chip--selected');
    chip.classList.toggle('detail-tag-chip--selected', isNowSelected);
    onToggle(tag, isNowSelected);
  });

  return chip;
}

/**
 * 動画詳細ページを描画する
 * @param {HTMLElement} container
 * @param {string} videoId
 */
export async function renderVideoDetailPage(container, videoId) {
  const page = createElement('div', { className: 'video-detail-page' });

  const backBtn = createElement('button', {
    className: 'detail-back-btn',
    textContent: '← 動画一覧に戻る',
    type: 'button',
  });
  backBtn.addEventListener('click', () => navigateTo('/videos'));
  page.appendChild(backBtn);

  const loading = createElement('p', {
    className: 'video-list-loading',
    textContent: '読み込み中...',
  });
  page.appendChild(loading);
  container.appendChild(page);

  try {
    const [video, allTags, allCategories] = await Promise.all([
      api.get(`/videos/${videoId}`),
      api.get('/tags'),
      api.get('/categories'),
    ]);

    page.removeChild(loading);
    renderDetailContent(page, video, allTags, allCategories);
  } catch (err) {
    page.removeChild(loading);
    if (err instanceof ApiError && err.status === 404) {
      const errEl = createElement('p', {
        className: 'manage-error',
        textContent: '動画が見つかりません',
      });
      page.appendChild(errEl);
    } else {
      const errEl = createElement('p', {
        className: 'manage-error',
        textContent: 'データの取得に失敗しました',
      });
      page.appendChild(errEl);
    }
  }
}

/**
 * 詳細コンテンツを描画する
 * @param {HTMLElement} page
 * @param {object} video
 * @param {object[]} allTags
 * @param {object[]} allCategories
 */
function renderDetailContent(page, video, allTags, allCategories) {
  // 動画情報セクション
  const infoSection = createElement('section', { className: 'detail-info' });

  const title = createElement('h1', {
    className: 'detail-title',
    textContent: video.title || '（タイトルなし）',
  });
  infoSection.appendChild(title);

  const meta = createElement('div', { className: 'detail-meta' });
  const userName = createElement('span', {
    className: 'detail-meta__item',
    textContent: `@${video.userName}`,
  });
  meta.appendChild(userName);

  if (video.durationSeconds != null) {
    const dur = createElement('span', {
      className: 'detail-meta__item',
      textContent: formatDuration(video.durationSeconds),
    });
    meta.appendChild(dur);
  }
  if (video.fileSizeBytes != null) {
    const size = createElement('span', {
      className: 'detail-meta__item',
      textContent: formatFileSize(video.fileSizeBytes),
    });
    meta.appendChild(size);
  }
  const date = createElement('time', {
    className: 'detail-meta__item',
    datetime: video.createdAt,
    textContent: formatDate(video.createdAt),
  });
  meta.appendChild(date);
  infoSection.appendChild(meta);
  page.appendChild(infoSection);

  // カテゴリ選択セクション
  const categorySection = createElement('section', { className: 'detail-section' });
  const categoryLabel = createElement('h2', {
    className: 'detail-section__title',
    textContent: 'カテゴリ',
  });
  categorySection.appendChild(categoryLabel);

  const categorySelect = createElement('select', { className: 'detail-category-select' });
  const emptyOption = createElement('option', { value: '', textContent: '（未設定）' });
  categorySelect.appendChild(emptyOption);

  allCategories.forEach(cat => {
    const option = createElement('option', {
      value: cat.id,
      textContent: cat.name,
    });
    if (video.categoryId === cat.id) {
      option.selected = true;
    }
    categorySelect.appendChild(option);
  });

  categorySection.appendChild(categorySelect);
  page.appendChild(categorySection);

  // タグ選択セクション
  const tagSection = createElement('section', { className: 'detail-section' });
  const tagLabel = createElement('h2', {
    className: 'detail-section__title',
    textContent: 'タグ',
  });
  tagSection.appendChild(tagLabel);

  const selectedTagIds = new Set(video.tags.map(t => t.id));
  const tagContainer = createElement('div', { className: 'detail-tag-list' });

  allTags.forEach(tag => {
    const chip = createSelectableTagChip(tag, selectedTagIds.has(tag.id), (t, isSelected) => {
      if (isSelected) {
        selectedTagIds.add(t.id);
      } else {
        selectedTagIds.delete(t.id);
      }
    });
    tagContainer.appendChild(chip);
  });

  if (allTags.length === 0) {
    const noTags = createElement('p', {
      className: 'detail-empty-hint',
      textContent: 'タグがまだありません。タグ管理ページで作成してください。',
    });
    tagSection.appendChild(noTags);
  }

  tagSection.appendChild(tagContainer);
  page.appendChild(tagSection);

  // 保存ボタン
  const saveBtn = createElement('button', {
    className: 'detail-save-btn',
    type: 'button',
    textContent: '変更を保存',
  });

  saveBtn.addEventListener('click', async () => {
    saveBtn.disabled = true;
    const categoryId = categorySelect.value || null;
    const tagIds = Array.from(selectedTagIds);

    try {
      await api.put(`/videos/${video.id}`, {
        title: video.title,
        categoryId,
        tagIds,
      });
      toast.success('動画情報を更新しました');
    } catch (err) {
      if (err instanceof ApiError) {
        toast.error(`更新に失敗しました (${err.status})`);
      } else {
        toast.error('ネットワークエラーが発生しました');
      }
    } finally {
      saveBtn.disabled = false;
    }
  });

  page.appendChild(saveBtn);
}
