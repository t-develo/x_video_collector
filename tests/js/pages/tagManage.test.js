import { describe, it, expect, vi, beforeEach } from 'vitest';
import { renderTagManagePage, getTagColorCss } from '../../../src/frontend/js/pages/tagManage.js';

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

describe('getTagColorCss', () => {
  it('Red (0) の CSS 値を返す', () => {
    expect(getTagColorCss(0)).toBe('#ff4757');
  });

  it('Cyan (4) の CSS 値を返す', () => {
    expect(getTagColorCss(4)).toBe('#00d4aa');
  });

  it('Gray (8) の CSS 値を返す', () => {
    expect(getTagColorCss(8)).toBe('#888888');
  });

  it('不明な値はグレーを返す', () => {
    expect(getTagColorCss(99)).toBe('#888888');
  });
});

describe('renderTagManagePage', () => {
  let container;

  beforeEach(() => {
    vi.clearAllMocks();
    document.body.innerHTML = '<div id="main"></div><div id="toast-container"></div>';
    container = document.getElementById('main');
  });

  it('タグ一覧が空の場合、空メッセージが表示される', async () => {
    const { api } = await import('../../../src/frontend/js/api.js');
    api.get.mockResolvedValue([]);

    await renderTagManagePage(container);

    expect(container.querySelector('.manage-empty')).not.toBeNull();
    expect(container.querySelector('.manage-empty').textContent).toContain('タグがまだありません');
  });

  it('タグ一覧にタグが表示される', async () => {
    const { api } = await import('../../../src/frontend/js/api.js');
    api.get.mockResolvedValue([
      { id: 'id-1', name: 'テスト', color: 0 },
      { id: 'id-2', name: 'サンプル', color: 4 },
    ]);

    await renderTagManagePage(container);

    const rows = container.querySelectorAll('.tag-row');
    expect(rows.length).toBe(2);
    expect(rows[0].querySelector('.tag-chip--colored').textContent).toBe('テスト');
    expect(rows[1].querySelector('.tag-chip--colored').textContent).toBe('サンプル');
  });

  it('作成フォームが表示される', async () => {
    const { api } = await import('../../../src/frontend/js/api.js');
    api.get.mockResolvedValue([]);

    await renderTagManagePage(container);

    expect(container.querySelector('.tag-create-form')).not.toBeNull();
    expect(container.querySelector('.tag-create-input')).not.toBeNull();
    expect(container.querySelector('.tag-color-selector')).not.toBeNull();
  });

  it('カラーセレクターに9色のスウォッチが表示される', async () => {
    const { api } = await import('../../../src/frontend/js/api.js');
    api.get.mockResolvedValue([]);

    await renderTagManagePage(container);

    const swatches = container.querySelectorAll('.tag-color-swatch');
    expect(swatches.length).toBe(9);
  });

  it('空のタグ名で送信すると警告トーストが表示される', async () => {
    const { api } = await import('../../../src/frontend/js/api.js');
    const { toast } = await import('../../../src/frontend/js/components/toast.js');
    api.get.mockResolvedValue([]);

    await renderTagManagePage(container);

    const form = container.querySelector('.tag-create-form');
    form.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));

    await new Promise(r => setTimeout(r, 0));

    expect(toast.warning).toHaveBeenCalledWith('タグ名を入力してください');
    expect(api.post).not.toHaveBeenCalled();
  });

  it('有効なタグ名で送信すると api.post が呼ばれる', async () => {
    const { api } = await import('../../../src/frontend/js/api.js');
    const { toast } = await import('../../../src/frontend/js/components/toast.js');
    api.get.mockResolvedValue([]);
    api.post.mockResolvedValue({ id: 'new-id', name: '新タグ', color: 8 });

    await renderTagManagePage(container);

    const input = container.querySelector('.tag-create-input');
    input.value = '新タグ';

    const form = container.querySelector('.tag-create-form');
    form.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));

    await new Promise(r => setTimeout(r, 0));

    expect(api.post).toHaveBeenCalledWith('/tags', { name: '新タグ', color: 8 });
    expect(toast.success).toHaveBeenCalled();
  });

  it('編集ボタンをクリックするとインライン編集モードになる', async () => {
    const { api } = await import('../../../src/frontend/js/api.js');
    api.get.mockResolvedValue([
      { id: 'id-1', name: 'テスト', color: 0 },
    ]);

    await renderTagManagePage(container);

    const editBtn = container.querySelector('.tag-action-btn');
    editBtn.click();

    expect(container.querySelector('.tag-edit-input')).not.toBeNull();
    expect(container.querySelector('.tag-edit-input').value).toBe('テスト');
  });

  it('削除ボタンをクリックすると確認後に api.delete が呼ばれる', async () => {
    const { api } = await import('../../../src/frontend/js/api.js');
    const { toast } = await import('../../../src/frontend/js/components/toast.js');
    api.get.mockResolvedValue([
      { id: 'id-1', name: 'テスト', color: 0 },
    ]);

    // confirm をモック
    vi.spyOn(window, 'confirm').mockReturnValue(true);
    api.delete.mockResolvedValue(null);

    await renderTagManagePage(container);

    const deleteBtn = container.querySelector('.tag-action-btn--danger');
    deleteBtn.click();

    await new Promise(r => setTimeout(r, 0));

    expect(api.delete).toHaveBeenCalledWith('/tags/id-1');
    expect(toast.success).toHaveBeenCalled();

    window.confirm.mockRestore();
  });

  it('削除をキャンセルすると api.delete が呼ばれない', async () => {
    const { api } = await import('../../../src/frontend/js/api.js');
    api.get.mockResolvedValue([
      { id: 'id-1', name: 'テスト', color: 0 },
    ]);

    vi.spyOn(window, 'confirm').mockReturnValue(false);

    await renderTagManagePage(container);

    const deleteBtn = container.querySelector('.tag-action-btn--danger');
    deleteBtn.click();

    await new Promise(r => setTimeout(r, 0));

    expect(api.delete).not.toHaveBeenCalled();

    window.confirm.mockRestore();
  });

  it('API エラー時にエラーメッセージが表示される', async () => {
    const { api } = await import('../../../src/frontend/js/api.js');
    api.get.mockRejectedValue(new Error('Network error'));

    await renderTagManagePage(container);

    expect(container.querySelector('.manage-error')).not.toBeNull();
    expect(container.querySelector('.manage-error').textContent).toContain('取得に失敗');
  });
});
