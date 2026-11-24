// The Search Console field: a choice of the KIND of property (a domain property or a URL prefix) and a text, the value that goes to the server for
// them, and what the server will make of it. Without a DOM.
//
// THE SERVER IS THE AUTHORITY (SettingsNormalizer.NormalizeSearchConsole), and normalizeSearchConsole below is a copy of it, so the form can say
// before a save exactly what will be stored, or that it will be refused. The two are held to the same list of inputs and answers,
// testing/search-console-vectors.json: SearchConsoleVectorsTests (C#) checks every answer against the server, search-console.test.ts this copy.
// A rule that changes on one side fails on the other.
//
// What is SENT for a kind and a text: a domain with its sc-domain: prefix, an address as typed; then, when the server accepts it, the value it
// stores (so the request carries the stored form), otherwise the text as it is, for the server to refuse with its code. The scheme of an address is
// never guessed: "www.example.com" under URL prefix is what the server makes of it, the DOMAIN property sc-domain:www.example.com, which the form
// says with a warning instead of a preview.

export type SearchConsoleKind = 'domain' | 'prefix';

export interface SearchConsoleField {
  kind: SearchConsoleKind;
  text: string;
}

const DOMAIN_PREFIX = 'sc-domain:';
const MAX_SITE_LENGTH = 2048;
const MAX_HOST_LENGTH = 253;
const MAX_LABEL_LENGTH = 63;
const MAX_PORT = 65535;

// ---- The server's character rules (.NET's char.IsWhiteSpace, char.IsControl, char.IsLetterOrDigit, string.Trim) ------------------------------------

// .NET white space: the Unicode space separators, the line and paragraph separators, TAB to CR and NEL. Unlike JavaScript's \s it does not
// include the byte order mark, and it does include NEL.
const isWhiteSpace = (code: number): boolean =>
  (code >= 0x09 && code <= 0x0d) ||
  code === 0x20 ||
  code === 0x85 ||
  code === 0xa0 ||
  code === 0x1680 ||
  (code >= 0x2000 && code <= 0x200a) ||
  code === 0x2028 ||
  code === 0x2029 ||
  code === 0x202f ||
  code === 0x205f ||
  code === 0x3000;

const isControl = (code: number): boolean => code <= 0x1f || (code >= 0x7f && code <= 0x9f);

// One UTF-16 unit, as .NET tests a char: a half of a surrogate pair is neither.
const LETTER_OR_DIGIT = /^[\p{L}\p{Nd}]$/u;

function trimmed(text: string): string {
  let start = 0;
  let end = text.length;
  while (start < end && isWhiteSpace(text.charCodeAt(start))) start++;
  while (end > start && isWhiteSpace(text.charCodeAt(end - 1))) end--;
  return text.slice(start, end);
}

const startsWithIgnoringCase = (text: string, prefix: string): boolean =>
  text.length >= prefix.length && text.slice(0, prefix.length).toUpperCase() === prefix.toUpperCase();

// ToLowerInvariant: one unit at a time, the simple mapping (a letter whose full lower case is two units keeps the first, as U+0130 gives "i").
const lowerInvariant = (text: string): string =>
  Array.from(text.split(''), (unit) => {
    const lower = unit.toLowerCase();
    return lower.length === 1 ? lower : lower[0];
  }).join('');

// ---- Domain properties -----------------------------------------------------------------------------------------------------------------------

const isLabel = (label: string): boolean =>
  label.length > 0 &&
  label.length <= MAX_LABEL_LENGTH &&
  label[0] !== '-' &&
  label[label.length - 1] !== '-' &&
  label.split('').every((unit) => unit === '-' || LETTER_OR_DIGIT.test(unit));

// Search Console can only verify a public name, so a single label ("localhost") is not one.
function isDomainName(host: string): boolean {
  if (host.length === 0 || host.length > MAX_HOST_LENGTH) return false;
  const labels = host.split('.');
  return labels.length >= 2 && labels.every(isLabel);
}

function domainProperty(host: string): string | null {
  const name = host.endsWith('/') ? host.slice(0, -1) : host; // one closing slash is tolerated, as in "example.com/"
  return isDomainName(name) ? DOMAIN_PREFIX + lowerInvariant(name) : null;
}

// ---- URL prefixes: what .NET's Uri makes of an http(s) address -------------------------------------------------------------------------------

// An IPv4 address in any form .NET's Uri reads one (1 to 4 parts, each decimal, octal with a leading 0, or hex with 0x; the last part fills the
// bytes that are left), written the usual way; null when the host is not one (it is then read as a name: "999.1.1.1" is a name).
function ipv4(host: string): string | null {
  const parts = host.split('.');
  if (parts.length > 4) return null;

  const values: number[] = [];
  for (const part of parts) {
    if (/^0[xX][0-9a-fA-F]+$/.test(part)) values.push(parseInt(part.slice(2), 16));
    else if (/^0[0-7]*$/.test(part)) values.push(part.length > 1 ? parseInt(part, 8) : 0);
    else if (/^[1-9][0-9]*$/.test(part)) values.push(parseInt(part, 10));
    else return null;
  }

  const last = values[values.length - 1];
  if (values.slice(0, -1).some((value) => value > 255) || last > 256 ** (5 - values.length) - 1) return null;

  let address = last;
  values.slice(0, -1).forEach((value, index) => (address += value * 256 ** (3 - index)));
  return [address / 2 ** 24, address / 2 ** 16, address / 2 ** 8, address].map((part) => Math.floor(part) % 256).join('.');
}

// A bracketed IPv6 address written the usual way (lower case, the longest run of zeros as ::), or null when it is not one.
function ipv6(host: string): string | null {
  try {
    return new URL(`http://${host}/`).hostname;
  } catch {
    return null;
  }
}

const isHex = (unit: string | undefined): boolean => unit !== undefined && /^[0-9a-fA-F]$/.test(unit);
const UNRESERVED = /^[A-Za-z0-9\-._~]$/;
// The characters .NET escapes in a path (white space and control characters never get here).
const ESCAPED = new Set(['"', '<', '>', '`', '{', '}', '|', '^']);
const hex = (code: number): string => `%${code.toString(16).toUpperCase().padStart(2, '0')}`;

// How many bytes a UTF-8 sequence that starts with this byte has; 0 for a byte that cannot start one.
const sequenceLength = (byte: number): number => (byte >= 0xc2 && byte <= 0xdf ? 2 : byte >= 0xe0 && byte <= 0xef ? 3 : byte >= 0xf0 && byte <= 0xf4 ? 4 : 0);

// The escapes of a valid UTF-8 sequence that starts at `at`, in upper case, and how many characters of the path they take; null when the escapes
// there are not one (.NET then keeps them as they were typed).
function utf8Escapes(path: string, at: number, first: number): { text: string; length: number } | null {
  const count = sequenceLength(first);
  if (count === 0) return null;

  const bytes: number[] = [];
  for (let index = 0; index < count; index++) {
    const position = at + index * 3;
    if (path[position] !== '%' || !isHex(path[position + 1]) || !isHex(path[position + 2])) return null;
    bytes.push(parseInt(path.slice(position + 1, position + 3), 16));
  }

  try {
    new TextDecoder('utf-8', { fatal: true }).decode(new Uint8Array(bytes));
  } catch {
    return null;
  }
  return { text: bytes.map(hex).join(''), length: count * 3 };
}

// The path's escapes as .NET writes them: an escaped unreserved character is unescaped, an escaped UTF-8 character is written in upper case, any
// other escape is kept as typed, a % that starts no escape is %25; a character outside ASCII is escaped as UTF-8, and so are " < > ` { } | ^.
// Null for a lone half of a surrogate pair.
function escapedPath(path: string): string | null {
  let out = '';
  for (let index = 0; index < path.length; ) {
    const unit = path[index];

    if (unit === '%') {
      if (!isHex(path[index + 1]) || !isHex(path[index + 2])) {
        out += '%25';
        index += 1;
        continue;
      }
      const byte = parseInt(path.slice(index + 1, index + 3), 16);
      const sequence = byte >= 0x80 ? utf8Escapes(path, index, byte) : null;
      if (byte < 0x80 && UNRESERVED.test(String.fromCharCode(byte))) out += String.fromCharCode(byte);
      else if (sequence) {
        out += sequence.text;
        index += sequence.length;
        continue;
      } else out += path.slice(index, index + 3);
      index += 3;
      continue;
    }

    const code = path.codePointAt(index)!;
    const width = code > 0xffff ? 2 : 1;
    if (code > 0x7f) {
      try {
        out += encodeURIComponent(String.fromCodePoint(code));
      } catch {
        return null;
      }
    } else out += ESCAPED.has(unit) ? hex(code) : unit;
    index += width;
  }
  return out;
}

// RFC 3986 section 5.2.4: "." and ".." segments taken out, as .NET's Uri does.
function withoutDotSegments(path: string): string {
  let input = path;
  const output: string[] = [];
  while (input.length > 0) {
    if (input.startsWith('../')) input = input.slice(3);
    else if (input.startsWith('./')) input = input.slice(2);
    else if (input.startsWith('/./')) input = input.slice(2);
    else if (input === '/.') input = '/';
    else if (input.startsWith('/../')) {
      input = input.slice(3);
      output.pop();
    } else if (input === '/..') {
      input = '/';
      output.pop();
    } else if (input === '.' || input === '..') input = '';
    else {
      const next = input.indexOf('/', input.startsWith('/') ? 1 : 0);
      const segment = next < 0 ? input : input.slice(0, next);
      output.push(segment);
      input = input.slice(segment.length);
    }
  }
  return output.join('');
}

// The host and port of an authority, written as they are stored; null when the server refuses them. `path` is what .NET reads as the start of the
// path when something follows an IPv6 address's closing bracket that is not a port.
function authorityOf(authority: string, defaultPort: number): { authority: string; pathPrefix: string } | null {
  let host: string;
  let portText: string | null = null;
  let pathPrefix = '';

  if (authority.startsWith('[')) {
    const close = authority.indexOf(']');
    if (close < 0) return null;
    const after = authority.slice(close + 1);
    if (after.startsWith(':')) portText = after.slice(1);
    else if (after !== '') pathPrefix = `/${after}`;
    const address = ipv6(authority.slice(0, close + 1));
    if (!address) return null;
    host = address;
  } else {
    const colon = authority.lastIndexOf(':');
    const name = lowerInvariant(colon < 0 ? authority : authority.slice(0, colon));
    if (colon >= 0) portText = authority.slice(colon + 1);
    const address = ipv4(name);
    if (!address && !isDomainName(name)) return null;
    host = address ?? name;
  }

  if (portText === null || portText === '') return { authority: host, pathPrefix };
  if (!/^[0-9]+$/.test(portText) || Number(portText) > MAX_PORT) return null;

  const port = Number(portText);
  return { authority: port === defaultPort ? host : `${host}:${port}`, pathPrefix };
}

// An http(s) address: scheme and host in lower case, the default port dropped, the path as .NET writes it and always ending in "/". Null for
// credentials, a query, a fragment, a host that is neither a name nor an IP address, or a port that is not one.
function urlPrefixProperty(text: string): string | null {
  if (text.includes('?') || text.includes('#')) return null;

  const schemeEnd = text.indexOf('://');
  const scheme = text.slice(0, schemeEnd).toLowerCase();
  const rest = text.slice(schemeEnd + 3);
  const slash = rest.indexOf('/');
  const rawAuthority = slash < 0 ? rest : rest.slice(0, slash);
  if (rawAuthority.includes('@')) return null;

  const authority = authorityOf(rawAuthority, scheme === 'https' ? 443 : 80);
  if (!authority) return null;

  // A backslash in the path is a slash to .NET's Uri.
  const escaped = escapedPath(authority.pathPrefix + (slash < 0 ? '' : rest.slice(slash)).replace(/\\/g, '/'));
  if (escaped === null) return null;

  const path = withoutDotSegments(escaped || '/') || '/';
  return `${scheme}://${authority.authority}${path.endsWith('/') ? path : `${path}/`}`;
}

/**
 * What the server stores for a Search Console property as it is sent, or null when it refuses it: SettingsNormalizer.NormalizeSearchConsole,
 * ported. A bare host (one closing slash allowed) or sc-domain:host is the domain property sc-domain:host; an http(s) address is a URL prefix;
 * anything else, white space or a control character anywhere, and more than 2048 characters, typed or as it would be stored (a URL prefix given
 * its closing slash), are refused.
 */
export function normalizeSearchConsole(input: string | null | undefined): string | null {
  const text = trimmed(input ?? '');
  if (text.length === 0 || text.length > MAX_SITE_LENGTH) return null;
  for (let index = 0; index < text.length; index++) {
    const code = text.charCodeAt(index);
    if (isWhiteSpace(code) || isControl(code)) return null;
  }

  const normalized = startsWithIgnoringCase(text, DOMAIN_PREFIX)
    ? domainProperty(text.slice(DOMAIN_PREFIX.length))
    : startsWithIgnoringCase(text, 'http://') || startsWithIgnoringCase(text, 'https://')
      ? urlPrefixProperty(text)
      : domainProperty(text);
  return normalized !== null && normalized.length <= MAX_SITE_LENGTH ? normalized : null;
}

// ---- The form --------------------------------------------------------------------------------------------------------------------------------

// The text as the server reads it for the kind chosen: a domain with its sc-domain: prefix (unless it has one), an address as typed.
function asTyped(kind: SearchConsoleKind, text: string | null | undefined): string {
  const typed = (text ?? '').trim();
  if (!typed) return '';
  return kind === 'domain' && !startsWithIgnoringCase(typed, DOMAIN_PREFIX) ? DOMAIN_PREFIX + typed : typed;
}

/** The value sent for a kind and a text: what the server stores when it accepts it, the text as it reads it otherwise, '' for an empty text. */
export function composeSearchConsole(kind: SearchConsoleKind, text: string | null | undefined): string {
  const sent = asTyped(kind, text);
  return sent === '' ? '' : (normalizeSearchConsole(sent) ?? sent);
}

/**
 * The kind and the text the form shows for a saved value: sc-domain:example.com is the domain example.com, anything else is shown as a URL prefix
 * with the value as it is. An empty value starts on the domain choice.
 */
export function parseSearchConsole(site: string | null | undefined): SearchConsoleField {
  const value = (site ?? '').trim();
  if (!value) return { kind: 'domain', text: '' };

  return startsWithIgnoringCase(value, DOMAIN_PREFIX) ? { kind: 'domain', text: value.slice(DOMAIN_PREFIX.length) } : { kind: 'prefix', text: value };
}

/**
 * A word of warning about a text that does not fit the kind chosen: a full address under domain property, or an address without http:// or
 * https:// under URL prefix. Null when there is nothing to say. What it comes to (refused, or kept as a domain property) is searchConsoleView's.
 */
export function searchConsoleCheck(kind: SearchConsoleKind, text: string | null | undefined): 'looksLikeUrl' | 'missingScheme' | null {
  const typed = (text ?? '').trim();
  if (!typed) return null;

  const http = /^https?:\/\//i.test(typed);
  if (kind === 'domain') return http ? 'looksLikeUrl' : null;
  return http || startsWithIgnoringCase(typed, DOMAIN_PREFIX) ? null : 'missingScheme';
}

export interface SearchConsoleView {
  /** What is sent ('' for an empty field). */
  sent: string;
  /** What the server will store; null when it will refuse it (or the field is empty). */
  stored: string | null;
  warning: 'looksLikeUrl' | 'missingScheme' | null;
  /**
   * The one line under the field: nothing (empty), the preview ("Will be saved as"), the warning (it would be kept, but not as the kind chosen; the
   * preview is not shown), or that the server will refuse it (with the warning as the reason, when there is one; never a preview).
   */
  line: 'none' | 'preview' | 'warning' | 'refused';
}

export function searchConsoleView(kind: SearchConsoleKind, text: string | null | undefined): SearchConsoleView {
  const sent = composeSearchConsole(kind, text);
  if (!sent) return { sent: '', stored: null, warning: null, line: 'none' };

  const stored = normalizeSearchConsole(sent);
  const warning = searchConsoleCheck(kind, text);
  return { sent, stored, warning, line: stored === null ? 'refused' : warning ? 'warning' : 'preview' };
}
