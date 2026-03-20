// router.js — Hash ベースクライアントサイドルーター

import { clearChildren, createElement } from './utils/dom.js';

/** @type {Map<string, function(HTMLElement): void>} */
const routes = new Map();

/**
 * ルートを登録する
 * @param {string} path - ハッシュパス（例: '/', '/register'）
 * @param {function(HTMLElement): void} handler
 */
export function addRoute(path, handler) {
  routes.set(path, handler);
}

/**
 * 指定パスへ遷移する
 * @param {string} path
 */
export function navigateTo(path) {
  window.location.hash = `#${path}`;
}

/**
 * 現在のハッシュからパスを取得する
 * @returns {string}
 */
export function getCurrentPath() {
  return window.location.hash.slice(1) || '/';
}

/**
 * ルーターを起動する
 * @param {HTMLElement} container - ページコンテンツを描画するコンテナ
 */
export function startRouter(container) {
  const handleRoute = () => {
    const path = getCurrentPath();
    const handler = routes.get(path);

    clearChildren(container);

    if (handler) {
      handler(container);
    } else {
      render404(container);
    }

    updateActiveNavLinks(path);
  };

  window.addEventListener('hashchange', handleRoute);
  handleRoute();
}

/**
 * ナビゲーションリンクのアクティブ状態を更新する
 * @param {string} currentPath
 */
function updateActiveNavLinks(currentPath) {
  document.querySelectorAll('.header-nav-link').forEach(link => {
    const href = link.getAttribute('href');
    const linkPath = href ? href.replace('#', '') : '';
    link.classList.toggle('active', linkPath === currentPath);
  });
}

/**
 * 404 ページを描画する
 * @param {HTMLElement} container
 */
function render404(container) {
  const wrapper = createElement('div', { className: 'not-found-wrapper' });
  const code = createElement('p', { className: 'not-found-code', textContent: '404' });
  const msg = createElement('p', { className: 'not-found-message', textContent: 'ページが見つかりません' });

  wrapper.appendChild(code);
  wrapper.appendChild(msg);
  container.appendChild(wrapper);
}
