// A stand-in for the element a text is written in: it answers term() from the real en.ts, as Umbraco's localize controller does (a string is
// returned as it is, a function is CALLED with the arguments it was given), so a test of a view function reads the words the dashboard shows.

import en from '../lang/en';
import type { Localizer } from '../localize';

const texts = en.esattoSeoReport as Record<string, unknown>;

/** A localizer for tests: English texts from en.ts, the culture given (the backoffice's English is British, hence the default). */
export function enHost(culture = 'en-gb'): Localizer {
  return {
    localize: {
      term(key: string, ...args: unknown[]): string {
        const value = texts[key.replace(/^esattoSeoReport_/, '')];
        if (value === undefined) return key;
        return typeof value === 'function' ? (value as (...values: unknown[]) => string)(...args) : (value as string);
      },
      lang: () => culture,
    },
  };
}
