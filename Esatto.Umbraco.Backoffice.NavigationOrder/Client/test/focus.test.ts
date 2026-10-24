import { describe, expect, it } from 'vitest';
import { focusTargetOf } from '../src/drag/focus';

const focusable = () => ({ focus: () => {} });

describe('focusTargetOf', () => {
  it("is the tab's inner #button: uui-tab doesn't delegate focus", () => {
    const button = focusable();
    const tab = { ...focusable(), shadowRoot: { querySelector: (selector: string) => (selector === '#button' ? button : null) } };
    expect(focusTargetOf(tab)).toBe(button);
  });

  it('falls back to the tab itself', () => {
    const bare = focusable();
    expect(focusTargetOf(bare)).toBe(bare);
    const empty = { ...focusable(), shadowRoot: { querySelector: () => null } };
    expect(focusTargetOf(empty)).toBe(empty);
  });
});
