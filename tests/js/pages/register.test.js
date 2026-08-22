import { describe, it, expect, vi, beforeEach } from 'vitest';
import { validateTweetUrl, renderRegisterPage, buildApiErrorMessage } from '../../../src/frontend/js/pages/register.js';

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

// router モジュールをモック
vi.mock('../../../src/frontend/js/router.js', () => ({
  navigateTo: vi.fn(),
  addRoute: vi.fn(),
  startRouter: vi.fn(),
  getCurrentPath: vi.fn(() => '/'),
}));

describe('validateTweetUrl', () => {
  it('空文字列はエラーを返す', () => {
    expect(validateTweetUrl('')).not.toBeNull();
  });

  it('空白のみはエラーを返す', () => {
    expect(validateTweetUrl('   ')).not.toBeNull();
  });

  it('x.com の有効な URL は null を返す', () => {
    expect(validateTweetUrl('https://x.com/user/status/1234567890')).toBeNull();
  });

  it('twitter.com の有効な URL は null を返す', () => {
    expect(validateTweetUrl('https://twitter.com/user/status/1234567890')).toBeNull();
  });

  it('前後の空白を無視して検証する', () => {
    expect(validateTweetUrl('  https://x.com/user/status/1234567890  ')).toBeNull();
  });

  it('クエリパラメータ付き URL も有効', () => {
    expect(validateTweetUrl('https://x.com/user/status/1234567890?s=20')).toBeNull();
  });

  it('x.com でない URL はエラーを返す', () => {
    expect(validateTweetUrl('https://youtube.com/watch?v=abc')).not.toBeNull();
  });

  it('/status/ が含まれない URL はエラーを返す', () => {
    expect(validateTweetUrl('https://x.com/user')).not.toBeNull();
  });

  it('status ID が数値でない場合はエラーを返す', () => {
    expect(validateTweetUrl('https://x.com/user/status/abc')).not.toBeNull();
  });

  it('http:// でも有効', () => {
    expect(validateTweetUrl('http://x.com/user/status/1234567890')).toBeNull();
  });

  it('/video/1 とクエリ付きの共有 URL も有効', () => {
    expect(
      validateTweetUrl('https://x.com/tachu_muscle/status/2088733011483525400/video/1?s=46'),
    ).toBeNull();
  });
});

describe('buildApiErrorMessage', () => {
  class ApiError extends Error {
    constructor(status, statusText, body) {
      super(`API Error: ${status} ${statusText}`);
      this.status = status;
      this.statusText = statusText;
      this.body = body;
    }
  }

  it('409 は重複登録メッセージを返す', () => {
    const msg = buildApiErrorMessage(new ApiError(409, 'Conflict', 'Already exists'));
    expect(msg).toContain('すでに登録されています');
  });

  it('400 は JSON ボディの message を表示する', () => {
    const body = JSON.stringify({ status: 400, error: 'BadRequest', message: 'Invalid X/Twitter URL' });
    const msg = buildApiErrorMessage(new ApiError(400, 'Bad Request', body));
    expect(msg).toContain('Invalid X/Twitter URL');
  });

  it('400 でボディが JSON でない場合は既定メッセージを返す', () => {
    const msg = buildApiErrorMessage(new ApiError(400, 'Bad Request', '<html>error</html>'));
    expect(msg).toContain('無効な URL');
  });

  it('400 でボディが空の場合は既定メッセージを返す', () => {
    const msg = buildApiErrorMessage(new ApiError(400, 'Bad Request', ''));
    expect(msg).toContain('無効な URL');
  });

  it('その他のステータスはステータスコード付きメッセージを返す', () => {
    const msg = buildApiErrorMessage(new ApiError(500, 'Internal Server Error', ''));
    expect(msg).toContain('500');
  });
});

describe('renderRegisterPage', () => {
  let container;

  beforeEach(() => {
    vi.clearAllMocks();
    document.body.innerHTML = '<div id="main"></div><div id="toast-container"></div>';
    container = document.getElementById('main');
  });

  it('フォームがレンダリングされる', () => {
    renderRegisterPage(container);
    expect(container.querySelector('.register-form')).not.toBeNull();
  });

  it('URL 入力フィールドが存在する', () => {
    renderRegisterPage(container);
    expect(container.querySelector('#tweet-url-input')).not.toBeNull();
  });

  it('登録ボタンが存在する', () => {
    renderRegisterPage(container);
    const btn = container.querySelector('.register-btn');
    expect(btn).not.toBeNull();
    expect(btn.type).toBe('submit');
  });

  it('空 URL でサブミットするとエラーが表示される', async () => {
    renderRegisterPage(container);
    const form = container.querySelector('.register-form');
    form.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));

    // 非同期処理を待つ
    await new Promise(r => setTimeout(r, 0));

    const errorEl = container.querySelector('.register-error');
    expect(errorEl.textContent).not.toBe('');
  });

  it('不正な URL でサブミットするとエラーが表示される', async () => {
    renderRegisterPage(container);
    const input = container.querySelector('#tweet-url-input');
    input.value = 'https://youtube.com/watch?v=abc';

    const form = container.querySelector('.register-form');
    form.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));

    await new Promise(r => setTimeout(r, 0));

    const errorEl = container.querySelector('.register-error');
    expect(errorEl.textContent).not.toBe('');
  });

  it('有効な URL でサブミットすると api.post が呼ばれる', async () => {
    const { api } = await import('../../../src/frontend/js/api.js');
    api.post.mockResolvedValue({ id: 'test-id' });

    renderRegisterPage(container);
    const input = container.querySelector('#tweet-url-input');
    input.value = 'https://x.com/user/status/1234567890';

    const form = container.querySelector('.register-form');
    form.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));

    await new Promise(r => setTimeout(r, 0));

    expect(api.post).toHaveBeenCalledWith('/videos', {
      tweetUrl: 'https://x.com/user/status/1234567890',
    });
  });

  it('API 成功時にトースト通知が表示され遷移する', async () => {
    const { api } = await import('../../../src/frontend/js/api.js');
    const { toast } = await import('../../../src/frontend/js/components/toast.js');
    const { navigateTo } = await import('../../../src/frontend/js/router.js');
    api.post.mockResolvedValue({ id: 'test-id' });

    renderRegisterPage(container);
    const input = container.querySelector('#tweet-url-input');
    input.value = 'https://x.com/user/status/1234567890';

    const form = container.querySelector('.register-form');
    form.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));

    await new Promise(r => setTimeout(r, 0));

    expect(toast.success).toHaveBeenCalled();
    expect(navigateTo).toHaveBeenCalledWith('/videos');
  });

  it('API 409 エラー時に「すでに登録されています」が表示される', async () => {
    const { api, ApiError } = await import('../../../src/frontend/js/api.js');
    api.post.mockRejectedValue(new ApiError(409, 'Conflict', 'Already exists'));

    renderRegisterPage(container);
    const input = container.querySelector('#tweet-url-input');
    input.value = 'https://x.com/user/status/1234567890';

    const form = container.querySelector('.register-form');
    form.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));

    await new Promise(r => setTimeout(r, 0));

    const errorEl = container.querySelector('.register-error');
    expect(errorEl.textContent).toContain('すでに登録されています');
  });

  it('API 422 エラー時に「無効な URL です」が表示される', async () => {
    const { api, ApiError } = await import('../../../src/frontend/js/api.js');
    api.post.mockRejectedValue(new ApiError(422, 'Unprocessable Entity', 'Invalid URL'));

    renderRegisterPage(container);
    const input = container.querySelector('#tweet-url-input');
    input.value = 'https://x.com/user/status/1234567890';

    const form = container.querySelector('.register-form');
    form.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));

    await new Promise(r => setTimeout(r, 0));

    const errorEl = container.querySelector('.register-error');
    expect(errorEl.textContent).toContain('無効な URL');
  });

  it('入力時にエラーがクリアされる', async () => {
    renderRegisterPage(container);
    const input = container.querySelector('#tweet-url-input');
    const form = container.querySelector('.register-form');

    // エラーを発生させる
    form.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
    await new Promise(r => setTimeout(r, 0));

    // エラーが表示されていることを確認
    const errorEl = container.querySelector('.register-error');
    expect(errorEl.textContent).not.toBe('');

    // 入力するとエラーがクリアされる
    input.dispatchEvent(new Event('input'));
    expect(errorEl.textContent).toBe('');
  });
});
