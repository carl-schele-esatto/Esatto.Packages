import { afterEach, beforeEach, describe, expect, it, vi, type MockInstance } from 'vitest';
import { MAX_KEY_BYTES, MAX_KEY_FILE_BYTES, checkKey, readKeyFile, utf8Length } from './key-check';
import { FAKE_KEY, FAKE_KEY_MARKER, fakeKey } from './testing/settings-fixtures';

// The early word on a pasted key, before anything is sent. The server checks the key for real (ServiceAccountKey.Validate) and has the last
// word; this only says, while the person is still on the form, that what they pasted cannot be a key, and whose key it is when it can.

// Written as a char code: an escape sequence for it can end up in the file as the invisible character itself.
const BOM = String.fromCharCode(0xfeff);

describe('checkKey', () => {
  it('reads the service account e-mail of a well-formed key', () => {
    expect(checkKey(FAKE_KEY)).toEqual({ email: 'svc@proj.iam.gserviceaccount.com', problem: null });
  });

  it('says nothing about an empty box', () => {
    expect(checkKey('')).toEqual({ email: null, problem: null });
    expect(checkKey('  \n ')).toEqual({ email: null, problem: null });
  });

  it('accepts a paste wrapped in a byte order mark, CRLF line ends and white space, and a trailing comma, as the server does', () => {
    const wrapped = `${BOM}\r\n  ${FAKE_KEY.replace(/\n/g, '\r\n').replace(/}$/, ',}')}\r\n`;

    expect(checkKey(wrapped)).toEqual({ email: 'svc@proj.iam.gserviceaccount.com', problem: null });
  });

  it('names what is wrong: not JSON, not a service account, no e-mail, no private key', () => {
    expect(checkKey('this is not json').problem).toBe('notJson');
    expect(checkKey('[1, 2]').problem).toBe('notJson');
    expect(checkKey('null').problem).toBe('notJson');
    expect(checkKey(fakeKey({ type: 'authorized_user' })).problem).toBe('notServiceAccount');
    expect(checkKey(fakeKey({ client_email: undefined })).problem).toBe('noClientEmail');
    expect(checkKey(fakeKey({ client_email: '  ' })).problem).toBe('noClientEmail');
    expect(checkKey(fakeKey({ private_key: undefined })).problem).toBe('noPrivateKey');
    expect(checkKey(fakeKey({ private_key: 'not a pem' })).problem).toBe('noPrivateKey');
  });

  it('refuses a text over 16 KB in UTF-8, counted before anything is trimmed, as the server counts it', () => {
    const padded = FAKE_KEY + ' '.repeat(MAX_KEY_BYTES - utf8Length(FAKE_KEY) + 1);

    expect(checkKey(padded).problem).toBe('tooLarge');
    expect(checkKey(padded.slice(0, -1)).problem).toBeNull();
  });

  it('never puts any of the text it was given into what it returns', () => {
    for (const text of [FAKE_KEY, `${FAKE_KEY} trailing`, fakeKey({ type: 'x' }), fakeKey({ client_email: undefined })]) {
      const said = JSON.stringify(checkKey(text));

      expect(said).not.toContain(FAKE_KEY_MARKER);
      expect(said).not.toContain('PRIVATE KEY');
    }
  });
});

describe('utf8Length', () => {
  it('counts the UTF-8 bytes of a text, a character outside the BMP as four', () => {
    expect(utf8Length('abc')).toBe(3);
    expect(utf8Length('å')).toBe(2);
    expect(utf8Length('€')).toBe(3);
    expect(utf8Length('😀')).toBe(4);
  });
});

describe('readKeyFile', () => {
  let spies: MockInstance[];

  beforeEach(() => {
    spies = (['log', 'info', 'warn', 'error', 'debug', 'trace'] as const).map((name) => vi.spyOn(console, name).mockImplementation(() => {}));
  });

  afterEach(() => {
    for (const spy of spies) spy.mockRestore();
  });

  const file = (text: string, size = utf8Length(text)) => ({ size, text: vi.fn(async () => text) });

  it('reads the text of the chosen file, and logs nothing', async () => {
    const chosen = file(FAKE_KEY);

    expect(await readKeyFile(chosen)).toEqual({ ok: true, text: FAKE_KEY });
    expect(chosen.text).toHaveBeenCalledTimes(1);
    for (const spy of spies) expect(spy).not.toHaveBeenCalled();
  });

  it('refuses a file that is too big without reading it', async () => {
    const big = file('x', MAX_KEY_FILE_BYTES + 1);

    expect(await readKeyFile(big)).toEqual({ ok: false, problem: 'tooLarge' });
    expect(big.text).not.toHaveBeenCalled();
  });

  it('reads a file a little over the limit, whose text may still fit once a byte order mark is gone', async () => {
    const near = file(FAKE_KEY, MAX_KEY_BYTES + 3);

    expect((await readKeyFile(near)).ok).toBe(true);
  });

  it('says the file could not be read when reading fails, without the reason and without logging it', async () => {
    const broken = { size: 10, text: vi.fn(async () => Promise.reject(new Error(`could not read ${FAKE_KEY_MARKER}`))) };

    const read = await readKeyFile(broken);

    expect(read).toEqual({ ok: false, problem: 'unreadable' });
    expect(JSON.stringify(read)).not.toContain(FAKE_KEY_MARKER);
    for (const spy of spies) expect(spy).not.toHaveBeenCalled();
  });
});
