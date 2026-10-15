import { umbHttpClient } from '@umbraco-cms/backoffice/http-client';
import type { RenderRequestBody } from './render-request';

export type RenderOutcome =
  | { status: 'rendered'; html: string }
  | { status: 'notPreviewable'; reason: 'noPartial' | 'ignored' | 'unsavedDocument' }
  | { status: 'error'; message: string };

const ENDPOINT = '/umbraco/management/api/v1/esatto/block-preview/render';

// `security: [bearer]` makes the shared backoffice client attach the user's token.
export function renderBlockPreview(body: RenderRequestBody) {
  return umbHttpClient.post<{ 200: RenderOutcome }>({
    security: [{ scheme: 'bearer', type: 'http' }],
    url: ENDPOINT,
    body,
    headers: { 'Content-Type': 'application/json' },
  });
}
