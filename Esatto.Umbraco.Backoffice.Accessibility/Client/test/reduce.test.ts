import { expect, test } from 'vitest';
import { reduceAxe, type AxeResultsLike } from '../src/scan/reduce';
import type { Job } from '../src/types';

const job: Job = { contentKey: 'k', name: 'Home', culture: 'sv', url: 'https://a.se/', viewport: 1280 };

const results: AxeResultsLike = {
  violations: [{
    id: 'image-alt', impact: 'critical', help: 'Images must have alternative text', helpUrl: 'https://dequeuniversity.com/rules/axe/4.14/image-alt',
    tags: ['wcag2a', 'wcag111'],
    nodes: [{ target: ['main', 'img.hero'], html: '<img class="hero" src="/media/a/x.jpg">' }],
  }],
  incomplete: [{
    id: 'color-contrast', impact: 'serious', help: 'Contrast', helpUrl: 'u', tags: ['wcag2aa', 'wcag143'],
    nodes: [{ target: ['p'], html: '<p>' }],
  }],
  passes: [{ id: 'document-title', impact: null, help: 'Title', helpUrl: 'u', tags: ['wcag2a', 'wcag242'], nodes: [] }],
};

test('reduceAxe maps violations and incomplete with selector and help', () => {
  const r = reduceAxe(results, job, ['https://a.se/r.pdf']);
  expect(r).toMatchObject({ contentKey: 'k', name: 'Home', culture: 'sv', url: 'https://a.se/', viewport: 1280, status: 'ok', reason: null });
  expect(r.rules).toEqual([
    { ruleId: 'image-alt', kind: 'violation', impact: 'critical', help: 'Images must have alternative text', helpUrl: 'https://dequeuniversity.com/rules/axe/4.14/image-alt', tags: ['wcag2a', 'wcag111'], nodes: [{ selector: 'main img.hero', html: '<img class="hero" src="/media/a/x.jpg">' }] },
    { ruleId: 'color-contrast', kind: 'incomplete', impact: 'serious', help: 'Contrast', helpUrl: 'u', tags: ['wcag2aa', 'wcag143'], nodes: [{ selector: 'p', html: '<p>' }] },
  ]);
  expect(r.passes).toEqual([{ ruleId: 'document-title', tags: ['wcag2a', 'wcag242'] }]);
  expect(r.documentLinks).toEqual(['https://a.se/r.pdf']);
});

test('nested targets (shadow DOM / iframes) are joined with " >>> "', () => {
  const r = reduceAxe({ violations: [{ ...results.violations[0], nodes: [{ target: [['x-card', 'button']], html: '<button>' }] }], incomplete: [], passes: [] }, job, []);
  expect(r.rules[0].nodes[0].selector).toBe('x-card >>> button');
});
