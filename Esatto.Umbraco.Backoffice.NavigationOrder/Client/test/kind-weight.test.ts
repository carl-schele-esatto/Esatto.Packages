import { describe, expect, it } from 'vitest';
import { withKindWeight } from '../src/apply/kind-weight';
import { section } from './fixtures';

describe('withKindWeight', () => {
  it("fills in the kind's weight for a manifest without its own (Umb.Kind.Section.Default weighs 1000)", () => {
    const raw = { ...section('My.Section'), kind: 'default' };
    expect(withKindWeight(raw, { weight: 1000 }).weight).toBe(1000);
    expect(raw.weight).toBeUndefined();
  });

  it("keeps the manifest's own weight", () => {
    const raw = { ...section('My.Section', 5), kind: 'default' };
    expect(withKindWeight(raw, { weight: 1000 })).toBe(raw);
  });

  it('leaves a manifest without a kind weight alone', () => {
    const raw = section('ai');
    expect(withKindWeight(raw, undefined)).toBe(raw);
    expect(withKindWeight(raw, {})).toBe(raw);
  });
});
