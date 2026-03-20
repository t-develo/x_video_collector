// components/header.js — サイトヘッダーコンポーネント

import { createElement, clearChildren } from '../utils/dom.js';
import { navigateTo } from '../router.js';

const NAV_LINKS = [
  { label: '動画一覧', path: '/videos' },
  { label: '登録', path: '/register' },
  { label: 'タグ管理', path: '/tags' },
  { label: 'カテゴリ管理', path: '/categories' },
];

const SVG_NS = 'http://www.w3.org/2000/svg';

/**
 * SVG line 要素を生成する
 * @param {number} x1
 * @param {number} y1
 * @param {number} x2
 * @param {number} y2
 * @returns {SVGLineElement}
 */
function createSvgLine(x1, y1, x2, y2) {
  const line = document.createElementNS(SVG_NS, 'line');
  line.setAttribute('x1', String(x1));
  line.setAttribute('y1', String(y1));
  line.setAttribute('x2', String(x2));
  line.setAttribute('y2', String(y2));
  return line;
}

/**
 * SVG アイコン要素を生成する
 * @returns {SVGSVGElement}
 */
function createSvgIcon() {
  const svg = document.createElementNS(SVG_NS, 'svg');
  svg.setAttribute('width', '18');
  svg.setAttribute('height', '18');
  svg.setAttribute('viewBox', '0 0 24 24');
  svg.setAttribute('fill', 'none');
  svg.setAttribute('stroke', 'currentColor');
  svg.setAttribute('stroke-width', '2');
  svg.setAttribute('stroke-linecap', 'round');
  svg.setAttribute('stroke-linejoin', 'round');
  svg.setAttribute('aria-hidden', 'true');
  return svg;
}

/**
 * ハンバーガーアイコン（開閉状態に応じた SVG）をボタンに設定する
 * @param {HTMLButtonElement} btn
 * @param {boolean} isOpen
 */
function setMenuIcon(btn, isOpen) {
  clearChildren(btn);
  const svg = createSvgIcon();
  if (isOpen) {
    // ✕ アイコン
    svg.appendChild(createSvgLine(18, 6, 6, 18));
    svg.appendChild(createSvgLine(6, 6, 18, 18));
  } else {
    // ハンバーガーアイコン
    svg.appendChild(createSvgLine(3, 6, 21, 6));
    svg.appendChild(createSvgLine(3, 12, 21, 12));
    svg.appendChild(createSvgLine(3, 18, 21, 18));
  }
  btn.appendChild(svg);
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
  setMenuIcon(menuBtn, false);

  menuBtn.addEventListener('click', () => {
    isMenuOpen = !isMenuOpen;
    nav.classList.toggle('header-nav--open', isMenuOpen);
    menuBtn.setAttribute('aria-expanded', String(isMenuOpen));
    menuBtn.setAttribute('aria-label', isMenuOpen ? 'メニューを閉じる' : 'メニューを開く');
    setMenuIcon(menuBtn, isMenuOpen);
  });

  // ナビリンククリックでメニューを閉じる（モバイル）
  nav.addEventListener('click', (e) => {
    if (e.target.classList.contains('header-nav-link') && isMenuOpen) {
      isMenuOpen = false;
      nav.classList.remove('header-nav--open');
      menuBtn.setAttribute('aria-expanded', 'false');
      menuBtn.setAttribute('aria-label', 'メニューを開く');
      setMenuIcon(menuBtn, false);
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
