import { describe, it, expect, vi } from 'vitest';
import { createElement, byId, clearChildren } from '../../../src/frontend/js/utils/dom.js';

describe('dom utils', () => {
  describe('createElement', () => {
    it('タグ名で要素を生成する', () => {
      const el = createElement('div');
      expect(el.tagName).toBe('DIV');
    });

    it('className を設定する', () => {
      const el = createElement('span', { className: 'my-class' });
      expect(el.className).toBe('my-class');
    });

    it('textContent を設定する', () => {
      const el = createElement('p', { textContent: 'Hello World' });
      expect(el.textContent).toBe('Hello World');
    });

    it('属性を設定する', () => {
      const el = createElement('input', { type: 'text', placeholder: '入力' });
      expect(el.getAttribute('type')).toBe('text');
      expect(el.getAttribute('placeholder')).toBe('入力');
    });

    it('イベントリスナーを登録する', () => {
      const handler = vi.fn();
      const el = createElement('button', { onClick: handler });
      el.click();
      expect(handler).toHaveBeenCalledTimes(1);
    });

    it('文字列の children を textContent に設定する', () => {
      const el = createElement('h1', {}, 'タイトル');
      expect(el.textContent).toBe('タイトル');
    });

    it('Node の children を追加する', () => {
      const child = document.createElement('span');
      const el = createElement('div', {}, child);
      expect(el.firstChild).toBe(child);
    });

    it('Node 配列の children をすべて追加する', () => {
      const c1 = document.createElement('li');
      const c2 = document.createElement('li');
      const el = createElement('ul', {}, [c1, c2]);
      expect(el.children).toHaveLength(2);
    });
  });

  describe('clearChildren', () => {
    it('子要素をすべて削除する', () => {
      const container = document.createElement('div');
      container.appendChild(document.createElement('p'));
      container.appendChild(document.createElement('p'));

      clearChildren(container);
      expect(container.childNodes).toHaveLength(0);
    });
  });
});
