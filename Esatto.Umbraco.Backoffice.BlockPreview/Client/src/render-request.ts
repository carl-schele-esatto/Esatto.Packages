// Pure: the body sent to the render endpoint, and a fingerprint deciding whether a
// re-render is needed (the nonce is per render, so it's not part of it).

export interface BlockDataValue {
  alias: string;
  culture?: string | null;
  segment?: string | null;
  value?: unknown;
  editorAlias?: string;
}

export interface BlockData {
  key: string;
  contentTypeKey: string;
  values: BlockDataValue[];
}

export interface RenderInput {
  documentKey: string;
  documentTypeKey: string;
  propertyAlias: string;
  culture: string | null | undefined;
  segment: string | null | undefined;
  nonce: string;
  content: BlockData;
  settings: BlockData | undefined;
}

export interface RenderRequestBody {
  documentKey: string;
  documentTypeKey: string;
  propertyAlias: string;
  culture: string | null;
  segment: string | null;
  nonce: string;
  content: BlockData;
  settings: BlockData | null;
}

export function buildRenderRequest(input: RenderInput): RenderRequestBody {
  return {
    documentKey: input.documentKey,
    documentTypeKey: input.documentTypeKey,
    propertyAlias: input.propertyAlias,
    culture: input.culture ?? null,
    segment: input.segment ?? null,
    nonce: input.nonce,
    content: input.content,
    settings: input.settings ?? null,
  };
}

export function requestFingerprint(body: RenderRequestBody): string {
  const { nonce: _nonce, ...rest } = body;
  return JSON.stringify(rest);
}

// Per-render id. crypto.randomUUID only exists in secure contexts (https or localhost); a backoffice
// on plain http would otherwise fail every render. getRandomValues works everywhere.
export function newNonce(): string {
  if (typeof globalThis.crypto?.randomUUID === 'function') return globalThis.crypto.randomUUID();
  const bytes = globalThis.crypto.getRandomValues(new Uint8Array(16));
  return Array.from(bytes, (b) => b.toString(16).padStart(2, '0')).join('');
}
