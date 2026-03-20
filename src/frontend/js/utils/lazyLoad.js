// utils/lazyLoad.js — Intersection Observer による画像の遅延読み込み

/**
 * Intersection Observer を使って img 要素の遅延読み込みを行う。
 * `data-src` に本来の URL をセットしておき、ビューポートに入ったタイミングで
 * `src` に移行する。
 *
 * @param {HTMLImageElement} img - 対象の img 要素
 * @param {string} src - 読み込む画像 URL
 * @param {string} [alt] - alt テキスト
 * @param {function(): void} [onError] - 読み込み失敗時のコールバック
 */
export function observeLazyImage(img, src, alt = '', onError = null) {
  img.setAttribute('alt', alt);
  img.dataset.src = src;

  if (!('IntersectionObserver' in window)) {
    // フォールバック: 即時読み込み
    img.src = src;
    if (onError) img.addEventListener('error', onError, { once: true });
    return;
  }

  const observer = new IntersectionObserver(
    (entries, obs) => {
      entries.forEach(entry => {
        if (!entry.isIntersecting) return;
        img.src = img.dataset.src;
        if (onError) img.addEventListener('error', onError, { once: true });
        obs.unobserve(img);
      });
    },
    { rootMargin: '200px' },
  );

  observer.observe(img);
}
