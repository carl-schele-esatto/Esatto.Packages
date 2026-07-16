// Decides whether the current backoffice surface should resolve content-dictionary
// `#Key` tokens (translate) or show them raw.
//
// Rule (per product decision): the **Content** section (Innehåll) always translates;
// every other section (Settings/Inställningar, Media, Members, Users, Translation, …)
// shows the raw `#Label.Name` token. This lets editors see translations while admins
// configuring doctypes see the actual dictionary keys.
//
// Detection is by backoffice route: Umbraco routes sections at
// `/umbraco/section/<alias>/…`, so the Content section is `/section/content`.

/**
 * Pure predicate: does this backoffice URL point at the Content section?
 * `content-blueprints` (or any `content<something>`) is deliberately NOT matched.
 */
export function isContentSurface(href: string): boolean {
  return /\/section\/content(?![\w-])/i.test(href);
}

/** Reads the live location. Falls back to `false` (show raw token) if unavailable. */
export function currentIsContentSurface(): boolean {
  try {
    return isContentSurface(globalThis.location?.href ?? "");
  } catch {
    return false;
  }
}
