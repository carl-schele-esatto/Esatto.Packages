// Pure: what the block shows for a render outcome, and whether the request fingerprint should be
// kept (same data → no re-render) or forgotten (the next trigger must render again).

import type { RenderOutcome } from './api';

export type ViewState =
  | { kind: 'loading' }
  | { kind: 'rendered'; html: string; nonce: string }
  | { kind: 'plain' }
  | { kind: 'unsaved' }
  | { kind: 'error'; message: string };

export function toViewState(outcome: RenderOutcome, nonce: string): { view: ViewState; keepFingerprint: boolean } {
  switch (outcome.status) {
    case 'rendered':
      return { view: { kind: 'rendered', html: outcome.html, nonce }, keepFingerprint: true };
    case 'notPreviewable':
      // "Save the page once…": after saving, the block data is unchanged — keep it re-renderable.
      return outcome.reason === 'unsavedDocument'
        ? { view: { kind: 'unsaved' }, keepFingerprint: false }
        : { view: { kind: 'plain' }, keepFingerprint: true };
    default:
      return { view: { kind: 'error', message: outcome.message }, keepFingerprint: false };
  }
}

// Previews render against a document. A Block List nested inside a block (block workspace) or on a
// media/member type has no document to render against: show the plain row.
export function isPreviewableWorkspace(entityType: string | undefined): boolean {
  return entityType === 'document';
}
