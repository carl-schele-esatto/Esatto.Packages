import { describe, it, expect } from 'vitest';
import { toCsv, csvSeparators } from '../src/csv';

describe('csv', () => {
  it('decimal-comma locales use ; and ,', () => expect(csvSeparators('sv-SE')).toEqual({ field: ';', decimal: ',' }));
  it('decimal-point locales use , and .', () => expect(csvSeparators('en-US')).toEqual({ field: ',', decimal: '.' }));

  it('formats numbers per locale without grouping', () => {
    expect(toCsv(['a', 'b'], [[1234.5, 'x']], 'sv-SE')).toBe('﻿a;b\r\n1234,5;x');
    expect(toCsv(['a', 'b'], [[1234.5, 'x']], 'en-US')).toBe('﻿a,b\r\n1234.5,x');
  });

  // Review Focus 4.
  it('quotes fields with separators, quotes or newlines', () =>
    expect(toCsv(['name'], [['Doe, Jane "JD"'], ['two\nlines'], ['a;b']], 'en-US'))
      .toBe('﻿name\r\n"Doe, Jane ""JD"""\r\n"two\nlines"\r\na;b'));
});
