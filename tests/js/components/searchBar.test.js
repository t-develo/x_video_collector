import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { createSearchBar } from '../../../src/frontend/js/components/searchBar.js';

describe('createSearchBar', () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('検索バーが描画される', () => {
    const onSearch = vi.fn();
    const el = createSearchBar({ onSearch });

    expect(el.classList.contains('search-bar')).toBe(true);
    expect(el.querySelector('.search-bar__input')).not.toBeNull();
    expect(el.querySelector('.search-bar__clear')).not.toBeNull();
    expect(el.querySelector('.search-bar__icon')).not.toBeNull();
  });

  it('初期値が設定される', () => {
    const onSearch = vi.fn();
    const el = createSearchBar({ initialValue: 'テスト', onSearch });

    const input = el.querySelector('.search-bar__input');
    expect(input.value).toBe('テスト');
  });

  it('初期値が空のときクリアボタンが非表示', () => {
    const onSearch = vi.fn();
    const el = createSearchBar({ onSearch });

    const clearBtn = el.querySelector('.search-bar__clear');
    expect(clearBtn.hidden).toBe(true);
  });

  it('初期値があるときクリアボタンが表示', () => {
    const onSearch = vi.fn();
    const el = createSearchBar({ initialValue: 'hello', onSearch });

    const clearBtn = el.querySelector('.search-bar__clear');
    expect(clearBtn.hidden).toBe(false);
  });

  it('入力すると 300ms 後に onSearch が呼ばれる', () => {
    const onSearch = vi.fn();
    const el = createSearchBar({ onSearch });

    const input = el.querySelector('.search-bar__input');
    input.value = 'abc';
    input.dispatchEvent(new Event('input'));

    expect(onSearch).not.toHaveBeenCalled();

    vi.advanceTimersByTime(300);
    expect(onSearch).toHaveBeenCalledWith('abc');
  });

  it('300ms 以内の連続入力はデバウンスされる', () => {
    const onSearch = vi.fn();
    const el = createSearchBar({ onSearch });

    const input = el.querySelector('.search-bar__input');

    input.value = 'a';
    input.dispatchEvent(new Event('input'));
    vi.advanceTimersByTime(100);

    input.value = 'ab';
    input.dispatchEvent(new Event('input'));
    vi.advanceTimersByTime(100);

    input.value = 'abc';
    input.dispatchEvent(new Event('input'));
    vi.advanceTimersByTime(300);

    expect(onSearch).toHaveBeenCalledOnce();
    expect(onSearch).toHaveBeenCalledWith('abc');
  });

  it('クリアボタンを押すと入力がクリアされ onSearch(\'\') が呼ばれる', () => {
    const onSearch = vi.fn();
    const el = createSearchBar({ initialValue: 'hello', onSearch });

    const clearBtn = el.querySelector('.search-bar__clear');
    clearBtn.click();

    const input = el.querySelector('.search-bar__input');
    expect(input.value).toBe('');
    expect(clearBtn.hidden).toBe(true);
    expect(onSearch).toHaveBeenCalledWith('');
  });

  it('入力後にクリアボタンが表示される', () => {
    const onSearch = vi.fn();
    const el = createSearchBar({ onSearch });

    const input = el.querySelector('.search-bar__input');
    const clearBtn = el.querySelector('.search-bar__clear');

    input.value = 'test';
    input.dispatchEvent(new Event('input'));

    expect(clearBtn.hidden).toBe(false);
  });
});
