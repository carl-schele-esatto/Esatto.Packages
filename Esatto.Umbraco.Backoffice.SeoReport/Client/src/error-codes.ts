// The fixed words a problem is named by. The server sends a `code` (and a few fixed arguments), never prose; the client has the words,
// two keys per code: error<Code>Title and error<Code>Text (see errorKey in localize.ts).
//
// These are the two C# lists, copied: Google/ErrorCodes.cs (what went wrong at Google or with the key) and Api/ApiCodes.cs (what is
// wrong with the request). ClientErrorCodesTests (C#) reads this file and fails when either list differs, so a code the server
// learns is not forgotten here. A code that is not in these lists is shown as `unknown`.

/** Google/ErrorCodes.cs: the key, Google and the property. */
export const GOOGLE_ERROR_CODES = [
  'notConfigured',
  'keyUnreadable',
  'keyInvalid',
  'apiDisabled',
  'noAccess',
  'propertyNotFound',
  'noData',
  'quota',
  'unknown',
  'ga4PropertyInvalid',
  'searchConsoleInvalid',
] as const;

/** Api/ApiCodes.cs: a request the API refuses. */
export const API_ERROR_CODES = [
  'periodInvalid',
  'periodOutOfRange',
  'dateInvalid',
  'dateOutOfRange',
  'alreadyRunning',
  'requestInvalid',
] as const;

export type ErrorCode = (typeof GOOGLE_ERROR_CODES)[number] | (typeof API_ERROR_CODES)[number];

const KNOWN: ReadonlySet<string> = new Set<string>([...GOOGLE_ERROR_CODES, ...API_ERROR_CODES]);

/** Whether <c>code</c> is one this client has words for. */
export function isKnownErrorCode(code: unknown): code is ErrorCode {
  return typeof code === 'string' && KNOWN.has(code);
}

/** The code the client shows for <c>code</c>: itself when it has words for it, otherwise `unknown`. */
export function displayCode(code: unknown): ErrorCode {
  return isKnownErrorCode(code) ? code : 'unknown';
}
