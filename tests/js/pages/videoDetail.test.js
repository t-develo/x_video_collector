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
  notes: null,
  failureReason: null,
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

/** api.get の標準モック（stream URL を含む） */
function mockApiGet(api, overrides = {}) {
  api.get.mockImplementation((path) => {
    if (path.endsWith('/stream')) return Promise.resolve({ streamUrl: 'https://blob.example.com/sas-token' });
    if (path.includes('/videos/')) return Promise.resolve(overrides.video ?? MOCK_VIDEO);
    if (path === '/tags') return Promise.resolve(overrides.tags ?? MOCK_TAGS);
    if (path === '/categories') return Promise.resolve(overrides.categories ?? MOCK_CATEGORIES);
    return Promise.reject(new Error('Unknown path'));
  });
}

describe('renderVideoDetailPage', () => {
  let container;

  beforeEach(() => {
    vi.clearAllMocks();
    document.body.innerHTML = '<div id="main"></div><div id="toast-container"></div>';
    container = document.getElementById('main');
  });

  it('動画情報が表示される', async () => {
    const { api } = await import('../../../src/frontend/js/api.js');
    mockApiGet(api);

    await renderVideoDetailPage(container, 'video-1');

    expect(container.querySelector('.detail-title-input').value).toBe('テスト動画');
    expect(container.querySelector('.detail-meta').textContent).toContain('@testuser');
  });

  it('戻るボタンが表示される', async () => {
    const { api } = await import('../../../src/frontend/js/api.js');
    mockApiGet(api);

    await renderVideoDetailPage(container, 'video-1');

    const backBtn = container.querySelector('.detail-back-btn');
    expect(backBtn).not.toBeNull();
    expect(backBtn.textContent).toContain('動画一覧に戻る');
  });

  it('戻るボタンをクリックすると動画一覧に遷移する', async () => {
    const { api } = await import('../../../src/frontend/js/api.js');
    const { navigateTo } = await import('../../../src/frontend/js/router.js');
    mockApiGet(api);

    await renderVideoDetailPage(container, 'video-1');

    container.querySelector('.detail-back-btn').click();
    expect(navigateTo).toHaveBeenCalledWith('/videos');
  });

  it('カテゴリドロップダウンが表示され現在のカテゴリが選択されている', async () => {
    const { api } = await import('../../../src/frontend/js/api.js');
    mockApiGet(api);

    await renderVideoDetailPage(container, 'video-1');

    const select = container.querySelector('.detail-category-select');
    expect(select).not.toBeNull();
    // 未設定 + 2カテゴリ = 3 options
    expect(select.options.length).toBe(3);
    expect(select.value).toBe('cat-1');
  });

  it('タグチップが全タグ分表示される', async () => {
    const { api } = await import('../../../src/frontend/js/api.js');
    mockApiGet(api);

    await renderVideoDetailPage(container, 'video-1');

    const chips = container.querySelectorAll('.detail-tag-chip');
    expect(chips.length).toBe(3);
  });

  it('動画に設定済みのタグが selected 状態になっている', async () => {
    const { api } = await import('../../../src/frontend/js/api.js');
    mockApiGet(api);

    await renderVideoDetailPage(container, 'video-1');

    const chips = container.querySelectorAll('.detail-tag-chip');
    // tag-1, tag-2 は selected、tag-3 は not selected
    expect(chips[0].classList.contains('detail-tag-chip--selected')).toBe(true);
    expect(chips[1].classList.contains('detail-tag-chip--selected')).toBe(true);
    expect(chips[2].classList.contains('detail-tag-chip--selected')).toBe(false);
  });

  it('タグチップをクリックすると選択状態がトグルされる', async () => {
    const { api } = await import('../../../src/frontend/js/api.js');
    mockApiGet(api);

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
    mockApiGet(api);
    api.put.mockResolvedValue({ ...MOCK_VIDEO });

    await renderVideoDetailPage(container, 'video-1');

    const saveBtn = container.querySelector('.detail-save-btn');
    saveBtn.click();

    await new Promise(r => setTimeout(r, 0));

    expect(api.put).toHaveBeenCalledWith('/videos/video-1', {
      title: 'テスト動画',
      categoryId: 'cat-1',
      tagIds: ['tag-1', 'tag-2'],
      notes: null,
    });
    expect(toast.success).toHaveBeenCalled();
  });

  it('タグを変更してから保存すると更新されたタグ ID が送信される', async () => {
    const { api } = await import('../../../src/frontend/js/api.js');
    mockApiGet(api);
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
    mockApiGet(api, { video: { ...MOCK_VIDEO, tags: [] }, tags: [] });

    await renderVideoDetailPage(container, 'video-1');

    expect(container.querySelector('.detail-empty-hint')).not.toBeNull();
    expect(container.querySelector('.detail-empty-hint').textContent).toContain('タグ管理ページ');
  });

  it('保存ボタンが表示される', async () => {
    const { api } = await import('../../../src/frontend/js/api.js');
    mockApiGet(api);

    await renderVideoDetailPage(container, 'video-1');

    const saveBtn = container.querySelector('.detail-save-btn');
    expect(saveBtn).not.toBeNull();
    expect(saveBtn.textContent).toBe('変更を保存');
  });

  // ========== 動画プレイヤー ==========

  it('blobPath がある場合に動画プレイヤーが表示される', async () => {
    const { api } = await import('../../../src/frontend/js/api.js');
    mockApiGet(api);

    await renderVideoDetailPage(container, 'video-1');

    const videoEl = container.querySelector('.detail-video-player');
    expect(videoEl).not.toBeNull();
    expect(videoEl.tagName.toLowerCase()).toBe('video');
    expect(videoEl.hasAttribute('controls')).toBe(true);
  });

  it('blobPath がない場合に動画プレイヤーが表示されない', async () => {
    const { api } = await import('../../../src/frontend/js/api.js');
    mockApiGet(api, { video: { ...MOCK_VIDEO, blobPath: null } });

    await renderVideoDetailPage(container, 'video-1');

    expect(container.querySelector('.detail-video-player')).toBeNull();
  });

  it('動画プレイヤーに SAS URL がセットされる', async () => {
    const { api } = await import('../../../src/frontend/js/api.js');
    mockApiGet(api);

    await renderVideoDetailPage(container, 'video-1');

    // stream API 呼び出しの非同期解決を待つ
    await new Promise(r => setTimeout(r, 0));

    const videoEl = container.querySelector('.detail-video-player');
    expect(videoEl.src).toContain('https://blob.example.com/sas-token');
  });

  it('ストリーム URL 取得失敗時にエラーメッセージが表示される', async () => {
    const { api } = await import('../../../src/frontend/js/api.js');
    api.get.mockImplementation((path) => {
      if (path.endsWith('/stream')) return Promise.reject(new Error('Stream error'));
      if (path.includes('/videos/')) return Promise.resolve(MOCK_VIDEO);
      if (path === '/tags') return Promise.resolve(MOCK_TAGS);
      if (path === '/categories') return Promise.resolve(MOCK_CATEGORIES);
      return Promise.reject(new Error('Unknown path'));
    });

    await renderVideoDetailPage(container, 'video-1');
    await new Promise(r => setTimeout(r, 0));

    const status = container.querySelector('.detail-player-status');
    expect(status).not.toBeNull();
    expect(status.textContent).toContain('読み込みに失敗');
  });

  // ========== ステータスバッジ ==========

  it('ステータスバッジが表示される', async () => {
    const { api } = await import('../../../src/frontend/js/api.js');
    mockApiGet(api);

    await renderVideoDetailPage(container, 'video-1');

    const badge = container.querySelector('.detail-status-badge');
    expect(badge).not.toBeNull();
    expect(badge.textContent).toBe('Ready');
    expect(badge.classList.contains('detail-status-badge--ready')).toBe(true);
  });

  // ========== 元ツイートリンク ==========

  it('元ツイートへのリンクが表示される', async () => {
    const { api } = await import('../../../src/frontend/js/api.js');
    mockApiGet(api);

    await renderVideoDetailPage(container, 'video-1');

    const link = container.querySelector('.detail-tweet-link');
    expect(link).not.toBeNull();
    expect(link.getAttribute('href')).toBe('https://x.com/user/status/123');
    expect(link.getAttribute('target')).toBe('_blank');
  });

  // ========== インラインタイトル編集 ==========

  it('タイトル入力フィールドに現在のタイトルが表示される', async () => {
    const { api } = await import('../../../src/frontend/js/api.js');
    mockApiGet(api);

    await renderVideoDetailPage(container, 'video-1');

    const titleInput = container.querySelector('.detail-title-input');
    expect(titleInput).not.toBeNull();
    expect(titleInput.value).toBe('テスト動画');
  });

  it('タイトルを変更してから保存すると新しいタイトルが送信される', async () => {
    const { api } = await import('../../../src/frontend/js/api.js');
    mockApiGet(api);
    api.put.mockResolvedValue({ ...MOCK_VIDEO });

    await renderVideoDetailPage(container, 'video-1');

    const titleInput = container.querySelector('.detail-title-input');
    titleInput.value = '新しいタイトル';

    const saveBtn = container.querySelector('.detail-save-btn');
    saveBtn.click();

    await new Promise(r => setTimeout(r, 0));

    const callArgs = api.put.mock.calls[0][1];
    expect(callArgs.title).toBe('新しいタイトル');
  });

  it('タイトルを空にして保存すると null が送信される', async () => {
    const { api } = await import('../../../src/frontend/js/api.js');
    mockApiGet(api);
    api.put.mockResolvedValue({ ...MOCK_VIDEO });

    await renderVideoDetailPage(container, 'video-1');

    const titleInput = container.querySelector('.detail-title-input');
    titleInput.value = '   ';

    const saveBtn = container.querySelector('.detail-save-btn');
    saveBtn.click();

    await new Promise(r => setTimeout(r, 0));

    const callArgs = api.put.mock.calls[0][1];
    expect(callArgs.title).toBeNull();
  });

  // ========== 削除フロー ==========

  it('削除ボタンが表示される', async () => {
    const { api } = await import('../../../src/frontend/js/api.js');
    mockApiGet(api);

    await renderVideoDetailPage(container, 'video-1');

    const deleteBtn = container.querySelector('.detail-delete-btn');
    expect(deleteBtn).not.toBeNull();
    expect(deleteBtn.textContent).toContain('削除');
  });

  it('削除ボタンをクリックすると確認モーダルが表示される', async () => {
    const { api } = await import('../../../src/frontend/js/api.js');
    mockApiGet(api);

    await renderVideoDetailPage(container, 'video-1');

    container.querySelector('.detail-delete-btn').click();

    const modal = container.querySelector('.detail-modal');
    expect(modal).not.toBeNull();
    expect(container.querySelector('.detail-modal__title').textContent).toContain('削除');
  });

  it('キャンセルボタンをクリックするとモーダルが閉じる', async () => {
    const { api } = await import('../../../src/frontend/js/api.js');
    mockApiGet(api);

    await renderVideoDetailPage(container, 'video-1');

    container.querySelector('.detail-delete-btn').click();
    expect(container.querySelector('.detail-modal-overlay')).not.toBeNull();

    container.querySelector('.detail-modal__cancel-btn').click();
    expect(container.querySelector('.detail-modal-overlay')).toBeNull();
  });

  it('確認ボタンをクリックすると api.delete が呼ばれ一覧へ遷移する', async () => {
    const { api } = await import('../../../src/frontend/js/api.js');
    const { toast } = await import('../../../src/frontend/js/components/toast.js');
    const { navigateTo } = await import('../../../src/frontend/js/router.js');
    mockApiGet(api);
    api.delete.mockResolvedValue(null);

    await renderVideoDetailPage(container, 'video-1');

    container.querySelector('.detail-delete-btn').click();
    container.querySelector('.detail-modal__confirm-btn').click();

    await new Promise(r => setTimeout(r, 0));

    expect(api.delete).toHaveBeenCalledWith('/videos/video-1');
    expect(toast.success).toHaveBeenCalled();
    expect(navigateTo).toHaveBeenCalledWith('/videos');
  });

  it('削除 API エラー時にエラートーストが表示される', async () => {
    const { api, ApiError } = await import('../../../src/frontend/js/api.js');
    const { toast } = await import('../../../src/frontend/js/components/toast.js');
    mockApiGet(api);
    api.delete.mockRejectedValue(new ApiError(500, 'Internal Server Error', 'error'));

    await renderVideoDetailPage(container, 'video-1');

    container.querySelector('.detail-delete-btn').click();
    container.querySelector('.detail-modal__confirm-btn').click();

    await new Promise(r => setTimeout(r, 0));

    expect(toast.error).toHaveBeenCalled();
    // モーダルはまだ表示されている
    expect(container.querySelector('.detail-modal-overlay')).not.toBeNull();
  });

  // ========== ステータスポーリング ==========

  it('Pending ステータスの動画はポーリングセクションが表示される', async () => {
    const { api } = await import('../../../src/frontend/js/api.js');
    const pendingVideo = { ...MOCK_VIDEO, status: 'Pending', blobPath: null };
    api.get.mockImplementation((path) => {
      if (path.includes('/videos/')) return Promise.resolve(pendingVideo);
      if (path === '/tags') return Promise.resolve(MOCK_TAGS);
      if (path === '/categories') return Promise.resolve(MOCK_CATEGORIES);
      return Promise.reject(new Error('Unknown path'));
    });

    await renderVideoDetailPage(container, 'video-1');

    expect(container.querySelector('.detail-pending-section')).not.toBeNull();
    expect(container.querySelector('.detail-pending-status').textContent).toContain('待機中');
    // プレイヤーと編集フォームは表示されない
    expect(container.querySelector('.detail-video-player')).toBeNull();
    expect(container.querySelector('.detail-save-btn')).toBeNull();
  });

  it('Downloading ステータスの動画はポーリングセクションが表示される', async () => {
    const { api } = await import('../../../src/frontend/js/api.js');
    const downloadingVideo = { ...MOCK_VIDEO, status: 'Downloading', blobPath: null };
    api.get.mockImplementation((path) => {
      if (path.includes('/videos/')) return Promise.resolve(downloadingVideo);
      if (path === '/tags') return Promise.resolve(MOCK_TAGS);
      if (path === '/categories') return Promise.resolve(MOCK_CATEGORIES);
      return Promise.reject(new Error('Unknown path'));
    });

    await renderVideoDetailPage(container, 'video-1');

    expect(container.querySelector('.detail-pending-section')).not.toBeNull();
    expect(container.querySelector('.detail-pending-status').textContent).toContain('ダウンロード中');
  });

  it('Processing ステータスの動画はポーリングセクションが表示される', async () => {
    const { api } = await import('../../../src/frontend/js/api.js');
    const processingVideo = { ...MOCK_VIDEO, status: 'Processing', blobPath: null };
    api.get.mockImplementation((path) => {
      if (path.includes('/videos/')) return Promise.resolve(processingVideo);
      if (path === '/tags') return Promise.resolve(MOCK_TAGS);
      if (path === '/categories') return Promise.resolve(MOCK_CATEGORIES);
      return Promise.reject(new Error('Unknown path'));
    });

    await renderVideoDetailPage(container, 'video-1');

    expect(container.querySelector('.detail-pending-section')).not.toBeNull();
    expect(container.querySelector('.detail-pending-status').textContent).toContain('処理中');
  });

  // ========== 再ダウンロード ==========

  it('Failed ステータスの動画は再ダウンロードボタンが表示される', async () => {
    const { api } = await import('../../../src/frontend/js/api.js');
    const failedVideo = { ...MOCK_VIDEO, status: 'Failed', blobPath: null };
    mockApiGet(api, { video: failedVideo });

    await renderVideoDetailPage(container, 'video-1');

    const retryBtn = container.querySelector('.detail-retry-btn');
    expect(retryBtn).not.toBeNull();
    expect(retryBtn.textContent).toContain('再ダウンロード');
  });

  it('Ready ステータスの動画は再ダウンロードボタンが表示されない', async () => {
    const { api } = await import('../../../src/frontend/js/api.js');
    mockApiGet(api);

    await renderVideoDetailPage(container, 'video-1');

    expect(container.querySelector('.detail-retry-btn')).toBeNull();
  });

  it('再ダウンロードボタンをクリックすると api.post が呼ばれる', async () => {
    const { api } = await import('../../../src/frontend/js/api.js');
    const { toast } = await import('../../../src/frontend/js/components/toast.js');
    const failedVideo = { ...MOCK_VIDEO, status: 'Failed', blobPath: null };
    mockApiGet(api, { video: failedVideo });
    api.post.mockResolvedValue(null);

    await renderVideoDetailPage(container, 'video-1');

    container.querySelector('.detail-retry-btn').click();
    await new Promise(r => setTimeout(r, 0));

    expect(api.post).toHaveBeenCalledWith('/videos/video-1/retry', {});
    expect(toast.success).toHaveBeenCalled();
  });

  it('再ダウンロード API エラー時にエラートーストが表示される', async () => {
    const { api, ApiError } = await import('../../../src/frontend/js/api.js');
    const { toast } = await import('../../../src/frontend/js/components/toast.js');
    const failedVideo = { ...MOCK_VIDEO, status: 'Failed', blobPath: null };
    mockApiGet(api, { video: failedVideo });
    api.post.mockRejectedValue(new ApiError(409, 'Conflict', 'error'));

    await renderVideoDetailPage(container, 'video-1');

    container.querySelector('.detail-retry-btn').click();
    await new Promise(r => setTimeout(r, 0));

    expect(toast.error).toHaveBeenCalled();
    // ボタンが再度有効化されている
    expect(container.querySelector('.detail-retry-btn').disabled).toBe(false);
  });

  // ========== メモ機能 ==========

  it('メモ入力欄が表示される', async () => {
    const { api } = await import('../../../src/frontend/js/api.js');
    mockApiGet(api);

    await renderVideoDetailPage(container, 'video-1');

    const textarea = container.querySelector('.detail-notes-textarea');
    expect(textarea).not.toBeNull();
  });

  it('既存のメモが textarea に表示される', async () => {
    const { api } = await import('../../../src/frontend/js/api.js');
    mockApiGet(api, { video: { ...MOCK_VIDEO, notes: '個人メモです' } });

    await renderVideoDetailPage(container, 'video-1');

    const textarea = container.querySelector('.detail-notes-textarea');
    expect(textarea.value).toBe('個人メモです');
  });

  it('メモが null の場合は textarea が空になる', async () => {
    const { api } = await import('../../../src/frontend/js/api.js');
    mockApiGet(api, { video: { ...MOCK_VIDEO, notes: null } });

    await renderVideoDetailPage(container, 'video-1');

    const textarea = container.querySelector('.detail-notes-textarea');
    expect(textarea.value).toBe('');
  });

  it('メモを入力して保存すると notes が送信される', async () => {
    const { api } = await import('../../../src/frontend/js/api.js');
    mockApiGet(api);
    api.put.mockResolvedValue({ ...MOCK_VIDEO });

    await renderVideoDetailPage(container, 'video-1');

    const textarea = container.querySelector('.detail-notes-textarea');
    textarea.value = 'これは私のメモです';

    container.querySelector('.detail-save-btn').click();
    await new Promise(r => setTimeout(r, 0));

    const callArgs = api.put.mock.calls[0][1];
    expect(callArgs.notes).toBe('これは私のメモです');
  });

  it('メモを空にして保存すると null が送信される', async () => {
    const { api } = await import('../../../src/frontend/js/api.js');
    mockApiGet(api, { video: { ...MOCK_VIDEO, notes: '既存のメモ' } });
    api.put.mockResolvedValue({ ...MOCK_VIDEO });

    await renderVideoDetailPage(container, 'video-1');

    const textarea = container.querySelector('.detail-notes-textarea');
    textarea.value = '   ';

    container.querySelector('.detail-save-btn').click();
    await new Promise(r => setTimeout(r, 0));

    const callArgs = api.put.mock.calls[0][1];
    expect(callArgs.notes).toBeNull();
  });

  // ========== 失敗理由表示 ==========

  it('Failed ステータスで failureReason がある場合は失敗理由が表示される', async () => {
    const { api } = await import('../../../src/frontend/js/api.js');
    const failedVideo = {
      ...MOCK_VIDEO,
      status: 'Failed',
      blobPath: null,
      failureReason: 'yt-dlp: ERROR: Unable to extract video',
    };
    mockApiGet(api, { video: failedVideo });

    await renderVideoDetailPage(container, 'video-1');

    const reason = container.querySelector('.detail-failure-reason');
    expect(reason).not.toBeNull();
    expect(reason.textContent).toBe('yt-dlp: ERROR: Unable to extract video');
  });

  it('Failed ステータスで failureReason が null の場合は失敗理由欄が表示されない', async () => {
    const { api } = await import('../../../src/frontend/js/api.js');
    const failedVideo = { ...MOCK_VIDEO, status: 'Failed', blobPath: null, failureReason: null };
    mockApiGet(api, { video: failedVideo });

    await renderVideoDetailPage(container, 'video-1');

    expect(container.querySelector('.detail-failure-reason')).toBeNull();
  });

  it('Ready ステータスでは失敗理由欄が表示されない', async () => {
    const { api } = await import('../../../src/frontend/js/api.js');
    mockApiGet(api);

    await renderVideoDetailPage(container, 'video-1');

    expect(container.querySelector('.detail-failure-reason')).toBeNull();
  });
});
