// Backoffice deep links and document-link detection.

/** Opens the content node in the backoffice (with its culture for variant content). */
export function editContentUrl(key: string, culture: string): string {
  const base = `/umbraco/section/content/workspace/document/edit/${key}`;
  return culture ? `${base}/${culture}` : base;
}

/** Opens the media item in the backoffice. */
export function editMediaUrl(key: string): string {
  return `/umbraco/section/media/workspace/media/edit/${key}`;
}

/**
 * The URL if it's absolute http(s), otherwise undefined. Every link built from scan data goes through
 * this: results are posted by browsers, so a stored `javascript:` URL must never become a clickable href.
 */
export function safeHref(url: string | null | undefined): string | undefined {
  try {
    const u = new URL(url ?? '');
    return u.protocol === 'http:' || u.protocol === 'https:' ? u.toString() : undefined;
  } catch {
    return undefined;
  }
}

const DOCUMENT = /\.(pdf|docx?|xlsx|pptx)$/i;

/** The distinct links that point to documents (PDF and Office files), which must be checked separately. */
export function documentLinksIn(hrefs: Iterable<string>): string[] {
  const out = new Set<string>();
  for (const href of hrefs) {
    try {
      if (DOCUMENT.test(new URL(href).pathname)) out.add(href);
    } catch {
      // not an absolute URL: ignore
    }
  }
  return [...out];
}
