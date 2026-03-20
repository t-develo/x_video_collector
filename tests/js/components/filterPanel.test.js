import { describe, it, expect, vi } from 'vitest';
import { createFilterPanel } from '../../../src/frontend/js/components/filterPanel.js';

const TAGS = [
  { id: 'tag-1', name: 'アニメ', color: '#ff0000' },
  { id: 'tag-2', name: '音楽', color: '#00ff00' },
];

const CATEGORIES = [
  { id: 'cat-1', name: 'エンタメ' },
  { id: 'cat-2', name: '教育' },
];

const EMPTY_FILTERS = { tagIds: [], categoryId: null, status: null };

describe('createFilterPanel', () => {
  it('フィルターパネルが描画される', () => {
    const onFilter = vi.fn();
    const el = createFilterPanel({
      tags: TAGS,
      categories: CATEGORIES,
      initialFilters: EMPTY_FILTERS,
      onFilter,
    });

    expect(el.classList.contains('filter-panel')).toBe(true);
    expect(el.querySelector('.filter-panel__reset')).not.toBeNull();
  });

  it('タグボタンが正しい数だけ描画される', () => {
    const onFilter = vi.fn();
    const el = createFilterPanel({
      tags: TAGS,
      categories: CATEGORIES,
      initialFilters: EMPTY_FILTERS,
      onFilter,
    });

    const tagBtns = el.querySelectorAll('.filter-tag-btn');
    expect(tagBtns.length).toBe(TAGS.length);
    expect(tagBtns[0].textContent).toBe('アニメ');
    expect(tagBtns[1].textContent).toBe('音楽');
  });

  it('カテゴリセレクトにカテゴリが表示される', () => {
    const onFilter = vi.fn();
    const el = createFilterPanel({
      tags: TAGS,
      categories: CATEGORIES,
      initialFilters: EMPTY_FILTERS,
      onFilter,
    });

    const selects = el.querySelectorAll('.filter-section__select');
    // 2番目のセレクトがカテゴリ
    const catSelect = selects[1];
    const options = catSelect.querySelectorAll('option');
    // "すべて" + 2カテゴリ = 3
    expect(options.length).toBe(3);
  });

  it('タグボタンをクリックすると onFilter が tagIds に含まれた状態で呼ばれる', () => {
    const onFilter = vi.fn();
    const el = createFilterPanel({
      tags: TAGS,
      categories: CATEGORIES,
      initialFilters: EMPTY_FILTERS,
      onFilter,
    });

    const tagBtn = el.querySelector('[data-tag-id="tag-1"]');
    tagBtn.click();

    expect(onFilter).toHaveBeenCalledWith(
      expect.objectContaining({ tagIds: ['tag-1'] }),
    );
    expect(tagBtn.classList.contains('filter-tag-btn--active')).toBe(true);
  });

  it('アクティブなタグボタンを再クリックすると tagIds から除外される', () => {
    const onFilter = vi.fn();
    const el = createFilterPanel({
      tags: TAGS,
      categories: CATEGORIES,
      initialFilters: { ...EMPTY_FILTERS, tagIds: ['tag-1'] },
      onFilter,
    });

    const tagBtn = el.querySelector('[data-tag-id="tag-1"]');
    tagBtn.click();

    expect(onFilter).toHaveBeenCalledWith(
      expect.objectContaining({ tagIds: [] }),
    );
    expect(tagBtn.classList.contains('filter-tag-btn--active')).toBe(false);
  });

  it('ステータスセレクトを変更すると onFilter が呼ばれる', () => {
    const onFilter = vi.fn();
    const el = createFilterPanel({
      tags: TAGS,
      categories: CATEGORIES,
      initialFilters: EMPTY_FILTERS,
      onFilter,
    });

    const statusSelect = el.querySelectorAll('.filter-section__select')[0];
    statusSelect.value = 'Ready';
    statusSelect.dispatchEvent(new Event('change'));

    expect(onFilter).toHaveBeenCalledWith(
      expect.objectContaining({ status: 'Ready' }),
    );
  });

  it('カテゴリセレクトを変更すると onFilter が呼ばれる', () => {
    const onFilter = vi.fn();
    const el = createFilterPanel({
      tags: TAGS,
      categories: CATEGORIES,
      initialFilters: EMPTY_FILTERS,
      onFilter,
    });

    const catSelect = el.querySelectorAll('.filter-section__select')[1];
    catSelect.value = 'cat-2';
    catSelect.dispatchEvent(new Event('change'));

    expect(onFilter).toHaveBeenCalledWith(
      expect.objectContaining({ categoryId: 'cat-2' }),
    );
  });

  it('リセットボタンですべてのフィルターがクリアされる', () => {
    const onFilter = vi.fn();
    const el = createFilterPanel({
      tags: TAGS,
      categories: CATEGORIES,
      initialFilters: { tagIds: ['tag-1'], categoryId: 'cat-1', status: 'Ready' },
      onFilter,
    });

    const resetBtn = el.querySelector('.filter-panel__reset');
    resetBtn.click();

    expect(onFilter).toHaveBeenCalledWith({ tagIds: [], categoryId: null, status: null });

    // UI もリセットされる
    const statusSelect = el.querySelectorAll('.filter-section__select')[0];
    expect(statusSelect.value).toBe('');
    const tagBtn = el.querySelector('[data-tag-id="tag-1"]');
    expect(tagBtn.classList.contains('filter-tag-btn--active')).toBe(false);
  });

  it('初期フィルターが反映される', () => {
    const onFilter = vi.fn();
    const el = createFilterPanel({
      tags: TAGS,
      categories: CATEGORIES,
      initialFilters: { tagIds: ['tag-2'], categoryId: 'cat-1', status: 'Pending' },
      onFilter,
    });

    const tagBtn2 = el.querySelector('[data-tag-id="tag-2"]');
    expect(tagBtn2.classList.contains('filter-tag-btn--active')).toBe(true);

    const statusSelect = el.querySelectorAll('.filter-section__select')[0];
    expect(statusSelect.value).toBe('Pending');
  });

  it('タグが空のときタグリストが空でも描画される', () => {
    const onFilter = vi.fn();
    const el = createFilterPanel({
      tags: [],
      categories: [],
      initialFilters: EMPTY_FILTERS,
      onFilter,
    });

    expect(el.querySelector('.filter-section__tag-list')).not.toBeNull();
    expect(el.querySelectorAll('.filter-tag-btn').length).toBe(0);
  });
});
