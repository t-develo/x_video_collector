import { describe, it, expect, vi, beforeEach } from 'vitest';
import { renderCategoryManagePage } from '../../../src/frontend/js/pages/categoryManage.js';

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

describe('renderCategoryManagePage', () => {
  let container;

  beforeEach(() => {
    vi.clearAllMocks();
    document.body.innerHTML = '<div id="main"></div><div id="toast-container"></div>';
    container = document.getElementById('main');
  });

  it('カテゴリ一覧が空の場合、空メッセージが表示される', async () => {
    const { api } = await import('../../../src/frontend/js/api.js');
    api.get.mockResolvedValue([]);

    await renderCategoryManagePage(container);

    expect(container.querySelector('.manage-empty')).not.toBeNull();
    expect(container.querySelector('.manage-empty').textContent).toContain('カテゴリがまだありません');
  });

  it('カテゴリ一覧にカテゴリが表示される', async () => {
    const { api } = await import('../../../src/frontend/js/api.js');
    api.get.mockResolvedValue([
      { id: 'id-1', name: 'お気に入り', sortOrder: 0 },
      { id: 'id-2', name: 'あとで見る', sortOrder: 1 },
    ]);

    await renderCategoryManagePage(container);

    const rows = container.querySelectorAll('.category-row');
    expect(rows.length).toBe(2);
    expect(rows[0].querySelector('.category-row__name').textContent).toBe('お気に入り');
    expect(rows[1].querySelector('.category-row__name').textContent).toBe('あとで見る');
  });

  it('作成フォームが表示される', async () => {
    const { api } = await import('../../../src/frontend/js/api.js');
    api.get.mockResolvedValue([]);

    await renderCategoryManagePage(container);

    expect(container.querySelector('.category-create-form')).not.toBeNull();
    expect(container.querySelector('.category-create-input')).not.toBeNull();
    expect(container.querySelector('.category-sort-input')).not.toBeNull();
  });

  it('空のカテゴリ名で送信すると警告トーストが表示される', async () => {
    const { api } = await import('../../../src/frontend/js/api.js');
    const { toast } = await import('../../../src/frontend/js/components/toast.js');
    api.get.mockResolvedValue([]);

    await renderCategoryManagePage(container);

    const form = container.querySelector('.category-create-form');
    form.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));

    await new Promise(r => setTimeout(r, 0));

    expect(toast.warning).toHaveBeenCalledWith('カテゴリ名を入力してください');
    expect(api.post).not.toHaveBeenCalled();
  });

  it('有効なカテゴリ名で送信すると api.post が呼ばれる', async () => {
    const { api } = await import('../../../src/frontend/js/api.js');
    const { toast } = await import('../../../src/frontend/js/components/toast.js');
    api.get.mockResolvedValue([]);
    api.post.mockResolvedValue({ id: 'new-id', name: '新カテゴリ', sortOrder: 0 });

    await renderCategoryManagePage(container);

    const nameInput = container.querySelector('.category-create-input');
    nameInput.value = '新カテゴリ';

    const form = container.querySelector('.category-create-form');
    form.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));

    await new Promise(r => setTimeout(r, 0));

    expect(api.post).toHaveBeenCalledWith('/categories', { name: '新カテゴリ', sortOrder: 0 });
    expect(toast.success).toHaveBeenCalled();
  });

  it('並び順を指定してカテゴリを作成できる', async () => {
    const { api } = await import('../../../src/frontend/js/api.js');
    api.get.mockResolvedValue([]);
    api.post.mockResolvedValue({ id: 'new-id', name: 'テスト', sortOrder: 5 });

    await renderCategoryManagePage(container);

    const nameInput = container.querySelector('.category-create-input');
    nameInput.value = 'テスト';
    const sortInput = container.querySelector('.category-sort-input');
    sortInput.value = '5';

    const form = container.querySelector('.category-create-form');
    form.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));

    await new Promise(r => setTimeout(r, 0));

    expect(api.post).toHaveBeenCalledWith('/categories', { name: 'テスト', sortOrder: 5 });
  });

  it('編集ボタンをクリックするとインライン編集モードになる', async () => {
    const { api } = await import('../../../src/frontend/js/api.js');
    api.get.mockResolvedValue([
      { id: 'id-1', name: 'お気に入り', sortOrder: 0 },
    ]);

    await renderCategoryManagePage(container);

    const editBtn = container.querySelector('.tag-action-btn');
    editBtn.click();

    expect(container.querySelector('.category-edit-input')).not.toBeNull();
    expect(container.querySelector('.category-edit-input').value).toBe('お気に入り');
  });

  it('削除ボタンをクリックすると確認後に api.delete が呼ばれる', async () => {
    const { api } = await import('../../../src/frontend/js/api.js');
    const { toast } = await import('../../../src/frontend/js/components/toast.js');
    api.get.mockResolvedValue([
      { id: 'id-1', name: 'お気に入り', sortOrder: 0 },
    ]);

    vi.spyOn(window, 'confirm').mockReturnValue(true);
    api.delete.mockResolvedValue(null);

    await renderCategoryManagePage(container);

    const deleteBtn = container.querySelector('.tag-action-btn--danger');
    deleteBtn.click();

    await new Promise(r => setTimeout(r, 0));

    expect(api.delete).toHaveBeenCalledWith('/categories/id-1');
    expect(toast.success).toHaveBeenCalled();

    window.confirm.mockRestore();
  });

  it('上下移動ボタンが表示される', async () => {
    const { api } = await import('../../../src/frontend/js/api.js');
    api.get.mockResolvedValue([
      { id: 'id-1', name: 'A', sortOrder: 0 },
      { id: 'id-2', name: 'B', sortOrder: 1 },
    ]);

    await renderCategoryManagePage(container);

    const moveBtns = container.querySelectorAll('.category-move-btn');
    expect(moveBtns.length).toBe(4); // 2 rows × 2 buttons each

    // 最初の行の上ボタンは disabled
    expect(moveBtns[0].disabled).toBe(true);
    // 最後の行の下ボタンは disabled
    expect(moveBtns[3].disabled).toBe(true);
  });

  it('API エラー時にエラーメッセージが表示される', async () => {
    const { api } = await import('../../../src/frontend/js/api.js');
    api.get.mockRejectedValue(new Error('Network error'));

    await renderCategoryManagePage(container);

    expect(container.querySelector('.manage-error')).not.toBeNull();
    expect(container.querySelector('.manage-error').textContent).toContain('取得に失敗');
  });
});
