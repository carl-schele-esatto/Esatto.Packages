/// <reference types="vite/client" />
import { describe, expect, it } from 'vitest';
import { API_ERROR_CODES, GOOGLE_ERROR_CODES } from './error-codes';
import en from './lang/en';
import { errorKey, lookupKey } from './localize';
import { TAB_IDS } from './navigate';

// A text the code asks for must exist, or the dashboard shows the raw key ("esattoSeoReport_uiRefreshLabel"). Nothing else would
// notice: Umbraco answers a missing key with the key, and no compiler checks a string. This reads the source and compares.

const texts = en.esattoSeoReport as Record<string, unknown>;

// What a translation key of this set looks like: an area, a PascalCase id and a part (ReportKeys).
const KEY_SHAPE = /^(?:kpi|chart|series|annotation|insight|narrative|opportunity|target|error|ui)[A-Z][A-Za-z0-9]*(?:Label|Description|Title|Subtitle|Text)$/;

// text(this, 'uiRefreshLabel') or text(host, "uiRefreshLabel", args): the key is the literal second argument.
const TEXT_CALL = /\btext\(\s*[\w.$]+\s*,\s*(['"])(?<key>[^'"]+)\1/g;

// Any string literal that has the shape of a key: a table of keys, a key kept in a constant, an argument to lookup.
const STRING_LITERAL = /(['"`])(?<value>[A-Za-z][A-Za-z0-9]*)\1/g;

/** The keys a source file asks for by a literal: those passed to text(), and any other string that is shaped like a key. */
export function keysIn(source: string): string[] {
  const keys = new Set<string>();
  for (const match of source.matchAll(TEXT_CALL)) keys.add(match.groups!.key);
  for (const match of source.matchAll(STRING_LITERAL)) if (KEY_SHAPE.test(match.groups!.value)) keys.add(match.groups!.value);
  return [...keys].sort();
}

// The package's own source, as text: everything under src except the tests, the test fixtures and the texts themselves.
const sources = import.meta.glob(['./**/*.ts', '!./**/*.test.ts', '!./lang/**', '!./testing/**'], {
  query: '?raw',
  import: 'default',
  eager: true,
}) as Record<string, string>;

describe('the texts the source asks for', () => {
  it('scans the source files it should', () => {
    const files = Object.keys(sources);

    expect(files.length).toBeGreaterThan(8);
    expect(files).toContain('./localize.ts');
    expect(files).not.toContain('./lang/en.ts');
    expect(files.some((file) => file.includes('.test.'))).toBe(false);
  });

  it('are all in en.ts', () => {
    const missing: string[] = [];
    for (const [file, source] of Object.entries(sources)) {
      for (const key of keysIn(source)) if (!(key in texts)) missing.push(`${file}: ${key}`);
    }

    expect(missing).toEqual([]);
  });

  it('are found at all: the elements ask for texts, so a scan that finds none is a broken scan', () => {
    const asked = Object.values(sources).flatMap(keysIn);

    expect(asked.length).toBeGreaterThan(20);
    expect(asked).toContain('uiRefreshLabel');
  });
});

describe('the scan itself', () => {
  it('finds a key passed to text() with either quote, and a key kept in a table', () => {
    const source = `
      text(this, 'uiRefreshLabel');
      text(host, "uiFetchNowLabel", { a: 1 });
      const TABS = { analysis: 'uiTabAnalysisLabel' };
      const notAKey = 'someOtherString';
      const alsoNot = 'textLabel';
    `;

    expect(keysIn(source)).toEqual(['uiFetchNowLabel', 'uiRefreshLabel', 'uiTabAnalysisLabel']);
  });

  it('finds a key passed to text() that is not shaped like the keys we know (a typo in the area)', () => {
    expect(keysIn(`text(this, 'uiRefresh')`)).toEqual(['uiRefresh']);
  });

  it('reports a key that en.ts does not have', () => {
    expect(keysIn(`text(this, 'uiNoSuchText')`).filter((key) => !(key in texts))).toEqual(['uiNoSuchText']);
  });
});

describe('the texts the client builds a key for', () => {
  it('has a title and a text for every code of a problem, Google\'s and the API\'s', () => {
    for (const code of [...GOOGLE_ERROR_CODES, ...API_ERROR_CODES]) {
      for (const part of ['Title', 'Text'] as const) {
        const key = errorKey(code, part);

        expect(typeof texts[key], key).toMatch(/^(string|function)$/);
      }
    }
  });

  it('has a label for every tab', () => {
    for (const tab of TAB_IDS) {
      const key = `uiTab${tab[0].toUpperCase()}${tab.slice(1)}Label`;

      expect(typeof texts[key], key).toBe('string');
    }
  });

  it('has a label for every setting that can be missing', () => {
    for (const missing of ['key', 'ga4_property_id', 'search_console_site']) {
      const key = lookupKey('ui', `missing_${missing}`, 'Label');

      expect(typeof texts[key], key).toBe('string');
    }
  });

  it('calls the button that fetches one month exactly "Refresh", as the texts that point at it say', () => {
    expect(texts.uiRefreshLabel).toBe('Refresh');
    expect(texts.targetMissingNoQueriesText).toContain('"Refresh"');
  });
});
