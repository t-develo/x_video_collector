import { describe, it, expect, vi, beforeEach } from 'vitest';
import { renderVideoListPage, sortVideos, createPagination } from '../../../src/frontend/js/pages/videoList.js';

// api モジュールをモック
vi.mock('../../../src/frontend/js/api.js', () => ({
  api: {
    get: vi.fn(),
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

// router をモック
vi.mock('../../../src/frontend/js/router.js', () => ({
  navigateTo: vi.fn(),
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

describe('sortVideos', () => {
  const videos = [
    makeVideo(1, { title: 'ぜんぶ', createdAt: '2026-01-01T00:00:00Z' }),
    makeVideo(2, { title: 'あいう', createdAt: '2026-01-03T00:00:00Z' }),
    makeVideo(3, { title: 'まみむ', createdAt: '2026-01-02T00:00:00Z' }),
  ];

  it('createdAt 降順（新しい順）でソートされる', () => {
    const result = sortVideos(videos, 'createdAt', 'desc');
    expect(result[0].id).toBe(2);
    expect(result[1].id).toBe(3);
    expect(result[2].id).toBe(1);
  });

  it('createdAt 昇順（古い順）でソートされる', () => {
    const result = sortVideos(videos, 'createdAt', 'asc');
    expect(result[0].id).toBe(1);
    expect(result[1].id).toBe(3);
    expect(result[2].id).toBe(2);
  });

  it('title 昇順でソートされる', () => {
    const result = sortVideos(videos, 'title', 'asc');
    // 日本語ロケール順: あいう < ぜんぶ < まみむ
    expect(result[0].title).toBe('あいう');
    expect(result[1].title).toBe('ぜんぶ');
    expect(result[2].title).toBe('まみむ');
  });

  it('title 降順でソートされる', () => {
    const result = sortVideos(videos, 'title', 'desc');
    // 降順: まみむ > ぜんぶ > あいう
    expect(result[0].title).toBe('まみむ');
  });

  it('元の配列を変更しない', () => {
    const original = [...videos];
    sortVideos(videos, 'title', 'asc');
    expect(videos).toEqual(original);
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

describe('renderVideoListPage', () => {
  let container;

  beforeEach(async () => {
    vi.clearAllMocks();
    document.body.innerHTML = '<div id="main"></div>';
    container = document.getElementById('main');
  });

  it('動画がある場合にカードグリッドが表示される', async () => {
    const { api } = await import('../../../src/frontend/js/api.js');
    api.get.mockResolvedValue({ items: makeVideos(3), totalCount: 3, page: 1, pageSize: 20 });

    await renderVideoListPage(container);

    const grid = container.querySelector('.video-grid');
    expect(grid).not.toBeNull();
    const cards = grid.querySelectorAll('.video-card');
    expect(cards.length).toBe(3);
  });

  it('動画がない場合に空状態が表示される', async () => {
    const { api } = await import('../../../src/frontend/js/api.js');
    api.get.mockResolvedValue({ items: [], totalCount: 0, page: 1, pageSize: 20 });

    await renderVideoListPage(container);

    expect(container.querySelector('.video-list-empty')).not.toBeNull();
    expect(container.querySelector('.video-grid')).toBeNull();
  });

  it('20件以下ではページネーションが表示されない', async () => {
    const { api } = await import('../../../src/frontend/js/api.js');
    api.get.mockResolvedValue({ items: makeVideos(10), totalCount: 10, page: 1, pageSize: 20 });

    await renderVideoListPage(container);

    expect(container.querySelector('.video-list-pagination')).toBeNull();
  });

  it('21件以上ではページネーションが表示される', async () => {
    const { api } = await import('../../../src/frontend/js/api.js');
    api.get.mockResolvedValue({ items: makeVideos(25), totalCount: 25, page: 1, pageSize: 20 });

    await renderVideoListPage(container);

    expect(container.querySelector('.video-list-pagination')).not.toBeNull();
  });

  it('件数が表示される', async () => {
    const { api } = await import('../../../src/frontend/js/api.js');
    api.get.mockResolvedValue({ items: makeVideos(5), totalCount: 5, page: 1, pageSize: 20 });

    await renderVideoListPage(container);

    const count = container.querySelector('.video-list-count');
    expect(count.textContent).toBe('5 件');
  });

  it('API エラー時にエラーメッセージが表示される', async () => {
    const { api } = await import('../../../src/frontend/js/api.js');
    api.get.mockRejectedValue(new Error('Network error'));

    await renderVideoListPage(container);

    expect(container.querySelector('.video-list-error')).not.toBeNull();
    expect(container.querySelector('.video-grid')).toBeNull();
  });

  it('ソートセレクトが表示される', async () => {
    const { api } = await import('../../../src/frontend/js/api.js');
    api.get.mockResolvedValue({ items: makeVideos(3), totalCount: 3, page: 1, pageSize: 20 });

    await renderVideoListPage(container);

    expect(container.querySelector('.video-list-sort__select')).not.toBeNull();
  });

  it('ページタイトルが表示される', async () => {
    const { api } = await import('../../../src/frontend/js/api.js');
    api.get.mockResolvedValue({ items: [], totalCount: 0, page: 1, pageSize: 20 });

    await renderVideoListPage(container);

    const title = container.querySelector('.video-list-title');
    expect(title).not.toBeNull();
    expect(title.textContent).toBe('動画一覧');
  });

  it('API レスポンスの items が undefined でも空状態を表示する', async () => {
    const { api } = await import('../../../src/frontend/js/api.js');
    api.get.mockResolvedValue({});

    await renderVideoListPage(container);

    expect(container.querySelector('.video-list-empty')).not.toBeNull();
  });

  it('/videos?page=1&pageSize=1000 エンドポイントを呼び出す', async () => {
    const { api } = await import('../../../src/frontend/js/api.js');
    api.get.mockResolvedValue({ items: [] });

    await renderVideoListPage(container);

    expect(api.get).toHaveBeenCalledWith('/videos?page=1&pageSize=1000');
  });
});
