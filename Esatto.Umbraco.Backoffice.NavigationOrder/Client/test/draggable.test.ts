import { describe, expect, it } from 'vitest';
import { makeTabDraggable } from '../src/drag/draggable';

describe('makeTabDraggable', () => {
  it('drags the tab itself, not its inner link: Chromium drops a link drag that starts on slotted label text', () => {
    const button = { draggable: true };
    const tab = { draggable: false, shadowRoot: { querySelector: (selector: string) => (selector === '#button' ? button : null) } };
    makeTabDraggable(tab);
    expect(tab.draggable).toBe(true);
    expect(button.draggable).toBe(false);
  });

  it('marks a tab without an inner #button', () => {
    const bare = { draggable: false };
    makeTabDraggable(bare);
    expect(bare.draggable).toBe(true);
    const empty = { draggable: false, shadowRoot: { querySelector: () => null } };
    makeTabDraggable(empty);
    expect(empty.draggable).toBe(true);
  });
});
