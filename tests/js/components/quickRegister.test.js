import { describe, it, expect, vi, beforeEach } from 'vitest';
import { createQuickRegisterForm } from '../../../src/frontend/js/components/quickRegister.js';
import { api, ApiError } from '../../../src/frontend/js/api.js';
import { toast } from '../../../src/frontend/js/components/toast.js';

// api モジュールをモック
vi.mock('../../../src/frontend/js/api.js', () => ({
  api: {
    post: vi.fn(),
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

// toast モジュールをモック
vi.mock('../../../src/frontend/js/components/toast.js', () => ({
  toast: {
    success: vi.fn(),
    error: vi.fn(),
    warning: vi.fn(),
    info: vi.fn(),
  },
}));

// router モジュールをモック（register.js が依存）
vi.mock('../../../src/frontend/js/router.js', () => ({
  navigateTo: vi.fn(),
}));

const VALID_URL = 'https://x.com/user/status/1234567890';

/** フォームに URL を入力して送信し、非同期処理の完了を待つ */
async function submitWith(form, value) {
  form.querySelector('.quick-register__input').value = value;
  form.dispatchEvent(new Event('submit', { cancelable: true }));
  await vi.waitFor(() => {
    expect(form.querySelector('.quick-register__btn').disabled).toBe(false);
  });
}

describe('createQuickRegisterForm', () => {
  let container;

  beforeEach(() => {
    vi.clearAllMocks();
    container = document.createElement('div');
    document.body.appendChild(container);
  });

  it('入力欄と登録ボタンを持つフォームが生成される', () => {
    const form = createQuickRegisterForm();

    expect(form.tagName).toBe('FORM');
    expect(form.querySelector('input.quick-register__input')).not.toBeNull();
    expect(form.querySelector('button.quick-register__btn[type="submit"]')).not.toBeNull();
  });

  it('入力欄にラベルが関連付けられている', () => {
    const form = createQuickRegisterForm();

    const label = form.querySelector('label');
    const input = form.querySelector('input');
    expect(label.getAttribute('for')).toBe(input.id);
  });

  it('空 URL で送信するとエラーが表示され api.post は呼ばれない', async () => {
    const form = createQuickRegisterForm();
    container.appendChild(form);

    await submitWith(form, '');

    expect(form.querySelector('.quick-register__error').textContent).not.toBe('');
    expect(api.post).not.toHaveBeenCalled();
  });

  it('不正な URL で送信するとエラーが表示され api.post は呼ばれない', async () => {
    const form = createQuickRegisterForm();
    container.appendChild(form);

    await submitWith(form, 'https://example.com/foo');

    expect(form.querySelector('.quick-register__input').getAttribute('aria-invalid')).toBe('true');
    expect(api.post).not.toHaveBeenCalled();
  });

  it('有効な URL で送信すると登録され、入力欄が空になり onRegistered が呼ばれる', async () => {
    api.post.mockResolvedValue({ id: 'abc' });
    const onRegistered = vi.fn();
    const form = createQuickRegisterForm({ onRegistered });
    container.appendChild(form);

    await submitWith(form, `  ${VALID_URL}  `);

    expect(api.post).toHaveBeenCalledWith('/videos', { tweetUrl: VALID_URL });
    expect(toast.success).toHaveBeenCalledTimes(1);
    expect(form.querySelector('.quick-register__input').value).toBe('');
    expect(onRegistered).toHaveBeenCalledTimes(1);
  });

  it('送信中はボタンと入力欄が無効になる', async () => {
    let resolvePost;
    api.post.mockReturnValue(new Promise((resolve) => { resolvePost = resolve; }));
    const form = createQuickRegisterForm();
    container.appendChild(form);
    form.querySelector('.quick-register__input').value = VALID_URL;

    form.dispatchEvent(new Event('submit', { cancelable: true }));

    const button = form.querySelector('.quick-register__btn');
    expect(button.disabled).toBe(true);
    expect(form.querySelector('.quick-register__input').disabled).toBe(true);
    resolvePost({});
    await vi.waitFor(() => expect(button.disabled).toBe(false));
  });

  it('409 エラー時は重複メッセージが表示され onRegistered は呼ばれない', async () => {
    api.post.mockRejectedValue(new ApiError(409, 'Conflict', ''));
    const onRegistered = vi.fn();
    const form = createQuickRegisterForm({ onRegistered });
    container.appendChild(form);

    await submitWith(form, VALID_URL);

    expect(form.querySelector('.quick-register__error').textContent).toBe('この動画はすでに登録されています');
    expect(form.querySelector('.quick-register__input').value).toBe(VALID_URL);
    expect(onRegistered).not.toHaveBeenCalled();
    expect(toast.success).not.toHaveBeenCalled();
  });

  it('ネットワークエラー時は再試行を促すメッセージが表示される', async () => {
    api.post.mockRejectedValue(new TypeError('Failed to fetch'));
    const form = createQuickRegisterForm();
    container.appendChild(form);

    await submitWith(form, VALID_URL);

    expect(form.querySelector('.quick-register__error').textContent).toContain('ネットワークエラー');
  });

  it('入力するとエラー表示がクリアされる', async () => {
    const form = createQuickRegisterForm();
    container.appendChild(form);
    await submitWith(form, '');
    const input = form.querySelector('.quick-register__input');

    input.dispatchEvent(new Event('input'));

    expect(form.querySelector('.quick-register__error').textContent).toBe('');
    expect(input.hasAttribute('aria-invalid')).toBe(false);
  });
});
