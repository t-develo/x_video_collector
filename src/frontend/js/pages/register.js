// pages/register.js — 動画登録ページ

import { createElement } from '../utils/dom.js';
import { api, ApiError } from '../api.js';
import { toast } from '../components/toast.js';
import { navigateTo } from '../router.js';

/** X / Twitter URL のパターン */
const TWEET_URL_PATTERN = /^https?:\/\/(twitter\.com|x\.com)\/[A-Za-z0-9_]+\/status\/\d+/;

/**
 * URL バリデーション
 * @param {string} url
 * @returns {string|null} エラーメッセージ（問題なければ null）
 */
export function validateTweetUrl(url) {
  if (!url || url.trim() === '') {
    return 'URL を入力してください';
  }
  if (!TWEET_URL_PATTERN.test(url.trim())) {
    return '有効な X (Twitter) の動画 URL を入力してください\n例: https://x.com/user/status/123456789';
  }
  return null;
}

/**
 * ステータスバッジ要素を生成する
 * @param {'Pending'|'Downloading'|'Ready'|'Failed'} status
 * @returns {HTMLElement}
 */
function createStatusBadge(status) {
  const badge = createElement('span', { className: `status-badge status-badge--${status.toLowerCase()}` });
  const dot = createElement('span', { className: 'status-badge__dot' });
  const label = createElement('span', { className: 'status-badge__label', textContent: status });
  badge.appendChild(dot);
  badge.appendChild(label);
  return badge;
}

/**
 * 登録フォームを構築する
 * @returns {{ form: HTMLElement, getUrl: () => string, setLoading: (v: boolean) => void, showError: (msg: string) => void, clearError: () => void }}
 */
function buildRegisterForm() {
  // フォームタイトル
  const title = createElement('h1', {
    className: 'register-title',
    textContent: '動画登録',
  });

  const subtitle = createElement('p', {
    className: 'register-subtitle',
    textContent: 'X (Twitter) の動画 URL を入力して登録します',
  });

  // URL 入力フィールド
  const inputLabel = createElement('label', {
    className: 'register-label',
    'for': 'tweet-url-input',
    textContent: 'X (Twitter) 動画 URL',
  });

  const input = createElement('input', {
    className: 'register-input',
    id: 'tweet-url-input',
    type: 'url',
    placeholder: 'https://x.com/user/status/123456789',
    autocomplete: 'off',
    autocorrect: 'off',
    spellcheck: 'false',
  });

  const errorMsg = createElement('p', { className: 'register-error', 'aria-live': 'polite' });

  const inputGroup = createElement('div', { className: 'register-input-group' });
  inputGroup.appendChild(inputLabel);
  inputGroup.appendChild(input);
  inputGroup.appendChild(errorMsg);

  // 登録ボタン
  const buttonInner = createElement('span', { className: 'register-btn__label', textContent: '登録する' });
  const spinner = createElement('span', { className: 'register-btn__spinner', 'aria-hidden': 'true' });
  const button = createElement('button', {
    className: 'register-btn',
    type: 'submit',
  });
  button.appendChild(spinner);
  button.appendChild(buttonInner);

  // フォーム
  const form = createElement('form', { className: 'register-form', novalidate: '' });
  form.appendChild(title);
  form.appendChild(subtitle);
  form.appendChild(inputGroup);
  form.appendChild(button);

  return {
    form,
    getUrl: () => input.value,
    setLoading: (loading) => {
      button.disabled = loading;
      button.classList.toggle('register-btn--loading', loading);
      input.disabled = loading;
    },
    showError: (msg) => {
      errorMsg.textContent = msg;
      input.classList.add('register-input--error');
      input.setAttribute('aria-invalid', 'true');
    },
    clearError: () => {
      errorMsg.textContent = '';
      input.classList.remove('register-input--error');
      input.removeAttribute('aria-invalid');
    },
  };
}

/**
 * 登録ページを描画する
 * @param {HTMLElement} container
 */
export function renderRegisterPage(container) {
  const { form, getUrl, setLoading, showError, clearError } = buildRegisterForm();

  form.addEventListener('submit', async (e) => {
    e.preventDefault();
    const url = getUrl().trim();

    clearError();
    const validationError = validateTweetUrl(url);
    if (validationError) {
      showError(validationError);
      return;
    }

    setLoading(true);
    try {
      await api.post('/videos', { tweetUrl: url });
      toast.success('動画を登録しました。ダウンロード処理を開始します。');
      navigateTo('/videos');
    } catch (err) {
      if (err instanceof ApiError) {
        const msg = err.status === 409
          ? 'この動画はすでに登録されています'
          : err.status === 422
            ? '無効な URL です'
            : `エラーが発生しました (${err.status})`;
        showError(msg);
      } else {
        showError('ネットワークエラーが発生しました。再試行してください。');
      }
    } finally {
      setLoading(false);
    }
  });

  // リアルタイムバリデーション（入力後にエラーをクリア）
  form.querySelector('#tweet-url-input').addEventListener('input', clearError);

  const wrapper = createElement('div', { className: 'register-page' });
  wrapper.appendChild(form);
  container.appendChild(wrapper);
}
