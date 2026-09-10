import { describe, it, expect } from 'vitest';
import { readFileSync, readdirSync } from 'node:fs';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';

/**
 * iOS Safari は 16px 未満のフォーム部品にフォーカスするとページを自動で拡大し、
 * フォーカスを外しても元の倍率に戻さない。
 * jsdom は link 済みスタイルシートを解決しないため算出スタイルでは検証できない。
 * ここでは CSS のソース文字列を読んで、入力系の font-size が
 * 16px 未満のトークンに戻っていないことを守る。
 */

const HERE = dirname(fileURLToPath(import.meta.url));
const FRONTEND_DIR = join(HERE, '../../../src/frontend');
const CSS_DIR = join(FRONTEND_DIR, 'css');

/** css/ 配下の .css を再帰的に集める */
const collectCssFiles = (dir) =>
  readdirSync(dir, { withFileTypes: true }).flatMap((entry) => {
    const path = join(dir, entry.name);
    if (entry.isDirectory()) return collectCssFiles(path);
    return entry.name.endsWith('.css') ? [path] : [];
  });

const cssFiles = collectCssFiles(CSS_DIR);

const readCss = (path) => readFileSync(path, 'utf8');

/** `セレクタ列 { 宣言 }` を粗くパースする（@media のネストは中身だけを拾えれば十分） */
const parseRules = (css) => {
  const rules = [];
  const pattern = /([^{}]+)\{([^{}]*)\}/g;
  let match;

  while ((match = pattern.exec(css)) !== null) {
    const selector = match[1].trim();
    if (selector.startsWith('@')) continue;
    rules.push({ selector, body: match[2] });
  }

  return rules;
};

/**
 * input / select / textarea として描画される要素のセレクタ。
 * 要素セレクタそのものと、`-input` / `__select` のように
 * ハイフン・アンダースコア区切りで終わるクラス名 (BEM を含む) を拾う。
 */
const CONTROL_SELECTOR_PATTERN =
  /(^|[\s,])(input|select|textarea)([\s,:]|$)|[-_](input|select|textarea)\b/i;

const SMALL_TOKENS = ['--text-xs', '--text-sm'];

describe('フォーム部品のフォントサイズ', () => {
  it('variables.css が --text-control を定義している', () => {
    const css = readCss(join(CSS_DIR, 'variables.css'));

    expect(css).toMatch(/--text-control:/);
  });

  it('タッチ端末 (pointer: coarse) で --text-control を 16px 相当に引き上げている', () => {
    const css = readCss(join(CSS_DIR, 'variables.css'));
    const coarseBlock = css.match(/@media\s*\(pointer:\s*coarse\)\s*\{[\s\S]*?\n\}/);

    expect(coarseBlock).not.toBeNull();
    // --text-base は 1rem = 16px
    expect(coarseBlock[0]).toMatch(/--text-control:\s*var\(--text-base\)/);
  });

  it('reset.css が input / textarea / select に --text-control をフォールバック指定している', () => {
    const css = readCss(join(CSS_DIR, 'reset.css'));
    const rule = parseRules(css).find((r) => r.selector === 'input, textarea, select');

    expect(rule).toBeDefined();
    expect(rule.body).toMatch(/font-size:\s*var\(--text-control\)/);
  });

  it('入力系セレクタが 16px 未満のトークンを font-size に使っていない', () => {
    const violations = [];

    for (const path of cssFiles) {
      for (const rule of parseRules(readCss(path))) {
        if (!CONTROL_SELECTOR_PATTERN.test(rule.selector)) continue;

        const fontSize = rule.body.match(/font-size:\s*([^;]+);/);
        if (!fontSize) continue;

        const value = fontSize[1].trim();
        if (SMALL_TOKENS.some((token) => value.includes(token))) {
          violations.push(`${path.replace(FRONTEND_DIR, '')}: ${rule.selector} → ${value}`);
        }
      }
    }

    expect(violations).toEqual([]);
  });
});

describe('viewport meta', () => {
  it('ピンチズームを禁止していない', () => {
    const html = readFileSync(join(FRONTEND_DIR, 'index.html'), 'utf8');
    const viewport = html.match(/<meta\s+name="viewport"\s+content="([^"]*)"/);

    expect(viewport).not.toBeNull();
    expect(viewport[1]).not.toMatch(/user-scalable\s*=\s*no/);
    expect(viewport[1]).not.toMatch(/maximum-scale/);
  });
});
