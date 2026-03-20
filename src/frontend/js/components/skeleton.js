// components/skeleton.js — ローディングスケルトンコンポーネント

import { createElement } from '../utils/dom.js';

/**
 * 動画カードのスケルトンを生成する
 * @returns {HTMLElement}
 */
export function createVideoCardSkeleton() {
  const card = createElement('div', { className: 'video-card-skeleton' });
  card.setAttribute('aria-hidden', 'true');

  const thumb = createElement('div', { className: 'video-card-skeleton__thumb' });
  const thumbShimmer = createElement('div', { className: 'skeleton' });
  thumb.appendChild(thumbShimmer);

  const body = createElement('div', { className: 'video-card-skeleton__body' });
  const titleLine1 = createElement('div', { className: 'skeleton video-card-skeleton__title' });
  const titleLine2 = createElement('div', { className: 'skeleton video-card-skeleton__title-short' });
  const tag = createElement('div', { className: 'skeleton video-card-skeleton__tag' });
  const date = createElement('div', { className: 'skeleton video-card-skeleton__date' });

  body.appendChild(titleLine1);
  body.appendChild(titleLine2);
  body.appendChild(tag);
  body.appendChild(date);

  card.appendChild(thumb);
  card.appendChild(body);

  return card;
}

/**
 * スケルトングリッドを生成する
 * @param {number} count - カード数
 * @returns {HTMLElement}
 */
export function createSkeletonGrid(count = 8) {
  const grid = createElement('div', { className: 'video-grid' });
  grid.setAttribute('aria-label', '読み込み中');
  grid.setAttribute('role', 'status');
  for (let i = 0; i < count; i++) {
    grid.appendChild(createVideoCardSkeleton());
  }
  return grid;
}
