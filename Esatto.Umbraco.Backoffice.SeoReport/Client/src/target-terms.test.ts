import { describe, expect, it } from 'vitest';
import { MAX_TERMS, MAX_TERM_LENGTH, brandTermsCheck, enteredTerms, termsCheck, termsFromText, textFromTerms } from './target-terms';

describe('the limits', () => {
  it('are the server\'s: 50 target queries of at most 100 characters', () => {
    expect(MAX_TERMS).toBe(50);
    expect(MAX_TERM_LENGTH).toBe(100);
  });
});

describe('enteredTerms (what is sent: the server normalizes)', () => {
  it('is one term per line, trimmed, without the blank lines, in the order typed', () => {
    expect(enteredTerms('  Blue Widgets \n\n\twidget repair\r\n   \nWidget Repair')).toEqual(['Blue Widgets', 'widget repair', 'Widget Repair']);
  });

  it('is empty for nothing', () => {
    expect(enteredTerms('')).toEqual([]);
    expect(enteredTerms(' \n \n')).toEqual([]);
    expect(enteredTerms(null)).toEqual([]);
  });

  it('reads a lone carriage return as a line break too', () => {
    expect(enteredTerms('a\rb')).toEqual(['a', 'b']);
  });
});

describe('termsFromText (a preview of what the server keeps)', () => {
  it('trims, collapses white space inside a term, lower-cases, and drops blanks and duplicates, keeping the first and the order', () => {
    expect(termsFromText('  Blue   Widgets \n\nwidget\trepair\nBLUE WIDGETS\nsecond')).toEqual(['blue widgets', 'widget repair', 'second']);
  });

  it('drops a term that is too long and does not shorten it', () => {
    const tooLong = 'x'.repeat(MAX_TERM_LENGTH + 1);
    const longest = 'y'.repeat(MAX_TERM_LENGTH);

    expect(termsFromText(`${tooLong}\n${longest}\nshort`)).toEqual([longest, 'short']);
  });

  it('stops at the most terms a list holds: it is the last ones that go', () => {
    const lines = Array.from({ length: MAX_TERMS + 5 }, (_, index) => `term ${index}`);
    const kept = termsFromText(lines.join('\n'));

    expect(kept).toHaveLength(MAX_TERMS);
    expect(kept[0]).toBe('term 0');
    expect(kept[MAX_TERMS - 1]).toBe(`term ${MAX_TERMS - 1}`);
  });
});

describe('termsCheck (the counters)', () => {
  it('counts the target queries that are entered once each, however they are written', () => {
    expect(termsCheck('One\nTWO\n one \n\n')).toEqual({ count: 2, kept: 2, tooMany: false, tooLong: 0 });
  });

  it('says when there are more than the list holds, and how many are kept', () => {
    const lines = Array.from({ length: MAX_TERMS + 3 }, (_, index) => `term ${index}`).join('\n');

    expect(termsCheck(lines)).toEqual({ count: MAX_TERMS + 3, kept: MAX_TERMS, tooMany: true, tooLong: 0 });
    expect(termsCheck(Array.from({ length: MAX_TERMS }, (_, index) => `term ${index}`).join('\n')).tooMany).toBe(false);
  });

  it('counts the terms that are too long separately, and not against the limit of the list', () => {
    const lines = [`${'x'.repeat(MAX_TERM_LENGTH + 1)}`, `${'z'.repeat(MAX_TERM_LENGTH + 50)}`, 'fine'].join('\n');

    expect(termsCheck(lines)).toEqual({ count: 1, kept: 1, tooMany: false, tooLong: 2 });
  });

  it('is zero for nothing', () => {
    expect(termsCheck('')).toEqual({ count: 0, kept: 0, tooMany: false, tooLong: 0 });
  });
});

describe('brandTermsCheck (the counter under the brand terms on the Settings tab: SettingsNormalizer.NormalizeList)', () => {
  it('counts each brand term once, ignoring case and extra white space', () => {
    expect(brandTermsCheck('Acme\nACME\n  acme   ab \nAcme\tAB\n\n')).toEqual({ count: 2, kept: 2, tooMany: false, tooLong: 0 });
  });

  it('says when there are more than the list holds, and counts the terms that are too long apart', () => {
    const many = Array.from({ length: MAX_TERMS + 2 }, (_, index) => `Brand ${index}`);

    expect(brandTermsCheck(many.join('\n'))).toEqual({ count: MAX_TERMS + 2, kept: MAX_TERMS, tooMany: true, tooLong: 0 });
    expect(brandTermsCheck(`${'x'.repeat(MAX_TERM_LENGTH + 1)}\nAcme`)).toEqual({ count: 1, kept: 1, tooMany: false, tooLong: 1 });
  });

  it('measures a term in the spelling it is kept in, not lower-cased', () => {
    // U+0130 lower-cases to two code units: as a target query the term would be one character too long.
    const term = `İ${'x'.repeat(MAX_TERM_LENGTH - 1)}`;

    expect(brandTermsCheck(term)).toEqual({ count: 1, kept: 1, tooMany: false, tooLong: 0 });
    expect(termsCheck(term).tooLong).toBe(1);
  });
});

describe('textFromTerms', () => {
  it('is one term per line, and the text of the list reads back as the same list', () => {
    const terms = ['blue widgets', 'widget repair'];

    expect(textFromTerms(terms)).toBe('blue widgets\nwidget repair');
    expect(termsFromText(textFromTerms(terms))).toEqual(terms);
  });

  it('is empty for no terms and tolerates a missing list', () => {
    expect(textFromTerms([])).toBe('');
    expect(textFromTerms(null)).toBe('');
  });
});
