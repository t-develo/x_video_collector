import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { observeLazyImage } from '../../../src/frontend/js/utils/lazyLoad.js';

describe('observeLazyImage', () => {
  let img;

  beforeEach(() => {
    img = document.createElement('img');
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('IntersectionObserver 未サポート時に即時 src がセットされる', () => {
    const original = window.IntersectionObserver;
    delete window.IntersectionObserver;

    observeLazyImage(img, '/test.jpg', 'テスト画像');
    expect(img.src).toContain('/test.jpg');
    expect(img.alt).toBe('テスト画像');

    window.IntersectionObserver = original;
  });

  it('alt 属性がセットされる', () => {
    const observeMock = vi.fn();
    window.IntersectionObserver = vi.fn(() => ({ observe: observeMock }));

    observeLazyImage(img, '/test.jpg', '動画サムネイル');
    expect(img.alt).toBe('動画サムネイル');
  });

  it('data-src 属性に URL がセットされる', () => {
    const observeMock = vi.fn();
    window.IntersectionObserver = vi.fn(() => ({ observe: observeMock }));

    observeLazyImage(img, '/api/thumbnails/123');
    expect(img.dataset.src).toBe('/api/thumbnails/123');
  });

  it('IntersectionObserver が observe を呼び出す', () => {
    const observeMock = vi.fn();
    window.IntersectionObserver = vi.fn(() => ({ observe: observeMock }));

    observeLazyImage(img, '/test.jpg');
    expect(observeMock).toHaveBeenCalledWith(img);
  });

  it('alt のデフォルトは空文字', () => {
    const original = window.IntersectionObserver;
    delete window.IntersectionObserver;

    observeLazyImage(img, '/test.jpg');
    expect(img.alt).toBe('');

    window.IntersectionObserver = original;
  });

  it('IntersectionObserver 未サポート時にエラーコールバックが error イベントで呼ばれる', () => {
    const original = window.IntersectionObserver;
    delete window.IntersectionObserver;

    const onError = vi.fn();
    observeLazyImage(img, '/bad.jpg', '', onError);
    img.dispatchEvent(new Event('error'));
    expect(onError).toHaveBeenCalledTimes(1);

    window.IntersectionObserver = original;
  });
});
