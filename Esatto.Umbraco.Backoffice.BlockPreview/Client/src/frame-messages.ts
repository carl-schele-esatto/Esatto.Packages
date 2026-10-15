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
