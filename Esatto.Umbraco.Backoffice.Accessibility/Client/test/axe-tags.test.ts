// @vitest-environment jsdom
import { describe, expect, test } from 'vitest';
import { axeTags } from '../src/axe-tags';

describe('axeTags', () => {
  test('2.2 AA emits A and AA tags of all versions', () =>
    expect(axeTags('V22', 'AA', false)).toEqual(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa', 'wcag22aa']));

  test('2.0 A emits only wcag2a', () => expect(axeTags('V20', 'A', false)).toEqual(['wcag2a']));

  test('2.1 AAA includes 2.0 AAA but not 2.2 tags', () =>
    expect(axeTags('V21', 'AAA', false)).toEqual(['wcag2a', 'wcag2aa', 'wcag2aaa', 'wcag21a', 'wcag21aa']));

  test('best practice is appended when enabled', () => expect(axeTags('V20', 'A', true)).toEqual(['wcag2a', 'best-practice']));

  test('every emitted tag exists in axe', async () => {
    const axe = (await import('axe-core')).default;
    const known = new Set(axe.getRules().flatMap((r) => r.tags));
    for (const v of ['V20', 'V21', 'V22'] as const)
      for (const l of ['A', 'AA', 'AAA'] as const)
        for (const t of axeTags(v, l, true)) expect(known.has(t), `${t} for ${v} ${l}`).toBe(true);
  });
});
