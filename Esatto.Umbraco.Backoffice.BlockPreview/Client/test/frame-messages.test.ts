import { describe, it, expect } from 'vitest';
import { readHeight, HEIGHT_MESSAGE_TYPE, MAX_PREVIEW_HEIGHT } from '../src/frame-messages';

const frame = {};
const msg = (data: unknown, source: unknown = frame) => ({ source, data });

// Review Focus 4.
describe('readHeight', () => {
  it('accepts a height from this frame with the current nonce', () => {
    expect(readHeight(msg({ type: HEIGHT_MESSAGE_TYPE, id: 'n1', height: 512 }), frame, 'n1')).toBe(512);
  });

  it('rejects a message from another frame', () => {
    expect(readHeight(msg({ type: HEIGHT_MESSAGE_TYPE, id: 'n1', height: 512 }, {}), frame, 'n1')).toBeNull();
  });

  it('rejects a stale nonce', () => {
    expect(readHeight(msg({ type: HEIGHT_MESSAGE_TYPE, id: 'old', height: 512 }), frame, 'n1')).toBeNull();
  });

  it('rejects other message types and malformed data', () => {
    expect(readHeight(msg({ type: 'other', id: 'n1', height: 512 }), frame, 'n1')).toBeNull();
    expect(readHeight(msg('not an object'), frame, 'n1')).toBeNull();
    expect(readHeight(msg(null), frame, 'n1')).toBeNull();
  });

  it('rejects non-finite or non-positive heights', () => {
    for (const height of [0, -5, Number.NaN, Number.POSITIVE_INFINITY, '512']) {
      expect(readHeight(msg({ type: HEIGHT_MESSAGE_TYPE, id: 'n1', height }), frame, 'n1')).toBeNull();
    }
  });

  it('rounds up fractional heights', () => {
    expect(readHeight(msg({ type: HEIGHT_MESSAGE_TYPE, id: 'n1', height: 100.2 }), frame, 'n1')).toBe(101);
  });
});

// Final review, Important 5: content sized to the viewport (min-height: 100vh) would otherwise grow
// the frame forever (frame grows → vh grows → content grows → …).
describe('readHeight cap', () => {
  it('caps the height at MAX_PREVIEW_HEIGHT', () => {
    expect(readHeight(msg({ type: HEIGHT_MESSAGE_TYPE, id: 'n1', height: 50_000 }), frame, 'n1')).toBe(MAX_PREVIEW_HEIGHT);
  });
});
