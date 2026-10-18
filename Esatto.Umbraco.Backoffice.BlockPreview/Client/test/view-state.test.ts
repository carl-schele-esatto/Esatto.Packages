import { describe, it, expect } from 'vitest';
import { toViewState, isPreviewableWorkspace } from '../src/view-state';

describe('toViewState', () => {
  it('rendered keeps the fingerprint (same data, no re-render)', () => {
    expect(toViewState({ status: 'rendered', html: '<p>x</p>' }, 'n1'))
      .toEqual({ view: { kind: 'rendered', html: '<p>x</p>', nonce: 'n1' }, keepFingerprint: true });
  });

  // Final review, Important 10: after "Save the page once…" the editor saves — the block data is
  // unchanged, so a kept fingerprint would block the re-render forever.
  it('unsaved forgets the fingerprint so the next trigger re-renders', () => {
    expect(toViewState({ status: 'notPreviewable', reason: 'unsavedDocument' }, 'n1'))
      .toEqual({ view: { kind: 'unsaved' }, keepFingerprint: false });
  });

  it('errors forget the fingerprint so the next change retries', () => {
    expect(toViewState({ status: 'error', message: 'boom' }, 'n1'))
      .toEqual({ view: { kind: 'error', message: 'boom' }, keepFingerprint: false });
  });

  it('no partial / ignored / unsupported show the plain row', () => {
    for (const reason of ['noPartial', 'ignored', 'unsupported'] as const) {
      expect(toViewState({ status: 'notPreviewable', reason }, 'n1').view).toEqual({ kind: 'plain' });
    }
  });
});

// Final review, Important 3 (+ minor 7): previews render against a document. A Block List nested in
// a block (block workspace) or on a media/member type gets the plain row.
describe('isPreviewableWorkspace', () => {
  it('accepts the document workspace', () => {
    expect(isPreviewableWorkspace('document')).toBe(true);
  });

  it('rejects block, media and member workspaces', () => {
    for (const entityType of ['block', 'media', 'member', 'document-blueprint', undefined]) {
      expect(isPreviewableWorkspace(entityType)).toBe(false);
    }
  });
});
