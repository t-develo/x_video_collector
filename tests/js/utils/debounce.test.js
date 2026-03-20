import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { debounce } from '../../../src/frontend/js/utils/debounce.js';

describe('debounce', () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('指定時間内の連続呼び出しは最後の1回だけ実行される', () => {
    const fn = vi.fn();
    const debouncedFn = debounce(fn, 300);

    debouncedFn('a');
    debouncedFn('b');
    debouncedFn('c');

    expect(fn).not.toHaveBeenCalled();

    vi.advanceTimersByTime(300);

    expect(fn).toHaveBeenCalledOnce();
    expect(fn).toHaveBeenCalledWith('c');
  });

  it('指定時間後に1回だけ実行される', () => {
    const fn = vi.fn();
    const debouncedFn = debounce(fn, 300);

    debouncedFn('hello');
    expect(fn).not.toHaveBeenCalled();

    vi.advanceTimersByTime(299);
    expect(fn).not.toHaveBeenCalled();

    vi.advanceTimersByTime(1);
    expect(fn).toHaveBeenCalledOnce();
    expect(fn).toHaveBeenCalledWith('hello');
  });

  it('間隔を空けた2回の呼び出しはそれぞれ実行される', () => {
    const fn = vi.fn();
    const debouncedFn = debounce(fn, 300);

    debouncedFn('first');
    vi.advanceTimersByTime(300);
    expect(fn).toHaveBeenCalledTimes(1);

    debouncedFn('second');
    vi.advanceTimersByTime(300);
    expect(fn).toHaveBeenCalledTimes(2);
    expect(fn).toHaveBeenNthCalledWith(2, 'second');
  });

  it('引数を正しく転送する', () => {
    const fn = vi.fn();
    const debouncedFn = debounce(fn, 100);

    debouncedFn(1, 2, 3);
    vi.advanceTimersByTime(100);

    expect(fn).toHaveBeenCalledWith(1, 2, 3);
  });
});
