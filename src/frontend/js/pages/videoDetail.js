// pages/videoDetail.js — 動画詳細ページ（プレイヤー・編集・削除）

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
 * 削除確認モーダルを表示する
 * @param {HTMLElement} page
 * @param {string} videoId
 */
function showDeleteModal(page, videoId) {
  const overlay = createElement('div', { className: 'detail-modal-overlay' });
  const modal = createElement('div', { className: 'detail-modal' });

  const modalTitle = createElement('h3', {
    className: 'detail-modal__title',
    textContent: '動画を削除しますか？',
  });

  const message = createElement('p', {
    className: 'detail-modal__message',
    textContent: 'この操作は取り消せません。動画ファイルとすべての情報が削除されます。',
  });

  const actions = createElement('div', { className: 'detail-modal__actions' });

  const cancelBtn = createElement('button', {
    className: 'detail-modal__cancel-btn',
    type: 'button',
    textContent: 'キャンセル',
  });

  cancelBtn.addEventListener('click', () => {
    page.removeChild(overlay);
  });

  const confirmBtn = createElement('button', {
    className: 'detail-modal__confirm-btn',
    type: 'button',
    textContent: '削除する',
  });

  confirmBtn.addEventListener('click', async () => {
    confirmBtn.disabled = true;
    cancelBtn.disabled = true;

    try {
      await api.delete(`/videos/${videoId}`);
      toast.success('動画を削除しました');
      navigateTo('/videos');
    } catch (err) {
      if (err instanceof ApiError) {
        toast.error(`削除に失敗しました (${err.status})`);
      } else {
        toast.error('ネットワークエラーが発生しました');
      }
      confirmBtn.disabled = false;
      cancelBtn.disabled = false;
    }
  });

  actions.appendChild(cancelBtn);
  actions.appendChild(confirmBtn);
  modal.appendChild(modalTitle);
  modal.appendChild(message);
  modal.appendChild(actions);
  overlay.appendChild(modal);
  page.appendChild(overlay);
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
  // 動画プレイヤーセクション（blobPath がある場合のみ）
  if (video.blobPath) {
    const playerSection = createElement('section', { className: 'detail-player-section' });

    const videoEl = createElement('video', {
      className: 'detail-video-player',
      controls: 'controls',
      preload: 'metadata',
    });

    const playerStatus = createElement('p', {
      className: 'detail-player-status',
      textContent: 'ストリームURLを取得中...',
    });

    playerSection.appendChild(videoEl);
    playerSection.appendChild(playerStatus);
    page.appendChild(playerSection);

    // SAS URL を非同期で取得して video src を設定
    (async () => {
      try {
        const data = await api.get(`/videos/${video.id}/stream`);
        videoEl.src = data.streamUrl;
        playerSection.removeChild(playerStatus);
      } catch {
        playerStatus.textContent = '動画の読み込みに失敗しました';
      }
    })();
  }

  // 動画情報セクション
  const infoSection = createElement('section', { className: 'detail-info' });

  // インライン編集可能タイトル
  const titleInput = createElement('input', {
    className: 'detail-title-input',
    type: 'text',
    placeholder: '（タイトルなし）',
  });
  titleInput.value = video.title || '';
  infoSection.appendChild(titleInput);

  const meta = createElement('div', { className: 'detail-meta' });

  const userName = createElement('span', {
    className: 'detail-meta__item',
    textContent: `@${video.userName}`,
  });
  meta.appendChild(userName);

  // ステータスバッジ
  const statusBadge = createElement('span', {
    className: `detail-status-badge detail-status-badge--${video.status.toLowerCase()}`,
    textContent: video.status,
  });
  meta.appendChild(statusBadge);

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

  // 元ツイートへのリンク
  const tweetLink = createElement('a', {
    className: 'detail-tweet-link',
    href: video.tweetUrl,
    target: '_blank',
    rel: 'noopener noreferrer',
    textContent: '元ツイートを開く ↗',
  });
  meta.appendChild(tweetLink);

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
    const title = titleInput.value.trim() || null;

    try {
      await api.put(`/videos/${video.id}`, {
        title,
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

  // 削除セクション
  const deleteSection = createElement('section', { className: 'detail-delete-section' });

  const deleteBtn = createElement('button', {
    className: 'detail-delete-btn',
    type: 'button',
    textContent: 'この動画を削除',
  });

  deleteBtn.addEventListener('click', () => {
    showDeleteModal(page, video.id);
  });

  deleteSection.appendChild(deleteBtn);
  page.appendChild(deleteSection);
}
