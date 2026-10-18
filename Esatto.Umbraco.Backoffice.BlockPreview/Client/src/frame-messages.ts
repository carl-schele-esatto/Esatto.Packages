// Pure: accept a height message only from this block's own iframe and current render.

export const HEIGHT_MESSAGE_TYPE = 'esatto-block-preview:height';

// Upper bound for one preview. Content sized to the viewport (min-height: 100vh) grows with the
// frame, which would otherwise feed back into an ever-growing iframe.
export const MAX_PREVIEW_HEIGHT = 3000;

export function readHeight(event: { source: unknown; data: unknown }, expectedSource: unknown, nonce: string): number | null {
  if (event.source !== expectedSource) return null;
  const data = event.data as { type?: unknown; id?: unknown; height?: unknown } | null;
  if (!data || typeof data !== 'object') return null;
  if (data.type !== HEIGHT_MESSAGE_TYPE || data.id !== nonce) return null;
  const { height } = data;
  if (typeof height !== 'number' || !Number.isFinite(height) || height <= 0) return null;
  return Math.min(Math.ceil(height), MAX_PREVIEW_HEIGHT);
}

// Embedded players the preview asks the backoffice to show over an element. Video players such as
// YouTube can't run inside the sandboxed preview (no storage, no referrer), so the backoffice
// shows them outside it — but only these players, never arbitrary pages.
export const EMBEDS_MESSAGE_TYPE = 'esatto-block-preview:embeds';

const EMBED_HOSTS = new Set(['www.youtube-nocookie.com', 'youtube-nocookie.com', 'www.youtube.com', 'youtube.com']);

export interface EmbedRect {
  top: number;
  left: number;
  width: number;
  height: number;
}

export interface Embed {
  url: string;
  rect: EmbedRect;
}

export function isAllowedEmbedUrl(url: string): boolean {
  let parsed: URL;
  try {
    parsed = new URL(url);
  } catch {
    return false;
  }
  return parsed.protocol === 'https:' && EMBED_HOSTS.has(parsed.hostname) && parsed.pathname.startsWith('/embed/');
}

function readRect(value: unknown): EmbedRect | null {
  const r = value as Partial<Record<keyof EmbedRect, unknown>> | null;
  if (!r || typeof r !== 'object') return null;
  const numbers = [r.top, r.left, r.width, r.height];
  if (!numbers.every((n) => typeof n === 'number' && Number.isFinite(n))) return null;
  const [top, left, width, height] = numbers as number[];
  if (width <= 0 || height <= 0) return null;
  return { top: Math.max(0, top), left: Math.max(0, left), width, height };
}

/** The embeds in a message from this block's own iframe and render; null for any other message. */
export function readEmbeds(event: { source: unknown; data: unknown }, expectedSource: unknown, nonce: string): Embed[] | null {
  if (event.source !== expectedSource) return null;
  const data = event.data as { type?: unknown; id?: unknown; embeds?: unknown } | null;
  if (!data || typeof data !== 'object') return null;
  if (data.type !== EMBEDS_MESSAGE_TYPE || data.id !== nonce || !Array.isArray(data.embeds)) return null;
  return data.embeds.flatMap((item: unknown) => {
    const embed = item as { url?: unknown; rect?: unknown } | null;
    if (!embed || typeof embed.url !== 'string' || !isAllowedEmbedUrl(embed.url)) return [];
    const rect = readRect(embed.rect);
    return rect ? [{ url: embed.url, rect }] : [];
  });
}
