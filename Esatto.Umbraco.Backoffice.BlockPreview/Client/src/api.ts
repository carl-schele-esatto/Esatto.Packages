import { umbHttpClient } from '@umbraco-cms/backoffice/http-client';
import type { RenderRequestBody } from './render-request';

export type RenderOutcome =
  | { status: 'rendered'; html: string }
  | { status: 'notPreviewable'; reason: 'noPartial' | 'ignored' | 'unsavedDocument' | 'unsupported' }
  | { status: 'error'; message: string };

const ENDPOINT = '/umbraco/management/api/v1/esatto/block-preview/render';

// `security: [bearer]` makes the shared backoffice client attach the user's token.
// The signal cancels the fetch itself when a newer render supersedes this one.
export function renderBlockPreview(body: RenderRequestBody, signal?: AbortSignal) {
  return umbHttpClient.post<{ 200: RenderOutcome }>({
    security: [{ scheme: 'bearer', type: 'http' }],
    url: ENDPOINT,
    body,
    signal,
    headers: { 'Content-Type': 'application/json' },
  });
}
