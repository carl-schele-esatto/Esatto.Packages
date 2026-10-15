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
