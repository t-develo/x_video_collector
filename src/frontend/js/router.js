// router.js — Hash ベースクライアントサイドルーター

import { clearChildren, createElement } from './utils/dom.js';

/** @type {Map<string, function(HTMLElement): void>} */
const routes = new Map();

/** @type {Array<{ pattern: RegExp, paramNames: string[], handler: function(HTMLElement, ...string): void }>} */
const paramRoutes = [];

/**
 * ルートを登録する
 * @param {string} path - ハッシュパス（例: '/', '/register', '/videos/:id'）
 * @param {function(HTMLElement, ...string): void} handler
 */
export function addRoute(path, handler) {
  if (path.includes(':')) {
    const paramNames = [];
    const regexStr = path.replace(/:([^/]+)/g, (_match, name) => {
      paramNames.push(name);
      return '([^/]+)';
    });
    paramRoutes.push({
      pattern: new RegExp(`^${regexStr}$`),
      paramNames,
      handler,
    });
  } else {
    routes.set(path, handler);
  }
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
 * パラメータ付きルートをマッチングする
 * @param {string} path
 * @returns {{ handler: function, params: string[] } | null}
 */
function matchParamRoute(path) {
  for (const route of paramRoutes) {
    const match = path.match(route.pattern);
    if (match) {
      return { handler: route.handler, params: match.slice(1) };
    }
  }
  return null;
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
      const paramMatch = matchParamRoute(path);
      if (paramMatch) {
        paramMatch.handler(container, ...paramMatch.params);
      } else {
        render404(container);
      }
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
