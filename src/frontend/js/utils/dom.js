// utils/dom.js — DOM 操作ヘルパー

/**
 * 要素を生成するヘルパー
 * @param {string} tag - HTML タグ名
 * @param {Record<string, unknown>} attrs - 属性オブジェクト
 * @param {string|Node|Array<Node>|null} children - 子要素
 * @returns {HTMLElement}
 */
export function createElement(tag, attrs = {}, children = null) {
  const el = document.createElement(tag);

  for (const [key, value] of Object.entries(attrs)) {
    if (key === 'className') {
      el.className = String(value);
    } else if (key === 'textContent') {
      el.textContent = String(value);
    } else if (key.startsWith('on') && typeof value === 'function') {
      el.addEventListener(key.slice(2).toLowerCase(), value);
    } else {
      el.setAttribute(key, String(value));
    }
  }

  if (children !== null) {
    if (typeof children === 'string') {
      el.textContent = children;
    } else if (Array.isArray(children)) {
      children.forEach(child => {
        if (child != null) el.appendChild(child);
      });
    } else {
      el.appendChild(children);
    }
  }

  return el;
}

/**
 * ID で要素を取得する（null 安全）
 * @param {string} id
 * @returns {HTMLElement|null}
 */
export function byId(id) {
  return document.getElementById(id);
}

/**
 * コンテナの子要素をすべて削除する
 * @param {HTMLElement} container
 */
export function clearChildren(container) {
  while (container.firstChild) {
    container.removeChild(container.firstChild);
  }
}
