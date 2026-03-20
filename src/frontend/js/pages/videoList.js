// pages/videoList.js — 動画一覧ページ

import { createElement } from '../utils/dom.js';
import { api } from '../api.js';
import { createVideoCard } from '../components/videoCard.js';
import { navigateTo } from '../router.js';

const PAGE_SIZE = 20;

/** ソートオプション定義 */
const SORT_OPTIONS = [
  { value: 'createdAt_desc', label: '登録日時（新しい順）' },
  { value: 'createdAt_asc', label: '登録日時（古い順）' },
  { value: 'title_asc', label: 'タイトル（昇順）' },
  { value: 'title_desc', label: 'タイトル（降順）' },
];

/**
 * 動画リストをソートする
 * @param {object[]} videos
 * @param {string} sortBy - 'createdAt' | 'title'
 * @param {string} sortOrder - 'asc' | 'desc'
 * @returns {object[]}
 */
export function sortVideos(videos, sortBy, sortOrder) {
  return [...videos].sort((a, b) => {
    let cmp;
    if (sortBy === 'title') {
      cmp = (a.title || '').localeCompare(b.title || '', 'ja');
    } else {
      cmp = new Date(a.createdAt).getTime() - new Date(b.createdAt).getTime();
    }
    return sortOrder === 'asc' ? cmp : -cmp;
  });
}

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
 * ソートセレクトを生成する
 * @param {string} currentSort
 * @param {function(string): void} onChange
 * @returns {HTMLElement}
 */
function createSortControl(currentSort, onChange) {
  const wrapper = createElement('div', { className: 'video-list-sort' });

  const label = createElement('label', {
    className: 'video-list-sort__label',
    textContent: '並び順:',
  });
  label.setAttribute('for', 'video-sort-select');

  const select = createElement('select', {
    className: 'video-list-sort__select',
    id: 'video-sort-select',
  });

  SORT_OPTIONS.forEach(opt => {
    const option = createElement('option', { value: opt.value, textContent: opt.label });
    if (opt.value === currentSort) option.selected = true;
    select.appendChild(option);
  });

  select.addEventListener('change', () => onChange(select.value));

  wrapper.appendChild(label);
  wrapper.appendChild(select);

  return wrapper;
}

/**
 * 動画一覧ページを描画する
 * @param {HTMLElement} container
 */
export async function renderVideoListPage(container) {
  let allVideos = [];
  let currentPage = 1;
  let currentSort = 'createdAt_desc';

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

  function render() {
    const [sortBy, sortOrder] = currentSort.split('_');
    const sorted = sortVideos(allVideos, sortBy, sortOrder);

    const totalPages = Math.max(1, Math.ceil(sorted.length / PAGE_SIZE));
    if (currentPage > totalPages) currentPage = totalPages;

    const start = (currentPage - 1) * PAGE_SIZE;
    const pageVideos = sorted.slice(start, start + PAGE_SIZE);

    // コントロールバー再描画
    while (controls.firstChild) controls.removeChild(controls.firstChild);

    const countEl = createElement('span', {
      className: 'video-list-count',
      textContent: `${allVideos.length} 件`,
    });
    controls.appendChild(countEl);

    const sortControl = createSortControl(currentSort, (newSort) => {
      currentSort = newSort;
      currentPage = 1;
      render();
    });
    controls.appendChild(sortControl);

    // コンテンツ再描画
    while (content.firstChild) content.removeChild(content.firstChild);

    if (allVideos.length === 0) {
      content.appendChild(createEmptyState());
      return;
    }

    const grid = createElement('div', { className: 'video-grid' });
    pageVideos.forEach(video => {
      const card = createVideoCard(video, () => navigateTo(`/videos/${video.id}`));
      grid.appendChild(card);
    });
    content.appendChild(grid);

    if (totalPages > 1) {
      const pagination = createPagination(currentPage, totalPages, (newPage) => {
        currentPage = newPage;
        render();
        page.scrollIntoView({ behavior: 'smooth' });
      });
      content.appendChild(pagination);
    }
  }

  // ローディング表示
  const loadingEl = createElement('p', {
    className: 'video-list-loading',
    textContent: '読み込み中...',
  });
  content.appendChild(loadingEl);

  // データ取得（大きな pageSize で全件取得し client-side でソート・ページング）
  try {
    const result = await api.get('/videos?page=1&pageSize=1000');
    allVideos = result.items ?? [];
  } catch (_err) {
    while (content.firstChild) content.removeChild(content.firstChild);
    const errEl = createElement('p', {
      className: 'video-list-error',
      textContent: 'データの取得に失敗しました。再読み込みしてください。',
    });
    content.appendChild(errEl);
    return;
  }

  render();
}
