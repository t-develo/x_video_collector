// components/videoCard.js — 動画カードコンポーネント

import { createElement } from '../utils/dom.js';
import { formatDate, formatDuration } from '../utils/format.js';
import { observeLazyImage } from '../utils/lazyLoad.js';

/**
 * ステータスバッジを生成する
 * @param {string} status
 * @returns {HTMLElement}
 */
function createStatusBadge(status) {
  const badge = createElement('span', { className: `status-badge status-badge--${status.toLowerCase()}` });
  const dot = createElement('span', { className: 'status-badge__dot' });
  const label = createElement('span', { className: 'status-badge__label', textContent: status });
  badge.appendChild(dot);
  badge.appendChild(label);
  return badge;
}

/**
 * タグチップ一覧を生成する
 * @param {{ name: string }[]} tags
 * @returns {HTMLElement}
 */
function createTagChips(tags) {
  const container = createElement('div', { className: 'video-card__tags' });
  const visible = tags.slice(0, 5);
  visible.forEach(tag => {
    const chip = createElement('span', { className: 'tag-chip', textContent: tag.name });
    container.appendChild(chip);
  });
  if (tags.length > 5) {
    const more = createElement('span', {
      className: 'tag-chip tag-chip--more',
      textContent: `+${tags.length - 5}`,
    });
    container.appendChild(more);
  }
  return container;
}

/**
 * サムネイルプレースホルダーを生成する
 * @returns {HTMLElement}
 */
function createThumbPlaceholder() {
  const ph = createElement('div', { className: 'video-card__thumb-placeholder' });
  const icon = createElement('span', { className: 'video-card__thumb-icon', textContent: '▶' });
  ph.appendChild(icon);
  return ph;
}

/**
 * 動画カードを生成する
 * @param {object} video - VideoListItemDto（camelCase）
 * @param {function} onClick
 * @returns {HTMLElement}
 */
export function createVideoCard(video, onClick) {
  const card = createElement('article', { className: 'video-card' });
  card.setAttribute('role', 'button');
  card.setAttribute('tabindex', '0');

  // サムネイル
  const thumb = createElement('div', { className: 'video-card__thumb' });
  if (video.thumbnailBlobPath) {
    const img = createElement('img', {
      className: 'video-card__thumb-img',
    });
    observeLazyImage(
      img,
      `/api/thumbnails/${video.id}`,
      video.title || 'サムネイル',
      () => {
        img.classList.add('video-card__thumb-img--hidden');
        thumb.appendChild(createThumbPlaceholder());
      },
    );
    thumb.appendChild(img);
  } else {
    thumb.appendChild(createThumbPlaceholder());
  }

  // 再生時間オーバーレイ
  if (video.durationSeconds != null) {
    const duration = createElement('span', {
      className: 'video-card__duration',
      textContent: formatDuration(video.durationSeconds),
    });
    thumb.appendChild(duration);
  }

  card.appendChild(thumb);

  // カード本文
  const body = createElement('div', { className: 'video-card__body' });

  // タイトル行（タイトル + ステータスバッジ）
  const titleRow = createElement('div', { className: 'video-card__title-row' });
  const title = createElement('h2', {
    className: 'video-card__title',
    textContent: video.title || '（タイトルなし）',
  });
  titleRow.appendChild(title);
  titleRow.appendChild(createStatusBadge(video.status));
  body.appendChild(titleRow);

  // タグ
  const tags = video.tags ?? [];
  if (tags.length > 0) {
    body.appendChild(createTagChips(tags));
  }

  // 登録日時
  const meta = createElement('div', { className: 'video-card__meta' });
  const dateEl = createElement('time', {
    className: 'video-card__date',
    datetime: video.createdAt,
    textContent: formatDate(video.createdAt),
  });
  meta.appendChild(dateEl);
  body.appendChild(meta);

  card.appendChild(body);

  // クリック・キーボード操作
  card.addEventListener('click', onClick);
  card.addEventListener('keydown', (e) => {
    if (e.key === 'Enter' || e.key === ' ') {
      e.preventDefault();
      onClick();
    }
  });

  return card;
}
