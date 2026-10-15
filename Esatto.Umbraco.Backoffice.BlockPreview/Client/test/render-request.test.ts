import { describe, it, expect } from 'vitest';
import { buildRenderRequest, requestFingerprint, type RenderInput } from '../src/render-request';

const input = (over: Partial<RenderInput> = {}): RenderInput => ({
  documentKey: 'doc',
  documentTypeKey: 'doctype',
  propertyAlias: 'carouselList',
  culture: 'sv',
  segment: null,
  nonce: 'n1',
  content: { key: 'c1', contentTypeKey: 'ct', values: [{ alias: 'images', value: [{ mediaKey: 'm1' }], editorAlias: 'Umbraco.MediaPicker3' }] },
  settings: undefined,
  ...over,
});

describe('buildRenderRequest', () => {
  it('maps the contexts and block data', () => {
    const body = buildRenderRequest(input());
    expect(body).toEqual({
      documentKey: 'doc',
      documentTypeKey: 'doctype',
      propertyAlias: 'carouselList',
      culture: 'sv',
      segment: null,
      nonce: 'n1',
      content: input().content,
      settings: null,
    });
  });

  it('passes settings through when present', () => {
    const settings = { key: 's1', contentTypeKey: 'st', values: [] };
    expect(buildRenderRequest(input({ settings })).settings).toEqual(settings);
  });

  it('treats an invariant variant as culture null', () => {
    expect(buildRenderRequest(input({ culture: undefined })).culture).toBeNull();
  });
});

// Review Focus 5: unchanged data must not re-render.
describe('requestFingerprint', () => {
  it('ignores the nonce', () => {
    expect(requestFingerprint(buildRenderRequest(input({ nonce: 'a' }))))
      .toBe(requestFingerprint(buildRenderRequest(input({ nonce: 'b' }))));
  });

  it('changes when a value changes', () => {
    const changed = input({ content: { key: 'c1', contentTypeKey: 'ct', values: [{ alias: 'images', value: [] }] } });
    expect(requestFingerprint(buildRenderRequest(changed))).not.toBe(requestFingerprint(buildRenderRequest(input())));
  });

  it('changes when the culture changes', () => {
    expect(requestFingerprint(buildRenderRequest(input({ culture: 'en-GB' }))))
      .not.toBe(requestFingerprint(buildRenderRequest(input())));
  });
});
