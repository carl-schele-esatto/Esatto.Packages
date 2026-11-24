import { describe, expect, it } from 'vitest';
import { composeSearchConsole, normalizeSearchConsole, parseSearchConsole, searchConsoleCheck, searchConsoleView } from './search-console';
import vectors from './testing/search-console-vectors.json';

// The Search Console field is a choice (domain property or URL prefix) and a text. What is sent is composed here the way the server's
// SettingsNormalizer.NormalizeSearchConsole would store it, so the form can show what will be saved; the server stays the authority.

// search-console-vectors.json is the shared truth: SearchConsoleVectorsTests (C#) holds every row to what the server's normalizer returns, so
// the expected values are the server's own, and this holds the client's copy to the same rows. Neither side can drift alone.
describe('normalizeSearchConsole', () => {
  it('has the shared vectors to check, with both kinds of property and refusals', () => {
    expect(vectors.length).toBeGreaterThanOrEqual(150);
    expect(vectors.some((row) => row.expected === null)).toBe(true);
    expect(vectors.some((row) => row.expected?.startsWith('sc-domain:'))).toBe(true);
    expect(vectors.some((row) => row.expected?.startsWith('https://'))).toBe(true);
  });

  it('gives what the server stores for every shared vector', () => {
    const wrong = vectors
      .map((row) => ({ ...row, actual: normalizeSearchConsole(row.input) }))
      .filter((row) => row.actual !== row.expected)
      .map((row) => `${JSON.stringify(row.input).slice(0, 80)} gave ${JSON.stringify(row.actual)}, the server ${JSON.stringify(row.expected)}`);

    expect(wrong).toEqual([]);
  });

  it('changes nothing when what it gives is normalized again, as the server', () => {
    // A stored value can be one character over the limit (2048 typed, the closing slash added); normalized again it is refused, on the server too.
    for (const row of vectors) {
      if (row.expected !== null && row.expected.length <= 2048) expect(normalizeSearchConsole(row.expected), row.expected).toBe(row.expected);
    }
  });
});

describe('searchConsoleView', () => {
  it('previews what the server will store', () => {
    expect(searchConsoleView('domain', 'Example.COM/')).toEqual({ sent: 'sc-domain:example.com', stored: 'sc-domain:example.com', warning: null, line: 'preview' });
    expect(searchConsoleView('prefix', 'HTTPS://www.example.com/Shop')).toMatchObject({ stored: 'https://www.example.com/Shop/', line: 'preview' });
  });

  it('shows the warning instead of the preview when an address has no scheme, and says it will be a domain property', () => {
    expect(searchConsoleView('prefix', 'www.example.com')).toEqual({
      sent: 'sc-domain:www.example.com',
      stored: 'sc-domain:www.example.com',
      warning: 'missingScheme',
      line: 'warning',
    });
  });

  it('says it will not be accepted when the server refuses it, with the reason when there is one, and never previews it', () => {
    expect(searchConsoleView('domain', 'https://www.example.com/')).toMatchObject({ stored: null, warning: 'looksLikeUrl', line: 'refused' });
    for (const [kind, text] of [
      ['domain', 'example.com/blog'],
      ['domain', 'localhost'],
      ['domain', 'exa mple.com'],
      ['prefix', 'example.com/blog'],
      ['prefix', 'https://example.com/?'],
      ['prefix', 'https://example.com/#'],
      ['prefix', 'https://example.com/a b'],
    ] as const) {
      expect(searchConsoleView(kind, text), `${kind} ${text}`).toMatchObject({ stored: null, line: 'refused' });
    }
  });

  it('says nothing for an empty field', () => {
    expect(searchConsoleView('domain', '  ')).toEqual({ sent: '', stored: null, warning: null, line: 'none' });
  });
});

describe('composeSearchConsole', () => {
  it('writes a domain property with the sc-domain: prefix', () => {
    expect(composeSearchConsole('domain', 'example.com')).toBe('sc-domain:example.com');
  });

  it('writes a URL prefix with its closing slash', () => {
    expect(composeSearchConsole('prefix', 'https://www.example.com')).toBe('https://www.example.com/');
  });

  it('lower-cases a domain, drops one closing slash and the white space around it, and does not double the prefix', () => {
    expect(composeSearchConsole('domain', '  Example.COM/ ')).toBe('sc-domain:example.com');
    expect(composeSearchConsole('domain', 'SC-Domain:Example.com')).toBe('sc-domain:example.com');
  });

  it('lower-cases the scheme and the host of a URL prefix and keeps the case of its path', () => {
    expect(composeSearchConsole('prefix', 'HTTPS://WWW.Example.com/Shop')).toBe('https://www.example.com/Shop/');
    expect(composeSearchConsole('prefix', 'http://example.com/blog/')).toBe('http://example.com/blog/');
  });

  it('drops the default port of a URL prefix and keeps any other', () => {
    expect(composeSearchConsole('prefix', 'https://www.example.com:443/')).toBe('https://www.example.com/');
    expect(composeSearchConsole('prefix', 'https://www.example.com:8443')).toBe('https://www.example.com:8443/');
  });

  it('sends an address without a scheme as the server keeps it: a domain property, never a guessed scheme', () => {
    expect(composeSearchConsole('prefix', 'www.example.com')).toBe('sc-domain:www.example.com');
  });

  it('sends what was typed when the server refuses it, so the server says so with its code', () => {
    expect(composeSearchConsole('prefix', 'https://www.example.com/?q=1')).toBe('https://www.example.com/?q=1');
    expect(composeSearchConsole('prefix', 'https://user@www.example.com/')).toBe('https://user@www.example.com/');
    expect(composeSearchConsole('prefix', 'https://')).toBe('https://');
  });

  it('treats a domain property typed under URL prefix as the domain property it is, as the server does', () => {
    expect(composeSearchConsole('prefix', 'sc-domain:Example.com')).toBe('sc-domain:example.com');
  });

  it('is empty for an empty text: the field is cleared, not set to a bare prefix', () => {
    expect(composeSearchConsole('domain', '')).toBe('');
    expect(composeSearchConsole('prefix', '   ')).toBe('');
  });
});

describe('parseSearchConsole', () => {
  it('reads a domain property back as the domain choice and the host', () => {
    expect(parseSearchConsole('sc-domain:example.com')).toEqual({ kind: 'domain', text: 'example.com' });
  });

  it('reads a URL prefix back as the URL-prefix choice and the address', () => {
    expect(parseSearchConsole('https://www.example.com/')).toEqual({ kind: 'prefix', text: 'https://www.example.com/' });
  });

  it('round-trips both kinds', () => {
    for (const [kind, text] of [
      ['domain', 'example.com'],
      ['prefix', 'https://www.example.com'],
    ] as const) {
      const site = composeSearchConsole(kind, text);
      const parsed = parseSearchConsole(site);

      expect(parsed.kind).toBe(kind);
      expect(composeSearchConsole(parsed.kind, parsed.text)).toBe(site);
    }
  });

  it('starts an empty field on the domain choice', () => {
    expect(parseSearchConsole(null)).toEqual({ kind: 'domain', text: '' });
    expect(parseSearchConsole('')).toEqual({ kind: 'domain', text: '' });
  });

  it('shows anything else as a URL prefix with the text as it is', () => {
    expect(parseSearchConsole('not a property')).toEqual({ kind: 'prefix', text: 'not a property' });
  });
});

describe('searchConsoleCheck', () => {
  it('warns when a full address is typed as a domain property', () => {
    expect(searchConsoleCheck('domain', 'https://www.example.com/')).toBe('looksLikeUrl');
  });

  it('warns when a URL prefix has no scheme: the server would store it as a domain property', () => {
    expect(searchConsoleCheck('prefix', 'www.example.com')).toBe('missingScheme');
  });

  it('says nothing about an empty field or a well-formed one', () => {
    expect(searchConsoleCheck('domain', '')).toBeNull();
    expect(searchConsoleCheck('domain', 'example.com')).toBeNull();
    expect(searchConsoleCheck('prefix', 'https://www.example.com/')).toBeNull();
    expect(searchConsoleCheck('prefix', 'sc-domain:example.com')).toBeNull();
  });
});
