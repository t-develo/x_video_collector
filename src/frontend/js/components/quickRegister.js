// components/quickRegister.js — トップページ用のクイック登録フォーム

import { createElement } from '../utils/dom.js';
import { api, ApiError } from '../api.js';
import { toast } from './toast.js';
import { validateTweetUrl, buildApiErrorMessage } from '../pages/register.js';

/**
 * URL 入力欄と登録ボタンだけのコンパクトな登録フォームを生成する
 * @param {object} [options]
 * @param {() => (void|Promise<void>)} [options.onRegistered] - 登録成功後に呼ばれるコールバック
 * @returns {HTMLFormElement}
 */
export function createQuickRegisterForm({ onRegistered } = {}) {
  const label = createElement('label', {
    className: 'visually-hidden',
    'for': 'quick-register-input',
    textContent: 'X (Twitter) 動画 URL',
  });

  const input = createElement('input', {
    className: 'quick-register__input',
    id: 'quick-register-input',
    type: 'url',
    placeholder: 'https://x.com/user/status/123456789',
    autocomplete: 'off',
    autocorrect: 'off',
    spellcheck: 'false',
  });

  const spinner = createElement('span', { className: 'quick-register__spinner', 'aria-hidden': 'true' });
  const buttonLabel = createElement('span', { className: 'quick-register__btn-label', textContent: '登録' });
  const button = createElement('button', { className: 'quick-register__btn', type: 'submit' });
  button.appendChild(spinner);
  button.appendChild(buttonLabel);

  const row = createElement('div', { className: 'quick-register__row' });
  row.appendChild(input);
  row.appendChild(button);

  const errorMsg = createElement('p', { className: 'quick-register__error', 'aria-live': 'polite' });

  const form = createElement('form', { className: 'quick-register', novalidate: '' });
  form.appendChild(label);
  form.appendChild(row);
  form.appendChild(errorMsg);

  const setLoading = (loading) => {
    button.disabled = loading;
    button.classList.toggle('quick-register__btn--loading', loading);
    input.disabled = loading;
  };

  const showError = (msg) => {
    errorMsg.textContent = msg;
    input.classList.add('quick-register__input--error');
    input.setAttribute('aria-invalid', 'true');
  };

  const clearError = () => {
    errorMsg.textContent = '';
    input.classList.remove('quick-register__input--error');
    input.removeAttribute('aria-invalid');
  };

  form.addEventListener('submit', async (e) => {
    e.preventDefault();
    const url = input.value.trim();

    clearError();
    const validationError = validateTweetUrl(url);
    if (validationError) {
      showError(validationError);
      return;
    }

    setLoading(true);
    let registered = false;
    try {
      await api.post('/videos', { tweetUrl: url });
      registered = true;
    } catch (err) {
      if (err instanceof ApiError) {
        showError(buildApiErrorMessage(err));
      } else {
        showError('ネットワークエラーが発生しました。再試行してください。');
      }
    } finally {
      setLoading(false);
    }

    if (!registered) return;

    toast.success('動画を登録しました。ダウンロード処理を開始します。');
    input.value = '';
    // 一覧の再取得失敗は一覧側でエラー表示するため、登録フォームのエラーにはしない
    await onRegistered?.();
  });

  input.addEventListener('input', clearError);

  return form;
}
