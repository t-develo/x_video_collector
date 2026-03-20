import { describe, it, expect, vi, beforeEach } from 'vitest';
import { renderVideoDetailPage } from '../../../src/frontend/js/pages/videoDetail.js';

vi.mock('../../../src/frontend/js/api.js', () => ({
  api: {
    get: vi.fn(),
    post: vi.fn(),
    put: vi.fn(),
    delete: vi.fn(),
  },
  ApiError: class ApiError extends Error {
    constructor(status, statusText, body) {
      super(`API Error: ${status} ${statusText}`);
      this.name = 'ApiError';
      this.status = status;
      this.statusText = statusText;
      this.body = body;
    }
  },
}));

vi.mock('../../../src/frontend/js/components/toast.js', () => ({
  toast: {
    success: vi.fn(),
    error: vi.fn(),
    warning: vi.fn(),
    info: vi.fn(),
  },
}));

vi.mock('../../../src/frontend/js/router.js', () => ({
  navigateTo: vi.fn(),
  addRoute: vi.fn(),
  startRouter: vi.fn(),
  getCurrentPath: vi.fn(() => '/'),
}));

const MOCK_VIDEO = {
  id: 'video-1',
  tweetUrl: 'https://x.com/user/status/123',
  tweetId: '123',
  userName: 'testuser',
  title: 'テスト動画',
  status: 'Ready',
  blobPath: 'videos/video-1.mp4',
  thumbnailBlobPath: 'thumbnails/video-1.jpg',
  durationSeconds: 120,
  fileSizeBytes: 5242880,
  categoryId: 'cat-1',
  tags: [
    { id: 'tag-1', name: 'お気に入り', color: 0 },
    { id: 'tag-2', name: 'Music', color: 5 },
  ],
  createdAt: '2025-01-15T10:00:00Z',
  updatedAt: '2025-01-15T10:00:00Z',
};

const MOCK_TAGS = [
  { id: 'tag-1', name: 'お気に入り', color: 0 },
  { id: 'tag-2', name: 'Music', color: 5 },
  { id: 'tag-3', name: 'Game', color: 3 },
];

const MOCK_CATEGORIES = [
  { id: 'cat-1', name: 'エンタメ', sortOrder: 0 },
  { id: 'cat-2', name: '技術', sortOrder: 1 },
];

describe('renderVideoDetailPage', () => {
  let container;

  beforeEach(() => {
    vi.clearAllMocks();
    document.body.innerHTML = '<div id="main"></div><div id="toast-container"></div>';
    container = document.getElementById('main');
  });

  it('動画情報が表示される', async () => {
    const { api } = await import('../../../src/frontend/js/api.js');
    api.get.mockImplementation((path) => {
      if (path.includes('/videos/')) return Promise.resolve(MOCK_VIDEO);
      if (path === '/tags') return Promise.resolve(MOCK_TAGS);
      if (path === '/categories') return Promise.resolve(MOCK_CATEGORIES);
      return Promise.reject(new Error('Unknown path'));
    });

    await renderVideoDetailPage(container, 'video-1');

    expect(container.querySelector('.detail-title').textContent).toBe('テスト動画');
    expect(container.querySelector('.detail-meta').textContent).toContain('@testuser');
  });

  it('戻るボタンが表示される', async () => {
    const { api } = await import('../../../src/frontend/js/api.js');
    api.get.mockImplementation((path) => {
      if (path.includes('/videos/')) return Promise.resolve(MOCK_VIDEO);
      if (path === '/tags') return Promise.resolve(MOCK_TAGS);
      if (path === '/categories') return Promise.resolve(MOCK_CATEGORIES);
      return Promise.reject(new Error('Unknown path'));
    });

    await renderVideoDetailPage(container, 'video-1');

    const backBtn = container.querySelector('.detail-back-btn');
    expect(backBtn).not.toBeNull();
    expect(backBtn.textContent).toContain('動画一覧に戻る');
  });

  it('戻るボタンをクリックすると動画一覧に遷移する', async () => {
    const { api } = await import('../../../src/frontend/js/api.js');
    const { navigateTo } = await import('../../../src/frontend/js/router.js');
    api.get.mockImplementation((path) => {
      if (path.includes('/videos/')) return Promise.resolve(MOCK_VIDEO);
      if (path === '/tags') return Promise.resolve(MOCK_TAGS);
      if (path === '/categories') return Promise.resolve(MOCK_CATEGORIES);
      return Promise.reject(new Error('Unknown path'));
    });

    await renderVideoDetailPage(container, 'video-1');

    container.querySelector('.detail-back-btn').click();
    expect(navigateTo).toHaveBeenCalledWith('/videos');
  });

  it('カテゴリドロップダウンが表示され現在のカテゴリが選択されている', async () => {
    const { api } = await import('../../../src/frontend/js/api.js');
    api.get.mockImplementation((path) => {
      if (path.includes('/videos/')) return Promise.resolve(MOCK_VIDEO);
      if (path === '/tags') return Promise.resolve(MOCK_TAGS);
      if (path === '/categories') return Promise.resolve(MOCK_CATEGORIES);
      return Promise.reject(new Error('Unknown path'));
    });

    await renderVideoDetailPage(container, 'video-1');

    const select = container.querySelector('.detail-category-select');
    expect(select).not.toBeNull();
    // 未設定 + 2カテゴリ = 3 options
    expect(select.options.length).toBe(3);
    expect(select.value).toBe('cat-1');
  });

  it('タグチップが全タグ分表示される', async () => {
    const { api } = await import('../../../src/frontend/js/api.js');
    api.get.mockImplementation((path) => {
      if (path.includes('/videos/')) return Promise.resolve(MOCK_VIDEO);
      if (path === '/tags') return Promise.resolve(MOCK_TAGS);
      if (path === '/categories') return Promise.resolve(MOCK_CATEGORIES);
      return Promise.reject(new Error('Unknown path'));
    });

    await renderVideoDetailPage(container, 'video-1');

    const chips = container.querySelectorAll('.detail-tag-chip');
    expect(chips.length).toBe(3);
  });

  it('動画に設定済みのタグが selected 状態になっている', async () => {
    const { api } = await import('../../../src/frontend/js/api.js');
    api.get.mockImplementation((path) => {
      if (path.includes('/videos/')) return Promise.resolve(MOCK_VIDEO);
      if (path === '/tags') return Promise.resolve(MOCK_TAGS);
      if (path === '/categories') return Promise.resolve(MOCK_CATEGORIES);
      return Promise.reject(new Error('Unknown path'));
    });

    await renderVideoDetailPage(container, 'video-1');

    const chips = container.querySelectorAll('.detail-tag-chip');
    // tag-1, tag-2 は selected、tag-3 は not selected
    expect(chips[0].classList.contains('detail-tag-chip--selected')).toBe(true);
    expect(chips[1].classList.contains('detail-tag-chip--selected')).toBe(true);
    expect(chips[2].classList.contains('detail-tag-chip--selected')).toBe(false);
  });

  it('タグチップをクリックすると選択状態がトグルされる', async () => {
    const { api } = await import('../../../src/frontend/js/api.js');
    api.get.mockImplementation((path) => {
      if (path.includes('/videos/')) return Promise.resolve(MOCK_VIDEO);
      if (path === '/tags') return Promise.resolve(MOCK_TAGS);
      if (path === '/categories') return Promise.resolve(MOCK_CATEGORIES);
      return Promise.reject(new Error('Unknown path'));
    });

    await renderVideoDetailPage(container, 'video-1');

    const chips = container.querySelectorAll('.detail-tag-chip');

    // tag-3 (未選択) をクリック → 選択状態になる
    chips[2].click();
    expect(chips[2].classList.contains('detail-tag-chip--selected')).toBe(true);

    // tag-1 (選択済み) をクリック → 非選択状態になる
    chips[0].click();
    expect(chips[0].classList.contains('detail-tag-chip--selected')).toBe(false);
  });

  it('保存ボタンをクリックすると api.put が正しいデータで呼ばれる', async () => {
    const { api } = await import('../../../src/frontend/js/api.js');
    const { toast } = await import('../../../src/frontend/js/components/toast.js');
    api.get.mockImplementation((path) => {
      if (path.includes('/videos/')) return Promise.resolve(MOCK_VIDEO);
      if (path === '/tags') return Promise.resolve(MOCK_TAGS);
      if (path === '/categories') return Promise.resolve(MOCK_CATEGORIES);
      return Promise.reject(new Error('Unknown path'));
    });
    api.put.mockResolvedValue({ ...MOCK_VIDEO });

    await renderVideoDetailPage(container, 'video-1');

    const saveBtn = container.querySelector('.detail-save-btn');
    saveBtn.click();

    await new Promise(r => setTimeout(r, 0));

    expect(api.put).toHaveBeenCalledWith('/videos/video-1', {
      title: 'テスト動画',
      categoryId: 'cat-1',
      tagIds: ['tag-1', 'tag-2'],
    });
    expect(toast.success).toHaveBeenCalled();
  });

  it('タグを変更してから保存すると更新されたタグ ID が送信される', async () => {
    const { api } = await import('../../../src/frontend/js/api.js');
    api.get.mockImplementation((path) => {
      if (path.includes('/videos/')) return Promise.resolve(MOCK_VIDEO);
      if (path === '/tags') return Promise.resolve(MOCK_TAGS);
      if (path === '/categories') return Promise.resolve(MOCK_CATEGORIES);
      return Promise.reject(new Error('Unknown path'));
    });
    api.put.mockResolvedValue({ ...MOCK_VIDEO });

    await renderVideoDetailPage(container, 'video-1');

    // tag-1 を外す、tag-3 を追加
    const chips = container.querySelectorAll('.detail-tag-chip');
    chips[0].click(); // tag-1 deselect
    chips[2].click(); // tag-3 select

    const saveBtn = container.querySelector('.detail-save-btn');
    saveBtn.click();

    await new Promise(r => setTimeout(r, 0));

    const callArgs = api.put.mock.calls[0][1];
    expect(callArgs.tagIds).toContain('tag-2');
    expect(callArgs.tagIds).toContain('tag-3');
    expect(callArgs.tagIds).not.toContain('tag-1');
  });

  it('404 エラー時にエラーメッセージが表示される', async () => {
    const { api, ApiError } = await import('../../../src/frontend/js/api.js');
    api.get.mockRejectedValue(new ApiError(404, 'Not Found', 'Not found'));

    await renderVideoDetailPage(container, 'nonexistent');

    expect(container.querySelector('.manage-error').textContent).toContain('動画が見つかりません');
  });

  it('ネットワークエラー時にエラーメッセージが表示される', async () => {
    const { api } = await import('../../../src/frontend/js/api.js');
    api.get.mockRejectedValue(new Error('Network error'));

    await renderVideoDetailPage(container, 'video-1');

    expect(container.querySelector('.manage-error').textContent).toContain('取得に失敗');
  });

  it('タグが0件の場合にヒントメッセージが表示される', async () => {
    const { api } = await import('../../../src/frontend/js/api.js');
    api.get.mockImplementation((path) => {
      if (path.includes('/videos/')) return Promise.resolve({ ...MOCK_VIDEO, tags: [] });
      if (path === '/tags') return Promise.resolve([]);
      if (path === '/categories') return Promise.resolve(MOCK_CATEGORIES);
      return Promise.reject(new Error('Unknown path'));
    });

    await renderVideoDetailPage(container, 'video-1');

    expect(container.querySelector('.detail-empty-hint')).not.toBeNull();
    expect(container.querySelector('.detail-empty-hint').textContent).toContain('タグ管理ページ');
  });

  it('保存ボタンが表示される', async () => {
    const { api } = await import('../../../src/frontend/js/api.js');
    api.get.mockImplementation((path) => {
      if (path.includes('/videos/')) return Promise.resolve(MOCK_VIDEO);
      if (path === '/tags') return Promise.resolve(MOCK_TAGS);
      if (path === '/categories') return Promise.resolve(MOCK_CATEGORIES);
      return Promise.reject(new Error('Unknown path'));
    });

    await renderVideoDetailPage(container, 'video-1');

    const saveBtn = container.querySelector('.detail-save-btn');
    expect(saveBtn).not.toBeNull();
    expect(saveBtn.textContent).toBe('変更を保存');
  });
});
