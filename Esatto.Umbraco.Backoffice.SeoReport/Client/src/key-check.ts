// An early word on a pasted service-account key, and reading a chosen key file, in the browser and without a DOM.
//
// THE SERVER DECIDES (ServiceAccountKey.Validate): a key is JSON with type "service_account", a client_email and a PEM private_key, at most
// 16 KB in UTF-8. This repeats those rules only to say, while the person is still on the form, that what they pasted cannot be a key (and whose
// key it is when it can); it never stops a save or a test, which the server answers for real.
//
// THE TEXT IS A SECRET. Nothing here logs, keeps or returns any of it: what comes back is a fixed code and the service account's e-mail (public:
// it is the address to grant access to). A parse error is caught and dropped without being looked at, because its message can quote the text.

/** The largest key, in UTF-8 bytes (ServiceAccountKey.MaxBytes). */
export const MAX_KEY_BYTES = 16 * 1024;

/**
 * The largest file that is read at all. A little over the key's own limit, because a byte order mark is not part of the text a file decodes to;
 * the text is then checked against MAX_KEY_BYTES like a paste.
 */
export const MAX_KEY_FILE_BYTES = MAX_KEY_BYTES + 1024;

export type KeyProblem = 'notJson' | 'notServiceAccount' | 'noClientEmail' | 'noPrivateKey' | 'tooLarge';

export interface KeyCheck {
  /** The service account's e-mail when the text looks like a key, otherwise null. */
  email: string | null;
  /** Why the text cannot be a key; null when it can be, and for an empty text. */
  problem: KeyProblem | null;
}

/** The UTF-8 length of a text, counted without making a copy of it (a lone surrogate counts as the three bytes of its replacement). */
export function utf8Length(text: string): number {
  let bytes = 0;
  for (let i = 0; i < text.length; i++) {
    const code = text.charCodeAt(i);
    if (code < 0x80) bytes += 1;
    else if (code < 0x800) bytes += 2;
    else if (code >= 0xd800 && code <= 0xdbff && (text.charCodeAt(i + 1) & 0xfc00) === 0xdc00) {
      bytes += 4;
      i++;
    } else bytes += 3;
  }
  return bytes;
}

const isRecord = (value: unknown): value is Record<string, unknown> => typeof value === 'object' && value !== null && !Array.isArray(value);

const filled = (value: unknown): value is string => typeof value === 'string' && value.trim().length > 0;

const looksLikePem = (value: unknown): boolean =>
  typeof value === 'string' && value.includes('-----BEGIN') && value.includes('PRIVATE KEY-----');

// The JSON in the text, or undefined. trim() also removes a byte order mark (ECMAScript counts U+FEFF as white space), and a comma before a
// closing bracket is dropped, because the server tolerates both.
function parsed(text: string): unknown {
  try {
    return JSON.parse(text.trim().replace(/,(\s*[}\]])/g, '$1'));
  } catch {
    return undefined;
  }
}

/** Whether the text can be a service-account key, and whose. See the file's header: a hint for the form, never a decision. */
export function checkKey(text: string | null | undefined): KeyCheck {
  if (!text || text.trim().length === 0) return { email: null, problem: null };

  // The server counts the text as it arrives, before anything is trimmed.
  if (text.length > MAX_KEY_BYTES || utf8Length(text) > MAX_KEY_BYTES) return { email: null, problem: 'tooLarge' };

  const key = parsed(text);
  if (!isRecord(key)) return { email: null, problem: 'notJson' };
  if (key.type !== 'service_account') return { email: null, problem: 'notServiceAccount' };
  if (!filled(key.client_email)) return { email: null, problem: 'noClientEmail' };
  if (!looksLikePem(key.private_key)) return { email: null, problem: 'noPrivateKey' };

  return { email: key.client_email.trim(), problem: null };
}

/** What a key file needs to be read: a File has both. */
export interface KeyFile {
  readonly size: number;
  text(): Promise<string>;
}

export type KeyFileRead = { ok: true; text: string } | { ok: false; problem: 'tooLarge' | 'unreadable' };

/**
 * The text of a chosen key file, read in the browser. A file over MAX_KEY_FILE_BYTES is refused WITHOUT being read; a file that cannot be read
 * is `unreadable`, and why is not looked at (the reason can name the file or quote it). Nothing is logged.
 */
export async function readKeyFile(file: KeyFile): Promise<KeyFileRead> {
  if (file.size > MAX_KEY_FILE_BYTES) return { ok: false, problem: 'tooLarge' };

  try {
    return { ok: true, text: await file.text() };
  } catch {
    return { ok: false, problem: 'unreadable' };
  }
}
