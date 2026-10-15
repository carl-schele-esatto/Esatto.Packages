import { describe, it, expect } from 'vitest';
import { readEmbeds, isAllowedEmbedUrl, EMBEDS_MESSAGE_TYPE } from '../src/frame-messages';

const frame = {};
const msg = (data: unknown, source: unknown = frame) => ({ source, data });
const YT = 'https://www.youtube-nocookie.com/embed/h969bLwxgKc?autoplay=1&rel=0';
const rect = { top: 10, left: 20, width: 640, height: 360 };

// 1.0.2: YouTube can't play inside the sandboxed preview (no storage, no referrer → Error 153).
// The preview asks the backoffice to show the player over the poster instead — only for
// known video players, only from this block's own frame and render.
describe('isAllowedEmbedUrl', () => {
  it('accepts YouTube embed players', () => {
    expect(isAllowedEmbedUrl(YT)).toBe(true);
    expect(isAllowedEmbedUrl('https://www.youtube.com/embed/h969bLwxgKc')).toBe(true);
    expect(isAllowedEmbedUrl('https://youtube-nocookie.com/embed/h969bLwxgKc')).toBe(true);
  });

  it('rejects anything that is not an https YouTube /embed/ URL', () => {
    expect(isAllowedEmbedUrl('http://www.youtube-nocookie.com/embed/h969bLwxgKc')).toBe(false);
    expect(isAllowedEmbedUrl('https://www.youtube.com/watch?v=h969bLwxgKc')).toBe(false);
    expect(isAllowedEmbedUrl('https://evil.example/embed/x')).toBe(false);
    expect(isAllowedEmbedUrl('https://www.youtube.com.evil.example/embed/x')).toBe(false);
    expect(isAllowedEmbedUrl('javascript:alert(1)')).toBe(false);
    expect(isAllowedEmbedUrl('/umbraco/section/content')).toBe(false);
    expect(isAllowedEmbedUrl('not a url')).toBe(false);
  });
});

describe('readEmbeds', () => {
  it('reads the embeds from this frame with the current nonce', () => {
    expect(readEmbeds(msg({ type: EMBEDS_MESSAGE_TYPE, id: 'n1', embeds: [{ url: YT, rect }] }), frame, 'n1'))
      .toEqual([{ url: YT, rect }]);
  });

  it('an empty list clears the embeds', () => {
    expect(readEmbeds(msg({ type: EMBEDS_MESSAGE_TYPE, id: 'n1', embeds: [] }), frame, 'n1')).toEqual([]);
  });

  it('rejects messages from another frame, a stale render or another type', () => {
    const data = { type: EMBEDS_MESSAGE_TYPE, id: 'n1', embeds: [{ url: YT, rect }] };
    expect(readEmbeds(msg(data, {}), frame, 'n1')).toBeNull();
    expect(readEmbeds(msg(data), frame, 'other')).toBeNull();
    expect(readEmbeds(msg({ ...data, type: 'esatto-block-preview:height' }), frame, 'n1')).toBeNull();
    expect(readEmbeds(msg(null), frame, 'n1')).toBeNull();
    expect(readEmbeds(msg({ type: EMBEDS_MESSAGE_TYPE, id: 'n1', embeds: 'nope' }), frame, 'n1')).toBeNull();
  });

  it('drops embeds with a URL that is not allowed', () => {
    const embeds = [{ url: 'https://evil.example/embed/x', rect }, { url: YT, rect }];
    expect(readEmbeds(msg({ type: EMBEDS_MESSAGE_TYPE, id: 'n1', embeds }), frame, 'n1')).toEqual([{ url: YT, rect }]);
  });

  it('drops embeds with a malformed or empty rectangle', () => {
    const embeds = [
      { url: YT, rect: { top: 0, left: 0, width: 0, height: 360 } },
      { url: YT, rect: { top: 'x', left: 0, width: 640, height: 360 } },
      { url: YT, rect: { top: Number.NaN, left: 0, width: 640, height: 360 } },
      { url: YT },
    ];
    expect(readEmbeds(msg({ type: EMBEDS_MESSAGE_TYPE, id: 'n1', embeds }), frame, 'n1')).toEqual([]);
  });

  it('keeps an embed inside the preview: negative offsets become 0', () => {
    const embeds = [{ url: YT, rect: { top: -5, left: -8, width: 640, height: 360 } }];
    expect(readEmbeds(msg({ type: EMBEDS_MESSAGE_TYPE, id: 'n1', embeds }), frame, 'n1'))
      .toEqual([{ url: YT, rect: { top: 0, left: 0, width: 640, height: 360 } }]);
  });
});
