import { describe, it, expect } from 'vitest';
import { createVideoCardSkeleton, createSkeletonGrid } from '../../../src/frontend/js/components/skeleton.js';

describe('createVideoCardSkeleton', () => {
  it('スケルトンカード要素が生成される', () => {
    const card = createVideoCardSkeleton();
    expect(card).not.toBeNull();
    expect(card.classList.contains('video-card-skeleton')).toBe(true);
  });

  it('aria-hidden が設定される', () => {
    const card = createVideoCardSkeleton();
    expect(card.getAttribute('aria-hidden')).toBe('true');
  });

  it('サムネイル領域が含まれる', () => {
    const card = createVideoCardSkeleton();
    expect(card.querySelector('.video-card-skeleton__thumb')).not.toBeNull();
  });

  it('ボディ領域が含まれる', () => {
    const card = createVideoCardSkeleton();
    expect(card.querySelector('.video-card-skeleton__body')).not.toBeNull();
  });

  it('シマーアニメーション要素が含まれる', () => {
    const card = createVideoCardSkeleton();
    const shimmerEls = card.querySelectorAll('.skeleton');
    expect(shimmerEls.length).toBeGreaterThan(0);
  });
});

describe('createSkeletonGrid', () => {
  it('デフォルトで 8 枚のスケルトンが生成される', () => {
    const grid = createSkeletonGrid();
    expect(grid.querySelectorAll('.video-card-skeleton').length).toBe(8);
  });

  it('指定数のスケルトンが生成される', () => {
    const grid = createSkeletonGrid(4);
    expect(grid.querySelectorAll('.video-card-skeleton').length).toBe(4);
  });

  it('0 件でも動作する', () => {
    const grid = createSkeletonGrid(0);
    expect(grid.querySelectorAll('.video-card-skeleton').length).toBe(0);
  });

  it('グリッドに role="status" が設定される', () => {
    const grid = createSkeletonGrid();
    expect(grid.getAttribute('role')).toBe('status');
  });

  it('グリッドに aria-label が設定される', () => {
    const grid = createSkeletonGrid();
    expect(grid.getAttribute('aria-label')).toBe('読み込み中');
  });

  it('グリッドクラスが設定される', () => {
    const grid = createSkeletonGrid();
    expect(grid.classList.contains('video-grid')).toBe(true);
  });
});
