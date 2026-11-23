import { describe, expect, it } from 'vitest';
import { fillText } from './fill-text';

describe('fillText', () => {
  it('puts each part where its marker is, and keeps the text around it', () => {
    expect(fillText('No {0} was found. See the {1} for how.', ['file', 'README'])).toEqual(['No ', 'file', ' was found. See the ', 'README', ' for how.']);
  });

  it('keeps a part as it is, so an element or a template stays one', () => {
    const element = { tag: 'code' };

    expect(fillText('Built from {0}.', [element])[1]).toBe(element);
  });

  it('can place the same part twice and parts side by side', () => {
    expect(fillText('{0}{1}-{0}', ['a', 'b'])).toEqual(['a', 'b', '-', 'a']);
  });

  it('returns a text without markers as one piece', () => {
    expect(fillText('Nothing to fill.', ['unused'])).toEqual(['Nothing to fill.']);
  });

  it('returns nothing for an empty text', () => {
    expect(fillText('', [])).toEqual([]);
  });

  it('leaves a marker that has no part visible instead of hiding the gap', () => {
    expect(fillText('See {1}.', ['only the first'])).toEqual(['See ', '{1}', '.']);
  });

  it('does not treat braces that are not a number as markers', () => {
    expect(fillText('Use {name} and { 0 }.', ['x'])).toEqual(['Use {name} and { 0 }.']);
  });
});
