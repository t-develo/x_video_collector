import { describe, it, expect, vi, beforeEach } from 'vitest';
import { renderVideoListPage, createPagination, createSortSelect, createStatsBar, buildApiUrl } from '../../../src/frontend/js/pages/videoList.js';

// api モジュールをモック
vi.mock('../../../src/frontend/js/api.js', () => ({
  api: {
    get: vi.fn(),
    getStats: vi.fn(),
  },
}));

// videoCard コンポーネントをモック
vi.mock('../../../src/frontend/js/components/videoCard.js', () => ({
  createVideoCard: vi.fn((video, onClick) => {
    const el = document.createElement('article');
    el.className = 'video-card';
    el.dataset.videoId = video.id;
    el.addEventListener('click', onClick);
    return el;
  }),
}));

// router をモック（クエリパラメータ関数を含む）
vi.mock('../../../src/frontend/js/router.js', () => ({
  navigateTo: vi.fn(),
  getCurrentQueryParams: vi.fn(() => new URLSearchParams()),
  setQueryParams: vi.fn(),
}));

/** テスト用動画データを生成する */
function makeVideo(id, overrides = {}) {
  return {
    id,
    title: `動画 ${id}`,
    status: 'Ready',
    thumbnailBlobPath: null,
    durationSeconds: 60,
    tags: [],
    createdAt: `2026-01-${String(id).padStart(2, '0')}T00:00:00Z`,
    ...overrides,
  };
}

/** 指定件数の動画リストを生成する */
function makeVideos(count, startId = 1) {
  return Array.from({ length: count }, (_, i) => makeVideo(startId + i));
}

/** デフォルト統計オブジェクト */
const DEFAULT_STATS = {
  totalCount: 10,
  pendingCount: 1,
  downloadingCount: 0,
  processingCount: 0,
  readyCount: 8,
  failedCount: 1,
  totalFileSizeBytes: 1024 * 1024 * 100,
};

describe('buildApiUrl', () => {
  it('検索条件なしで /videos エンドポイントを返す', () => {
    const url = buildApiUrl({ keyword: '', status: null, tagIds: [], categoryId: null, page: 1, sortKey: 'createdAt', sortDir: 'desc' });
    expect(url).toContain('/videos?');
    expect(url).not.toContain('/search');
    expect(url).toContain('sortBy=createdAt');
    expect(url).toContain('sortDir=desc');
  });

  it('検索条件ありで /videos/search エンドポイントを返す', () => {
    const url = buildApiUrl({ keyword: 'test', status: null, tagIds: [], categoryId: null, page: 1, sortKey: 'title', sortDir: 'asc' });
    expect(url).toContain('/videos/search?');
    expect(url).toContain('q=test');
    expect(url).toContain('sortBy=title');
    expect(url).toContain('sortDir=asc');
  });

  it('タイトルソートのパラメータが正しく付与される', () => {
    const url = buildApiUrl({ keyword: '', status: null, tagIds: [], categoryId: null, page: 2, sortKey: 'title', sortDir: 'asc' });
    expect(url).toContain('sortBy=title');
    expect(url).toContain('sortDir=asc');
    expect(url).toContain('page=2');
  });
});

describe('createSortSelect', () => {
  it('ソートセレクトボックスが生成される', () => {
    const el = createSortSelect('createdAt', 'desc', () => {});
    expect(el.querySelector('select')).not.toBeNull();
    expect(el.querySelector('label')).not.toBeNull();
  });

  it('初期値が正しく選択される', () => {
    const el = createSortSelect('title', 'asc', () => {});
    const select = el.querySelector('select');
    expect(select.value).toBe('title:asc');
  });

  it('変更時に onChange コールバックが呼ばれる', () => {
    const onChange = vi.fn();
    const el = createSortSelect('createdAt', 'desc', onChange);
    const select = el.querySelector('select');
    select.value = 'title:asc';
    select.dispatchEvent(new Event('change'));
    expect(onChange).toHaveBeenCalledWith('title', 'asc');
  });
});

describe('createStatsBar', () => {
  it('統計バーが生成される', () => {
    const el = createStatsBar(DEFAULT_STATS);
    expect(el.classList.contains('stats-bar')).toBe(true);
  });

  it('合計件数が表示される', () => {
    const el = createStatsBar(DEFAULT_STATS);
    const values = el.querySelectorAll('.stats-bar__value');
    expect(values[0].textContent).toBe('10');
  });

  it('準備完了件数が表示される', () => {
    const el = createStatsBar(DEFAULT_STATS);
    const items = el.querySelectorAll('.stats-bar__item');
    const readyItem = Array.from(items).find(i => i.classList.contains('stats-bar__item--ready'));
    expect(readyItem.querySelector('.stats-bar__value').textContent).toBe('8');
  });

  it('エラー件数が表示される', () => {
    const el = createStatsBar(DEFAULT_STATS);
    const items = el.querySelectorAll('.stats-bar__item');
    const failedItem = Array.from(items).find(i => i.classList.contains('stats-bar__item--failed'));
    expect(failedItem.querySelector('.stats-bar__value').textContent).toBe('1');
  });
});

describe('createPagination', () => {
  it('前へ・次へボタンとページ情報が生成される', () => {
    const nav = createPagination(2, 5, () => {});
    expect(nav.querySelector('.pagination-btn')).not.toBeNull();
    const info = nav.querySelector('.pagination-info');
    expect(info.textContent).toBe('2 / 5');
  });

  it('1ページ目では前へボタンが無効', () => {
    const nav = createPagination(1, 5, () => {});
    const [prevBtn] = nav.querySelectorAll('.pagination-btn');
    expect(prevBtn.disabled).toBe(true);
  });

  it('最終ページでは次へボタンが無効', () => {
    const nav = createPagination(5, 5, () => {});
    const btns = nav.querySelectorAll('.pagination-btn');
    const nextBtn = btns[btns.length - 1];
    expect(nextBtn.disabled).toBe(true);
  });

  it('前へボタンクリックで onPageChange(page - 1) が呼ばれる', () => {
    const onChange = vi.fn();
    const nav = createPagination(3, 5, onChange);
    const [prevBtn] = nav.querySelectorAll('.pagination-btn');
    prevBtn.click();
    expect(onChange).toHaveBeenCalledWith(2);
  });

  it('次へボタンクリックで onPageChange(page + 1) が呼ばれる', () => {
    const onChange = vi.fn();
    const nav = createPagination(3, 5, onChange);
    const btns = nav.querySelectorAll('.pagination-btn');
    const nextBtn = btns[btns.length - 1];
    nextBtn.click();
    expect(onChange).toHaveBeenCalledWith(4);
  });
});

/**
 * api.get と api.getStats を URL に応じて適切な値を返すようにセットアップする
 */
async function setupApiMock(getStub, getStatsMock, videosResponse) {
  getStub.mockImplementation((url) => {
    if (url === '/tags') return Promise.resolve([]);
    if (url === '/categories') return Promise.resolve([]);
    return Promise.resolve(videosResponse);
  });
  getStatsMock.mockResolvedValue(DEFAULT_STATS);
}

describe('renderVideoListPage', () => {
  let container;

  beforeEach(async () => {
    vi.clearAllMocks();
    document.body.innerHTML = '<div id="main"></div>';
    container = document.getElementById('main');

    // getCurrentQueryParams のデフォルトモックを設定
    const { getCurrentQueryParams } = await import('../../../src/frontend/js/router.js');
    getCurrentQueryParams.mockReturnValue(new URLSearchParams());
  });

  it('動画がある場合にカードグリッドが表示される', async () => {
    const { api } = await import('../../../src/frontend/js/api.js');
    await setupApiMock(api.get, api.getStats, { items: makeVideos(3), totalCount: 3, page: 1, pageSize: 20 });

    await renderVideoListPage(container);

    const grid = container.querySelector('.video-grid');
    expect(grid).not.toBeNull();
    const cards = grid.querySelectorAll('.video-card');
    expect(cards.length).toBe(3);
  });

  it('動画がない場合に空状態が表示される', async () => {
    const { api } = await import('../../../src/frontend/js/api.js');
    await setupApiMock(api.get, api.getStats, { items: [], totalCount: 0, page: 1, pageSize: 20 });

    await renderVideoListPage(container);

    expect(container.querySelector('.video-list-empty')).not.toBeNull();
    expect(container.querySelector('.video-grid')).toBeNull();
  });

  it('20件以下ではページネーションが表示されない', async () => {
    const { api } = await import('../../../src/frontend/js/api.js');
    await setupApiMock(api.get, api.getStats, { items: makeVideos(10), totalCount: 10, page: 1, pageSize: 20 });

    await renderVideoListPage(container);

    expect(container.querySelector('.video-list-pagination')).toBeNull();
  });

  it('21件以上ではページネーションが表示される', async () => {
    const { api } = await import('../../../src/frontend/js/api.js');
    await setupApiMock(api.get, api.getStats, { items: makeVideos(25), totalCount: 25, page: 1, pageSize: 20 });

    await renderVideoListPage(container);

    expect(container.querySelector('.video-list-pagination')).not.toBeNull();
  });

  it('件数が表示される', async () => {
    const { api } = await import('../../../src/frontend/js/api.js');
    await setupApiMock(api.get, api.getStats, { items: makeVideos(5), totalCount: 5, page: 1, pageSize: 20 });

    await renderVideoListPage(container);

    const count = container.querySelector('.video-list-count');
    expect(count.textContent).toBe('5 件');
  });

  it('API エラー時にエラーメッセージが表示される', async () => {
    const { api } = await import('../../../src/frontend/js/api.js');
    api.getStats.mockResolvedValue(DEFAULT_STATS);
    api.get.mockImplementation((url) => {
      if (url === '/tags' || url === '/categories') return Promise.resolve([]);
      return Promise.reject(new Error('Network error'));
    });

    await renderVideoListPage(container);

    expect(container.querySelector('.video-list-error')).not.toBeNull();
    expect(container.querySelector('.video-grid')).toBeNull();
  });

  it('ソートセレクトが表示される', async () => {
    const { api } = await import('../../../src/frontend/js/api.js');
    await setupApiMock(api.get, api.getStats, { items: makeVideos(3), totalCount: 3, page: 1, pageSize: 20 });

    await renderVideoListPage(container);

    expect(container.querySelector('.video-list-sort__select')).not.toBeNull();
  });

  it('ページタイトルが表示される', async () => {
    const { api } = await import('../../../src/frontend/js/api.js');
    await setupApiMock(api.get, api.getStats, { items: [], totalCount: 0, page: 1, pageSize: 20 });

    await renderVideoListPage(container);

    const title = container.querySelector('.video-list-title');
    expect(title).not.toBeNull();
    expect(title.textContent).toBe('動画一覧');
  });

  it('API レスポンスの items が undefined でも空状態を表示する', async () => {
    const { api } = await import('../../../src/frontend/js/api.js');
    await setupApiMock(api.get, api.getStats, {});

    await renderVideoListPage(container);

    expect(container.querySelector('.video-list-empty')).not.toBeNull();
  });

  it('デフォルトソートで sortBy=createdAt&sortDir=desc を含む URL を呼び出す', async () => {
    const { api } = await import('../../../src/frontend/js/api.js');
    await setupApiMock(api.get, api.getStats, { items: [] });

    await renderVideoListPage(container);

    const videoCalls = api.get.mock.calls.filter(([url]) => url.startsWith('/videos'));
    const lastCall = videoCalls[videoCalls.length - 1][0];
    expect(lastCall).toContain('sortBy=createdAt');
    expect(lastCall).toContain('sortDir=desc');
  });

  it('統計バーが表示される', async () => {
    const { api } = await import('../../../src/frontend/js/api.js');
    await setupApiMock(api.get, api.getStats, { items: [], totalCount: 0, page: 1, pageSize: 20 });

    await renderVideoListPage(container);

    expect(container.querySelector('.stats-bar')).not.toBeNull();
  });

  it('検索バーが表示される', async () => {
    const { api } = await import('../../../src/frontend/js/api.js');
    await setupApiMock(api.get, api.getStats, { items: [] });

    await renderVideoListPage(container);

    expect(container.querySelector('.search-bar')).not.toBeNull();
    expect(container.querySelector('.search-bar__input')).not.toBeNull();
  });

  it('フィルタートグルボタンが表示される', async () => {
    const { api } = await import('../../../src/frontend/js/api.js');
    await setupApiMock(api.get, api.getStats, { items: [] });

    await renderVideoListPage(container);

    expect(container.querySelector('.filter-toggle-btn')).not.toBeNull();
  });
});
