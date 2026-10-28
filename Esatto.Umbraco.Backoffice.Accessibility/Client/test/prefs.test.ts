import { describe, expect, test } from 'vitest';
import { ATTRIBUTES, attributesFor, parseOptions, readPrefs, writePrefs, type PrefKey } from '../public-site/prefs';

const ALL: PrefKey[] = ['text', 'spacing', 'contrast', 'motion', 'font', 'links', 'guide'];
const noOs = { reducedMotion: false, moreContrast: false };

describe('attributesFor', () => {
  test('OS reduced motion sets motion=reduce when following', () =>
    expect(attributesFor({}, { reducedMotion: true, moreContrast: false }, { motion: true, contrast: false }, [])).toEqual({ 'data-a11y-motion': 'reduce' }));

  test('OS reduced motion is ignored when not following', () =>
    expect(attributesFor({}, { reducedMotion: true, moreContrast: false }, { motion: false, contrast: false }, [])).toEqual({}));

  test('explicit pref false overrides OS', () =>
    expect(attributesFor({ motion: false }, { reducedMotion: true, moreContrast: false }, { motion: true, contrast: false }, ALL)).toEqual({}));

  test('contrast not followed by default', () =>
    expect(attributesFor({}, { reducedMotion: false, moreContrast: true }, { motion: true, contrast: false }, ALL)).toEqual({}));

  test('contrast followed when enabled', () =>
    expect(attributesFor({}, { reducedMotion: false, moreContrast: true }, { motion: false, contrast: true }, [])).toEqual({ 'data-a11y-contrast': 'high' }));

  test('explicit prefs apply only for options the panel offers', () =>
    expect(attributesFor({ text: true, font: true }, noOs, { motion: false, contrast: false }, ['text'])).toEqual({ 'data-a11y-text': 'large' }));

  test('every option maps to its documented attribute', () =>
    expect(attributesFor(Object.fromEntries(ALL.map((k) => [k, true])), noOs, { motion: false, contrast: false }, ALL)).toEqual({
      'data-a11y-text': 'large', 'data-a11y-spacing': 'wide', 'data-a11y-contrast': 'high', 'data-a11y-motion': 'reduce',
      'data-a11y-font': 'readable', 'data-a11y-links': 'highlight', 'data-a11y-guide': 'on',
    }));
});

test('attribute names match the documented contract', () =>
  expect(Object.values(ATTRIBUTES).map(([name]) => name).sort()).toEqual([
    'data-a11y-contrast', 'data-a11y-font', 'data-a11y-guide', 'data-a11y-links', 'data-a11y-motion', 'data-a11y-spacing', 'data-a11y-text',
  ]));

describe('storage', () => {
  const memory = () => {
    const data = new Map<string, string>();
    return { getItem: (k: string) => data.get(k) ?? null, setItem: (k: string, v: string) => void data.set(k, v), removeItem: (k: string) => void data.delete(k), data };
  };

  test('corrupt storage reads as empty prefs', () => {
    const s = memory();
    s.setItem('esatto-a11y', '{not json');
    expect(readPrefs(s)).toEqual({});
  });

  test('non-boolean and unknown values are dropped', () => {
    const s = memory();
    s.setItem('esatto-a11y', JSON.stringify({ text: true, spacing: 'yes', bogus: true }));
    expect(readPrefs(s)).toEqual({ text: true });
  });

  test('round trip under the esatto-a11y key', () => {
    const s = memory();
    writePrefs(s, { motion: false, links: true });
    expect(JSON.parse(s.data.get('esatto-a11y')!)).toEqual({ motion: false, links: true });
    expect(readPrefs(s)).toEqual({ motion: false, links: true });
  });

  test('missing or throwing storage reads as empty', () => {
    expect(readPrefs(null)).toEqual({});
    expect(readPrefs({ getItem: () => { throw new Error('denied'); } })).toEqual({});
  });
});

test('parseOptions keeps only known keys', () => expect(parseOptions('text, guide,bogus,,')).toEqual(['text', 'guide']));
