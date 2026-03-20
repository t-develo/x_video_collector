// pages/videoList.js — 動画一覧ページ

import { createElement, clearChildren } from '../utils/dom.js';
import { api } from '../api.js';
import { createVideoCard } from '../components/videoCard.js';
import { navigateTo } from '../router.js';

const PAGE_SIZE = 20;

/**
 * ページネーションコントロールを生成する
 * @param {number} page
 * @param {number} totalPages
 * @param {function(number): void} onPageChange
 * @returns {HTMLElement}
 */
export function createPagination(page, totalPages, onPageChange) {
  const nav = createElement('nav', { className: 'video-list-pagination' });
  nav.setAttribute('aria-label', 'ページネーション');

  const prevBtn = createElement('button', {
    className: 'pagination-btn',
    textContent: '← 前へ',
  });
  prevBtn.disabled = page <= 1;
  prevBtn.addEventListener('click', () => onPageChange(page - 1));

  const pageInfo = createElement('span', {
    className: 'pagination-info',
    textContent: `${page} / ${totalPages}`,
  });

  const nextBtn = createElement('button', {
    className: 'pagination-btn',
    textContent: '次へ →',
  });
  nextBtn.disabled = page >= totalPages;
  nextBtn.addEventListener('click', () => onPageChange(page + 1));

  nav.appendChild(prevBtn);
  nav.appendChild(pageInfo);
  nav.appendChild(nextBtn);

  return nav;
}

/**
 * 空状態UIを生成する
 * @returns {HTMLElement}
 */
function createEmptyState() {
  const empty = createElement('div', { className: 'video-list-empty' });

  const icon = createElement('div', { className: 'video-list-empty__icon', textContent: '📂' });

  const msg = createElement('p', {
    className: 'video-list-empty__message',
    textContent: '動画がまだ登録されていません',
  });

  const link = createElement('button', {
    className: 'video-list-empty__link',
    textContent: '+ 動画を登録する',
  });
  link.addEventListener('click', () => navigateTo('/register'));

  empty.appendChild(icon);
  empty.appendChild(msg);
  empty.appendChild(link);

  return empty;
}

/**
 * 動画一覧ページを描画する（サーバーサイドページング）
 * @param {HTMLElement} container
 */
export async function renderVideoListPage(container) {
  let currentPage = 1;
  let totalCount = 0;

  // ページ構造を構築
  const page = createElement('div', { className: 'video-list-page' });

  const header = createElement('div', { className: 'video-list-header' });
  const titleEl = createElement('h1', { className: 'video-list-title', textContent: '動画一覧' });
  header.appendChild(titleEl);

  const controls = createElement('div', { className: 'video-list-controls' });
  header.appendChild(controls);

  const content = createElement('div', { className: 'video-list-content' });

  page.appendChild(header);
  page.appendChild(content);
  container.appendChild(page);

  async function fetchAndRender(targetPage) {
    clearChildren(content);
    const loadingEl = createElement('p', {
      className: 'video-list-loading',
      textContent: '読み込み中...',
    });
    content.appendChild(loadingEl);

    try {
      const result = await api.get(`/videos?page=${targetPage}&pageSize=${PAGE_SIZE}`);
      const videos = result.items ?? [];
      totalCount = result.totalCount ?? 0;
      currentPage = result.page ?? targetPage;
      const totalPages = result.totalPages ?? Math.max(1, Math.ceil(totalCount / PAGE_SIZE));

      // コントロールバー更新
      clearChildren(controls);
      const countEl = createElement('span', {
        className: 'video-list-count',
        textContent: `${totalCount} 件`,
      });
      controls.appendChild(countEl);

      // コンテンツ描画
      clearChildren(content);

      if (videos.length === 0) {
        content.appendChild(createEmptyState());
        return;
      }

      const grid = createElement('div', { className: 'video-grid' });
      videos.forEach(video => {
        const card = createVideoCard(video, () => navigateTo(`/videos/${video.id}`));
        grid.appendChild(card);
      });
      content.appendChild(grid);

      if (totalPages > 1) {
        const pagination = createPagination(currentPage, totalPages, (newPage) => {
          fetchAndRender(newPage);
          page.scrollIntoView({ behavior: 'smooth' });
        });
        content.appendChild(pagination);
      }
    } catch (_err) {
      clearChildren(content);
      const errEl = createElement('p', {
        className: 'video-list-error',
        textContent: 'データの取得に失敗しました。再読み込みしてください。',
      });
      content.appendChild(errEl);
    }
  }

  await fetchAndRender(1);
}
