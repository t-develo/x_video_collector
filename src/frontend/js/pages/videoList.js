// pages/videoList.js — 動画一覧ページ（検索・フィルター・ソート対応）

import { createElement, clearChildren } from '../utils/dom.js';
import { api } from '../api.js';
import { createVideoCard } from '../components/videoCard.js';
import { createSearchBar } from '../components/searchBar.js';
import { createFilterPanel } from '../components/filterPanel.js';
import { createSkeletonGrid } from '../components/skeleton.js';
import { navigateTo, getCurrentQueryParams, setQueryParams } from '../router.js';

const PAGE_SIZE = 20;

/** ダウンロード中とみなすステータス */
const TRANSIENT_STATUSES = new Set(['Pending', 'Downloading', 'Processing']);

/** 自動リフレッシュ間隔（ミリ秒） */
const AUTO_REFRESH_INTERVAL_MS = 5000;

/** @typedef {'createdAt'|'title'} SortKey */
/** @typedef {'asc'|'desc'} SortDir */

/**
 * 動画配列をクライアントサイドでソートする（元配列を変更しない）
 * @param {Array<object>} videos
 * @param {SortKey} key
 * @param {SortDir} dir
 * @returns {Array<object>}
 */
export function sortVideos(videos, key, dir) {
  return [...videos].sort((a, b) => {
    let cmp;
    if (key === 'createdAt') {
      cmp = new Date(a.createdAt).getTime() - new Date(b.createdAt).getTime();
    } else {
      cmp = String(a[key] ?? '').localeCompare(String(b[key] ?? ''), 'ja');
    }
    return dir === 'desc' ? -cmp : cmp;
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
 * ダウンロード中の動画がある場合に自動リフレッシュをスケジュールする
 * @param {HTMLElement} pageEl - ページ要素（DOM 切り離しを検知するため）
 * @param {number} targetPage
 * @param {function(number): Promise<void>} fetchAndRender
 */
function scheduleAutoRefresh(pageEl, targetPage, fetchAndRender) {
  setTimeout(async () => {
    if (!pageEl.isConnected) return;
    await fetchAndRender(targetPage);
  }, AUTO_REFRESH_INTERVAL_MS);
}

/**
 * 空状態UIを生成する
 * @param {boolean} hasActiveSearch - 検索/フィルターが有効かどうか
 * @returns {HTMLElement}
 */
function createEmptyState(hasActiveSearch) {
  const empty = createElement('div', { className: 'video-list-empty' });

  const icon = createElement('div', {
    className: 'video-list-empty__icon',
    textContent: hasActiveSearch ? '🔍' : '📂',
  });

  const msg = createElement('p', {
    className: 'video-list-empty__message',
    textContent: hasActiveSearch
      ? '検索条件に一致する動画がありません'
      : '動画がまだ登録されていません',
  });

  empty.appendChild(icon);
  empty.appendChild(msg);

  if (!hasActiveSearch) {
    const link = createElement('button', {
      className: 'video-list-empty__link',
      textContent: '+ 動画を登録する',
    });
    link.addEventListener('click', () => navigateTo('/register'));
    empty.appendChild(link);
  }

  return empty;
}

/**
 * ソートセレクトを生成する
 * @param {SortKey} sortKey
 * @param {SortDir} sortDir
 * @param {function(SortKey, SortDir): void} onChange
 * @returns {HTMLElement}
 */
function createSortSelect(sortKey, sortDir, onChange) {
  const wrapper = createElement('div', { className: 'video-list-sort' });

  const label = createElement('label', {
    className: 'video-list-sort__label',
    textContent: '並び順:',
  });

  const select = createElement('select', { className: 'video-list-sort__select' });

  const options = [
    { value: 'createdAt:desc', text: '新しい順' },
    { value: 'createdAt:asc', text: '古い順' },
    { value: 'title:asc', text: 'タイトル昇順' },
    { value: 'title:desc', text: 'タイトル降順' },
  ];

  options.forEach(({ value, text }) => {
    const opt = createElement('option', { value, textContent: text });
    if (value === `${sortKey}:${sortDir}`) opt.selected = true;
    select.appendChild(opt);
  });

  select.addEventListener('change', () => {
    const [key, dir] = select.value.split(':');
    onChange(/** @type {SortKey} */ (key), /** @type {SortDir} */ (dir));
  });

  wrapper.appendChild(label);
  wrapper.appendChild(select);

  return wrapper;
}

/**
 * 検索・フィルター条件から API URL を構築する
 * @param {object} params
 * @param {string} params.keyword
 * @param {string|null} params.status
 * @param {string[]} params.tagIds
 * @param {string|null} params.categoryId
 * @param {number} params.page
 * @returns {string}
 */
function buildApiUrl({ keyword, status, tagIds, categoryId, page }) {
  const hasSearch = keyword || status || tagIds.length > 0 || categoryId;
  if (hasSearch) {
    const qs = new URLSearchParams();
    if (keyword) qs.set('q', keyword);
    if (status) qs.set('status', status);
    if (tagIds.length > 0) qs.set('tagIds', tagIds.join(','));
    if (categoryId) qs.set('categoryId', categoryId);
    qs.set('page', String(page));
    qs.set('pageSize', String(PAGE_SIZE));
    return `/videos/search?${qs.toString()}`;
  }
  return `/videos?page=${page}&pageSize=${PAGE_SIZE}`;
}

/**
 * 動画一覧ページを描画する（サーバーサイドページング + 検索/フィルター）
 * @param {HTMLElement} container
 */
export async function renderVideoListPage(container) {
  // URL クエリパラメータから初期状態を復元
  const qp = getCurrentQueryParams();
  let currentPage = parseInt(qp.get('page') ?? '1', 10) || 1;
  let currentKeyword = qp.get('q') ?? '';
  let currentStatus = qp.get('status') ?? null;
  let currentCategoryId = qp.get('categoryId') ?? null;
  const tagsParam = qp.get('tags');
  let currentTagIds = tagsParam ? tagsParam.split(',').filter(Boolean) : [];
  let currentSortKey = /** @type {SortKey} */ (qp.get('sort') ?? 'createdAt');
  let currentSortDir = /** @type {SortDir} */ (qp.get('dir') ?? 'desc');

  /** @type {Array<{id: string, name: string, color: string}>} */
  let allTags = [];
  /** @type {Array<{id: string, name: string}>} */
  let allCategories = [];

  // ページ構造を構築
  const pageEl = createElement('div', { className: 'video-list-page' });

  // ヘッダー
  const header = createElement('div', { className: 'video-list-header' });
  const titleEl = createElement('h1', { className: 'video-list-title', textContent: '動画一覧' });

  const topControls = createElement('div', { className: 'video-list-top-controls' });

  // 検索バー
  const searchBarEl = createSearchBar({
    initialValue: currentKeyword,
    onSearch: (keyword) => {
      currentKeyword = keyword;
      currentPage = 1;
      syncUrlAndFetch();
    },
  });

  // フィルター開閉トグル
  const filterToggleBtn = createElement('button', {
    className: 'filter-toggle-btn',
    type: 'button',
    textContent: 'フィルター ▼',
  });

  // フィルターパネルコンテナ
  const filterContainer = createElement('div', { className: 'filter-container' });
  filterContainer.hidden = true;

  filterToggleBtn.addEventListener('click', () => {
    filterContainer.hidden = !filterContainer.hidden;
    filterToggleBtn.textContent = filterContainer.hidden ? 'フィルター ▼' : 'フィルター ▲';
  });

  topControls.appendChild(searchBarEl);
  topControls.appendChild(filterToggleBtn);

  header.appendChild(titleEl);
  header.appendChild(topControls);

  // コントロールバー（件数・ソート）
  const controls = createElement('div', { className: 'video-list-controls' });

  // コンテンツエリア
  const content = createElement('div', { className: 'video-list-content' });

  pageEl.appendChild(header);
  pageEl.appendChild(filterContainer);
  pageEl.appendChild(controls);
  pageEl.appendChild(content);
  container.appendChild(pageEl);

  // タグ・カテゴリを取得してフィルターパネルを構築
  try {
    const [tagsResult, categoriesResult] = await Promise.all([
      api.get('/tags'),
      api.get('/categories'),
    ]);
    allTags = tagsResult ?? [];
    allCategories = categoriesResult ?? [];
  } catch (_err) {
    // タグ/カテゴリ取得失敗は無視（フィルターパネルを空で表示）
  }

  const filterPanelEl = createFilterPanel({
    tags: allTags,
    categories: allCategories,
    initialFilters: {
      tagIds: currentTagIds,
      categoryId: currentCategoryId,
      status: currentStatus,
    },
    onFilter: (filters) => {
      currentTagIds = filters.tagIds;
      currentCategoryId = filters.categoryId;
      currentStatus = filters.status;
      currentPage = 1;
      syncUrlAndFetch();
    },
  });
  filterContainer.appendChild(filterPanelEl);

  /**
   * URL クエリパラメータを更新してからデータをフェッチする
   */
  function syncUrlAndFetch() {
    setQueryParams({
      q: currentKeyword || null,
      status: currentStatus,
      categoryId: currentCategoryId,
      tags: currentTagIds.length > 0 ? currentTagIds.join(',') : null,
      sort: currentSortKey !== 'createdAt' ? currentSortKey : null,
      dir: currentSortDir !== 'desc' ? currentSortDir : null,
      page: currentPage > 1 ? String(currentPage) : null,
    });
    fetchAndRender(currentPage);
  }

  /**
   * データを取得して一覧を描画する
   * @param {number} targetPage
   */
  async function fetchAndRender(targetPage) {
    clearChildren(content);
    content.appendChild(createSkeletonGrid(8));

    try {
      const url = buildApiUrl({
        keyword: currentKeyword,
        status: currentStatus,
        tagIds: currentTagIds,
        categoryId: currentCategoryId,
        page: targetPage,
      });
      const result = await api.get(url);
      const videos = result.items ?? [];
      const totalCount = result.totalCount ?? 0;
      currentPage = result.page ?? targetPage;
      const totalPages = result.totalPages ?? Math.max(1, Math.ceil(totalCount / PAGE_SIZE));

      const hasActiveSearch = !!(currentKeyword || currentStatus || currentTagIds.length > 0 || currentCategoryId);

      // コントロールバー更新
      clearChildren(controls);
      const countEl = createElement('span', {
        className: 'video-list-count',
        textContent: `${totalCount} 件`,
      });
      const sortEl = createSortSelect(currentSortKey, currentSortDir, (key, dir) => {
        currentSortKey = key;
        currentSortDir = dir;
        syncUrlAndFetch();
      });
      controls.appendChild(countEl);
      controls.appendChild(sortEl);

      // コンテンツ描画
      clearChildren(content);

      if (videos.length === 0) {
        content.appendChild(createEmptyState(hasActiveSearch));
        return;
      }

      const sorted = sortVideos(videos, currentSortKey, currentSortDir);

      const grid = createElement('div', { className: 'video-grid' });
      sorted.forEach(video => {
        const card = createVideoCard(video, () => navigateTo(`/videos/${video.id}`));
        grid.appendChild(card);
      });
      content.appendChild(grid);

      if (totalPages > 1) {
        const pagination = createPagination(currentPage, totalPages, (newPage) => {
          currentPage = newPage;
          syncUrlAndFetch();
          pageEl.scrollIntoView({ behavior: 'smooth' });
        });
        content.appendChild(pagination);
      }

      // ダウンロード中の動画がある場合は自動リフレッシュ
      const hasTransient = videos.some(v => TRANSIENT_STATUSES.has(v.status));
      if (hasTransient) {
        scheduleAutoRefresh(pageEl, targetPage, fetchAndRender);
      }
    } catch (_err) {
      clearChildren(content);
      const errWrapper = createElement('div', { className: 'video-list-error' });
      const errMsg = createElement('p', {
        className: 'video-list-error__message',
        textContent: 'データの取得に失敗しました。',
      });
      const retryBtn = createElement('button', {
        className: 'video-list-error__retry-btn',
        type: 'button',
        textContent: '再試行',
      });
      retryBtn.addEventListener('click', () => fetchAndRender(targetPage));
      errWrapper.appendChild(errMsg);
      errWrapper.appendChild(retryBtn);
      content.appendChild(errWrapper);
    }
  }

  await fetchAndRender(currentPage);
}
