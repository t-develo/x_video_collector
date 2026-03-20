// components/header.js — サイトヘッダーコンポーネント

import { createElement } from '../utils/dom.js';
import { navigateTo } from '../router.js';

const NAV_LINKS = [
  { label: '動画一覧', path: '/videos' },
  { label: '登録', path: '/register' },
  { label: 'タグ管理', path: '/tags' },
];

/**
 * ヘッダーをレンダリングする
 * @param {HTMLElement} container - #site-header 要素
 */
export function renderHeader(container) {
  const logo = createElement('span', { className: 'header-logo' }, 'X_VIDEO_COLLECTOR');
  logo.addEventListener('click', () => navigateTo('/videos'));

  const nav = createElement('nav', { className: 'header-nav' });
  NAV_LINKS.forEach(({ label, path }) => {
    const link = createElement('a', {
      className: 'header-nav-link',
      href: `#${path}`,
    }, label);
    nav.appendChild(link);
  });

  const userInfo = createElement('div', { className: 'header-user' });
  loadUserInfo(userInfo);

  container.appendChild(logo);
  container.appendChild(nav);
  container.appendChild(userInfo);
}

/**
 * ユーザー情報を非同期で読み込みエリアを更新する
 * @param {HTMLElement} wrapper
 */
async function loadUserInfo(wrapper) {
  try {
    const r = await fetch('/.auth/me');
    const data = await r.json();
    const principal = data?.clientPrincipal;
    if (principal) {
      const name = document.createElement('span');
      name.textContent = principal.userDetails ?? principal.userId ?? '';
      wrapper.appendChild(name);
    }
  } catch {
    // 認証情報取得失敗は無視
  }
}
