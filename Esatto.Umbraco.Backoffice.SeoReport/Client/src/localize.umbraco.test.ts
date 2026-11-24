// What Umbraco REALLY does with the arguments of a function-valued text, and proof that text() meets it.
//
// This file runs Umbraco's own UmbLocalizationController and UmbLocalizationManager (from node_modules, unchanged) over the real en.ts,
// registered the way Umbraco's localization registry registers a dictionary: a section's entries flattened to "<section>_<key>".
// Only two things the controller imports for its HTML helpers are replaced (lit's unsafeHTML and the core escapeHTML), because the
// real ones pull in the whole backoffice UI library and need a browser; neither is used by term().
//
// What the controller does (localization.controller.js):
//   term(key, ...args)                    args is a REST array
//   #processTerm(term, args)              a function is called as term(...args); a string with args has %0% and {0} replaced
// So term(key, { a: 1 }) reaches the function as ONE object, and term(key) as no argument at all. A function with a default
// ("= {}") copes with no argument, but not with null: a default only replaces undefined, and destructuring null throws. text()
// therefore always passes exactly one object.

import { beforeAll, describe, expect, it, vi } from 'vitest';
import en from './lang/en';
import { text, type Localizer } from './localize';

vi.mock('@umbraco-cms/backoffice/external/lit', () => ({ unsafeHTML: (value: string) => value }));
vi.mock('@umbraco-cms/backoffice/utils', () => ({ escapeHTML: (value: unknown) => String(value) }));

// Umbraco's registry (core/localization/registry) flattens a dictionary like this: each entry becomes "<section>_<key>", values untouched.
function flatten(dictionaries: Record<string, Record<string, unknown>>): Record<string, unknown> {
  const flat: Record<string, unknown> = {};
  for (const [name, dictionary] of Object.entries(dictionaries)) {
    for (const [key, value] of Object.entries(dictionary)) flat[`${name}_${key}`] = value;
  }
  return flat;
}

let localizer: Localizer;
let controller: { term(key: string, ...args: unknown[]): string };

beforeAll(async () => {
  // The manager reads these when it is created.
  vi.stubGlobal('document', { documentElement: { lang: 'en', dir: 'ltr' } });
  vi.stubGlobal('navigator', { language: 'en' });
  vi.stubGlobal('requestAnimationFrame', (callback: () => void) => setTimeout(callback, 0));

  const { UmbLocalizationController, umbLocalizationManager } = await import('@umbraco-cms/backoffice/localization-api');

  umbLocalizationManager.registerLocalization({ $code: 'en', $dir: 'ltr', ...flatten(en) } as never);

  const host = { getHostElement: () => ({ lang: 'en' }), addUmbController: () => undefined, removeUmbController: () => undefined };
  controller = new UmbLocalizationController(host as never) as unknown as typeof controller;
  localizer = { localize: { term: (key, ...args) => controller.term(key, ...args), lang: () => 'en' } };
});

describe("Umbraco's localization controller, run for real", () => {
  it('calls a function-valued text with the one object it was given', () => {
    const said = controller.term('esattoSeoReport_targetPageNearText', { path: '/products/widgets', position: 14, queries: 2 });

    expect(said).toMatch(/^\/products\/widgets ranks at position 14\.0 for 2 search queries\. /);
  });

  it('does NOT call it with the object spread into positional arguments: that is what text() must never do', () => {
    // The first positional argument is then "/products/widgets", and destructuring a string finds no "path".
    const said = controller.term('esattoSeoReport_targetPageNearText', '/products/widgets', 14, 2);

    expect(said).toMatch(/^A page ranks for this search/);
  });

  it('throws when a destructuring function is handed null, which is why text() never passes null', () => {
    expect(() => controller.term('esattoSeoReport_targetPageNearText', null)).toThrow(TypeError);
  });

  it('calls a function with no argument at all when none is given, which the default object absorbs', () => {
    expect(controller.term('esattoSeoReport_kpiNoComparisonText')).toBe('no comparison: last year has no data');
  });

  it('leaves a plain string alone when an object is passed with it', () => {
    expect(controller.term('esattoSeoReport_uiGa4Label', {})).toBe('GA4');
  });

  it('returns the key itself for a key that is not registered', () => {
    expect(controller.term('esattoSeoReport_noSuchText')).toBe('esattoSeoReport_noSuchText');
  });
});

describe('text() through the real controller', () => {
  it('writes a text with its arguments', () => {
    expect(text(localizer, 'narrativeConversionText', { sessions: 30, events: 1 })).toBe('30 visits from search engines led to 1 key event.');
    expect(text(localizer, 'seriesSessionsLabel', { year: 2026 })).toBe('2026 visits');
  });

  it('writes a text the server sent without arguments, both as undefined and as null', () => {
    expect(text(localizer, 'uiGscLagText')).toBe('2-3 days of data lag in Search Console.');
    expect(text(localizer, 'uiGscLagText', undefined)).toBe('2-3 days of data lag in Search Console.');
    expect(text(localizer, 'uiGscLagText', null)).toBe('2-3 days of data lag in Search Console.');
  });

  it('writes a text whose arguments the server sent as explicit nulls', () => {
    const said = text(localizer, 'narrativeVisibilityText', {
      top3: 20,
      total: 200,
      percent: 10,
      comparePeriod: null,
      compareTop3: null,
      comparePercent: null,
      yearOnYear: null,
      bestPeriod: null,
      bestTop3: null,
      bestPercent: null,
    });

    expect(said).toBe('20 of your 200 most shown search queries rank in the top 3 (10.0%).');
  });

  it('writes every function-valued text with no arguments, with null and with an empty object, without throwing', () => {
    const functions = Object.entries(en.esattoSeoReport).filter(([, value]) => typeof value === 'function');

    expect(functions.length).toBeGreaterThan(10);
    for (const [key] of functions) {
      for (const args of [undefined, null, {}]) {
        expect(() => text(localizer, key, args), key).not.toThrow();
        expect(text(localizer, key, args), key).not.toBe(`esattoSeoReport_${key}`);
      }
    }
  });

  it('never lets a string text be changed by the placeholder rule Umbraco applies when arguments are passed', () => {
    // With an argument, Umbraco replaces %0% and {0} in a STRING. text() always passes one, so no text may contain either.
    const strings = Object.entries(en.esattoSeoReport).filter(([, value]) => typeof value === 'string') as [string, string][];

    expect(strings.length).toBeGreaterThan(10);
    for (const [key, value] of strings) expect(value, key).not.toMatch(/%\d+%|\{\d+\}/);
  });
});
