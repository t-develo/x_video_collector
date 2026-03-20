import { describe, it, expect, beforeEach, vi } from 'vitest';
import { getState, setState, subscribe, resetState } from '../../src/frontend/js/state.js';

describe('state', () => {
  beforeEach(() => {
    resetState();
  });

  describe('getState / setState', () => {
    it('値を設定・取得できる', () => {
      setState('user', { name: 'Alice' });
      expect(getState('user')).toEqual({ name: 'Alice' });
    });

    it('未設定のキーは undefined を返す', () => {
      expect(getState('nonexistent')).toBeUndefined();
    });

    it('値を上書きできる', () => {
      setState('count', 1);
      setState('count', 2);
      expect(getState('count')).toBe(2);
    });
  });

  describe('subscribe', () => {
    it('setState 時に購読者へ通知する', () => {
      const listener = vi.fn();
      subscribe('items', listener);

      setState('items', [1, 2, 3]);
      expect(listener).toHaveBeenCalledWith([1, 2, 3]);
    });

    it('複数の購読者すべてへ通知する', () => {
      const l1 = vi.fn();
      const l2 = vi.fn();
      subscribe('x', l1);
      subscribe('x', l2);

      setState('x', 42);
      expect(l1).toHaveBeenCalledWith(42);
      expect(l2).toHaveBeenCalledWith(42);
    });

    it('購読解除後は通知されない', () => {
      const listener = vi.fn();
      const unsubscribe = subscribe('flag', listener);

      setState('flag', true);
      unsubscribe();
      setState('flag', false);

      expect(listener).toHaveBeenCalledTimes(1);
    });
  });
});
