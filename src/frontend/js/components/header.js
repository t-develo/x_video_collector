// components/header.js — サイトヘッダーコンポーネント

import { createElement } from '../utils/dom.js';
import { navigateTo } from '../router.js';

const NAV_LINKS = [
  { label: '動画一覧', path: '/videos' },
  { label: '登録', path: '/register' },
  { label: 'タグ管理', path: '/tags' },
  { label: 'カテゴリ管理', path: '/categories' },
];

/**
 * ハンバーガーアイコン SVG を返す
 * @param {boolean} isOpen
 * @returns {string}
 */
function menuIconSvg(isOpen) {
  if (isOpen) {
    // ✕ アイコン
    return `<svg xmlns="http://www.w3.org/2000/svg" width="18" height="18" viewBox="0 0 24 24" fill="none"
      stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
      <line x1="18" y1="6" x2="6" y2="18"/><line x1="6" y1="6" x2="18" y2="18"/>
    </svg>`;
  }
  // ハンバーガーアイコン
  return `<svg xmlns="http://www.w3.org/2000/svg" width="18" height="18" viewBox="0 0 24 24" fill="none"
    stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
    <line x1="3" y1="6" x2="21" y2="6"/><line x1="3" y1="12" x2="21" y2="12"/><line x1="3" y1="18" x2="21" y2="18"/>
  </svg>`;
}

/**
 * ヘッダーをレンダリングする
 * @param {HTMLElement} container - #site-header 要素
 */
export function renderHeader(container) {
  const logo = createElement('span', { className: 'header-logo' }, 'X_VIDEO_COLLECTOR');
  logo.setAttribute('role', 'link');
  logo.setAttribute('aria-label', 'トップページへ');
  logo.addEventListener('click', () => navigateTo('/videos'));

  const nav = createElement('nav', {
    className: 'header-nav',
    id: 'header-nav',
  });
  nav.setAttribute('aria-label', 'メインナビゲーション');

  NAV_LINKS.forEach(({ label, path }) => {
    const link = createElement('a', {
      className: 'header-nav-link',
      href: `#${path}`,
    }, label);
    nav.appendChild(link);
  });

  // ハンバーガーメニューボタン（モバイル）
  let isMenuOpen = false;
  const menuBtn = createElement('button', {
    className: 'header-menu-btn',
    type: 'button',
  });
  menuBtn.setAttribute('aria-label', 'メニューを開く');
  menuBtn.setAttribute('aria-expanded', 'false');
  menuBtn.setAttribute('aria-controls', 'header-nav');
  menuBtn.innerHTML = menuIconSvg(false);

  menuBtn.addEventListener('click', () => {
    isMenuOpen = !isMenuOpen;
    nav.classList.toggle('header-nav--open', isMenuOpen);
    menuBtn.setAttribute('aria-expanded', String(isMenuOpen));
    menuBtn.setAttribute('aria-label', isMenuOpen ? 'メニューを閉じる' : 'メニューを開く');
    menuBtn.innerHTML = menuIconSvg(isMenuOpen);
  });

  // ナビリンククリックでメニューを閉じる（モバイル）
  nav.addEventListener('click', (e) => {
    if (e.target.classList.contains('header-nav-link') && isMenuOpen) {
      isMenuOpen = false;
      nav.classList.remove('header-nav--open');
      menuBtn.setAttribute('aria-expanded', 'false');
      menuBtn.setAttribute('aria-label', 'メニューを開く');
      menuBtn.innerHTML = menuIconSvg(false);
    }
  });

  const userInfo = createElement('div', {
    className: 'header-user',
    'aria-label': 'ユーザー情報',
  });
  loadUserInfo(userInfo);

  container.appendChild(logo);
  container.appendChild(nav);
  container.appendChild(menuBtn);
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
