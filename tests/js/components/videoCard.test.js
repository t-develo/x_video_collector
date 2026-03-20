import { describe, it, expect, vi, beforeEach } from 'vitest';
import { createVideoCard } from '../../../src/frontend/js/components/videoCard.js';

// format ユーティリティをモック
vi.mock('../../../src/frontend/js/utils/format.js', () => ({
  formatDate: (iso) => (iso ? '2026/01/01 12:00' : '---'),
  formatDuration: (sec) => (sec != null ? '01:30' : '--:--'),
}));

/** テスト用の最小限の Video オブジェクトを生成する */
function makeVideo(overrides = {}) {
  return {
    id: 'test-id-123',
    title: 'テスト動画',
    status: 'Ready',
    thumbnailBlobPath: null,
    durationSeconds: 90,
    tags: [],
    createdAt: '2026-01-01T12:00:00Z',
    ...overrides,
  };
}

describe('createVideoCard', () => {
  beforeEach(() => {
    document.body.innerHTML = '';
  });

  it('article 要素を返す', () => {
    const card = createVideoCard(makeVideo(), () => {});
    expect(card.tagName.toLowerCase()).toBe('article');
    expect(card.classList.contains('video-card')).toBe(true);
  });

  it('タイトルが表示される', () => {
    const card = createVideoCard(makeVideo({ title: 'マイ動画' }), () => {});
    expect(card.querySelector('.video-card__title').textContent).toBe('マイ動画');
  });

  it('タイトルが空の場合は「（タイトルなし）」を表示する', () => {
    const card = createVideoCard(makeVideo({ title: '' }), () => {});
    expect(card.querySelector('.video-card__title').textContent).toBe('（タイトルなし）');
  });

  it('ステータスバッジが表示される', () => {
    const card = createVideoCard(makeVideo({ status: 'Ready' }), () => {});
    const badge = card.querySelector('.status-badge');
    expect(badge).not.toBeNull();
    expect(badge.classList.contains('status-badge--ready')).toBe(true);
  });

  it('Pending ステータスバッジのクラスが正しい', () => {
    const card = createVideoCard(makeVideo({ status: 'Pending' }), () => {});
    expect(card.querySelector('.status-badge--pending')).not.toBeNull();
  });

  it('サムネイルなしの場合はプレースホルダーが表示される', () => {
    const card = createVideoCard(makeVideo({ thumbnailBlobPath: null }), () => {});
    expect(card.querySelector('.video-card__thumb-placeholder')).not.toBeNull();
    expect(card.querySelector('.video-card__thumb-img')).toBeNull();
  });

  it('サムネイルありの場合は img 要素が生成される', () => {
    const card = createVideoCard(makeVideo({ thumbnailBlobPath: 'thumbnails/test.jpg' }), () => {});
    const img = card.querySelector('.video-card__thumb-img');
    expect(img).not.toBeNull();
    expect(img.getAttribute('src')).toBe('/api/thumbnails/test-id-123');
  });

  it('再生時間が表示される', () => {
    const card = createVideoCard(makeVideo({ durationSeconds: 90 }), () => {});
    const duration = card.querySelector('.video-card__duration');
    expect(duration).not.toBeNull();
    expect(duration.textContent).toBe('01:30');
  });

  it('再生時間が null の場合は表示されない', () => {
    const card = createVideoCard(makeVideo({ durationSeconds: null }), () => {});
    expect(card.querySelector('.video-card__duration')).toBeNull();
  });

  it('タグが表示される', () => {
    const card = createVideoCard(makeVideo({
      tags: [{ name: 'anime' }, { name: 'action' }],
    }), () => {});
    const chips = card.querySelectorAll('.tag-chip');
    expect(chips.length).toBe(2);
    expect(chips[0].textContent).toBe('anime');
    expect(chips[1].textContent).toBe('action');
  });

  it('タグが 5 件を超える場合は「+N」チップが表示される', () => {
    const tags = Array.from({ length: 7 }, (_, i) => ({ name: `tag${i}` }));
    const card = createVideoCard(makeVideo({ tags }), () => {});
    const chips = card.querySelectorAll('.tag-chip');
    // 5件 + 1件の「+2」チップ
    expect(chips.length).toBe(6);
    const moreChip = card.querySelector('.tag-chip--more');
    expect(moreChip).not.toBeNull();
    expect(moreChip.textContent).toBe('+2');
  });

  it('タグがない場合はタグエリアが生成されない', () => {
    const card = createVideoCard(makeVideo({ tags: [] }), () => {});
    expect(card.querySelector('.video-card__tags')).toBeNull();
  });

  it('登録日時が表示される', () => {
    const card = createVideoCard(makeVideo(), () => {});
    expect(card.querySelector('.video-card__date')).not.toBeNull();
    expect(card.querySelector('.video-card__date').textContent).toBe('2026/01/01 12:00');
  });

  it('クリック時に onClick が呼ばれる', () => {
    const onClick = vi.fn();
    const card = createVideoCard(makeVideo(), onClick);
    card.dispatchEvent(new Event('click'));
    expect(onClick).toHaveBeenCalledTimes(1);
  });

  it('Enter キーで onClick が呼ばれる', () => {
    const onClick = vi.fn();
    const card = createVideoCard(makeVideo(), onClick);
    card.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', bubbles: true }));
    expect(onClick).toHaveBeenCalledTimes(1);
  });

  it('Space キーで onClick が呼ばれる', () => {
    const onClick = vi.fn();
    const card = createVideoCard(makeVideo(), onClick);
    card.dispatchEvent(new KeyboardEvent('keydown', { key: ' ', bubbles: true }));
    expect(onClick).toHaveBeenCalledTimes(1);
  });

  it('role="button" と tabindex="0" が設定される', () => {
    const card = createVideoCard(makeVideo(), () => {});
    expect(card.getAttribute('role')).toBe('button');
    expect(card.getAttribute('tabindex')).toBe('0');
  });
});
