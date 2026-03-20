import { describe, it, expect, beforeEach, vi } from 'vitest';
import { addRoute, navigateTo, getCurrentPath, startRouter } from '../../src/frontend/js/router.js';

describe('router', () => {
  beforeEach(() => {
    // ルートマップをリセットするため、モジュールをリセット
    window.location.hash = '';
    document.body.innerHTML = '<div id="container"></div>';
  });

  describe('getCurrentPath', () => {
    it('ハッシュが空の場合は "/" を返す', () => {
      window.location.hash = '';
      expect(getCurrentPath()).toBe('/');
    });

    it('ハッシュ値からパスを返す', () => {
      window.location.hash = '#/videos';
      expect(getCurrentPath()).toBe('/videos');
    });
  });

  describe('navigateTo', () => {
    it('指定パスにハッシュを設定する', () => {
      navigateTo('/register');
      expect(window.location.hash).toBe('#/register');
    });
  });

  describe('startRouter', () => {
    it('登録されたルートハンドラを呼び出す', () => {
      const container = document.getElementById('container');
      const handler = vi.fn();

      addRoute('/test-route', handler);
      window.location.hash = '#/test-route';
      startRouter(container);

      expect(handler).toHaveBeenCalledWith(container);
    });

    it('未登録ルートで 404 を表示する', () => {
      const container = document.getElementById('container');
      window.location.hash = '#/nonexistent-page-xyz';
      startRouter(container);

      expect(container.textContent).toContain('404');
    });

    it('パラメータ付きルートがマッチする', () => {
      const container = document.getElementById('container');
      const handler = vi.fn();

      addRoute('/items/:id', handler);
      window.location.hash = '#/items/abc-123';
      startRouter(container);

      expect(handler).toHaveBeenCalledWith(container, 'abc-123');
    });
  });
});
