import type { JobOutcome } from './queue';

/** What a plain same-origin request tells us about a page before it's framed. */
export interface PreflightResponse {
  /** Final URL after redirects. */
  url: string;
  status: number;
  contentType: string | null;
  xFrameOptions: string | null;
  csp: string | null;
}

/**
 * Decides from the response whether the page can be scanned. Only real framing headers stop the whole
 * scan ("blocked", every page will refuse); an error status, a redirect to another site or a download
 * fail just that page. Null means: go ahead and frame it.
 */
export function classifyPreflight(r: PreflightResponse, origin: string): JobOutcome | null {
  if (r.status >= 400) return { kind: 'failed', reason: `HTTP ${r.status}` };

  const finalOrigin = new URL(r.url).origin;
  if (finalOrigin !== origin) return { kind: 'failed', reason: `redirects to ${finalOrigin}` };

  const type = (r.contentType ?? '').split(';')[0].trim().toLowerCase();
  if (type && type !== 'text/html' && type !== 'application/xhtml+xml') return { kind: 'failed', reason: `not an HTML page (${type})` };

  const xfo = (r.xFrameOptions ?? '').trim().toUpperCase();
  if (xfo === 'DENY') return { kind: 'blocked', reason: 'The site sends X-Frame-Options: DENY.' };

  const ancestors = /frame-ancestors\s+([^;]*)/i.exec(r.csp ?? '')?.[1].trim().toLowerCase();
  if (ancestors !== undefined && (ancestors === "'none'" || !/('self'|\*)/.test(ancestors) && !ancestors.includes(origin.toLowerCase()))) {
    return { kind: 'blocked', reason: `The site sends Content-Security-Policy frame-ancestors ${ancestors}.` };
  }
  return null;
}

/** Requests the page the way the frame will (same origin, cookies), following redirects. */
export async function preflight(url: string): Promise<PreflightResponse> {
  const r = await fetch(url, { credentials: 'same-origin', redirect: 'follow', headers: { Accept: 'text/html' } });
  // The body isn't needed; cancel it so a large page doesn't download twice.
  r.body?.cancel().catch(() => {});
  return {
    url: r.url || url,
    status: r.status,
    contentType: r.headers.get('content-type'),
    xFrameOptions: r.headers.get('x-frame-options'),
    csp: r.headers.get('content-security-policy'),
  };
}
