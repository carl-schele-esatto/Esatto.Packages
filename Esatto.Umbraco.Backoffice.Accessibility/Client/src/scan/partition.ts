import type { Job, ScanPage } from '../types';

export const OTHER_ORIGIN = 'other-origin:';

export interface Partitioned {
  scannable: ScanPage[];
  skipped: (ScanPage & { skipReason: string })[];
}

/**
 * Splits the page list into pages the browser can scan and pages it must skip. A page on another origin
 * than the backoffice (scheme, host or port) can't be read from a frame, so it's skipped as
 * `other-origin:<origin>` rather than silently counted as clean.
 */
export function partitionPages(pages: ScanPage[], origin: string): Partitioned {
  const scannable: ScanPage[] = [];
  const skipped: (ScanPage & { skipReason: string })[] = [];
  for (const page of pages) {
    if (page.skipReason) {
      skipped.push({ ...page, skipReason: page.skipReason });
      continue;
    }
    const url = new URL(page.url);
    if (url.origin !== origin) skipped.push({ ...page, skipReason: `${OTHER_ORIGIN}${url.origin}` });
    else scannable.push(page);
  }
  return { scannable, skipped };
}

/** Why pages on other origins were skipped, and what to do about it. */
export function otherOriginsMessage(origins: string[], backofficeOrigin: string): string {
  const backoffice = new URL(backofficeOrigin);
  const list = origins.join(', ');
  const schemeOnly = origins.filter((o) => new URL(o).host === backoffice.host);
  const why = `Pages on ${list} were skipped: a browser can only scan pages on the same address as the backoffice (${backofficeOrigin}), meaning the same scheme, host and port.`;
  if (schemeOnly.length) {
    const scheme = (o: string | URL) => new URL(o).protocol.replace(':', '');
    return `${why} These pages use ${scheme(schemeOnly[0])} instead of ${scheme(backoffice)}. Set the site's domain to ${backofficeOrigin} in Culture and Hostnames, or open the backoffice on ${schemeOnly[0]}.`;
  }
  return `${why} Open the backoffice on ${origins[0]}${origins.length > 1 ? ' (and so on)' : ''} to scan them.`;
}

/** One job per page per viewport width, page by page. */
export function jobsFor(pages: Pick<ScanPage, 'contentKey' | 'name' | 'culture' | 'url'>[], viewports: number[]): Job[] {
  return pages.flatMap((p) => viewports.map((viewport) => ({
    contentKey: p.contentKey, name: p.name, culture: p.culture, url: p.url, viewport,
  })));
}
