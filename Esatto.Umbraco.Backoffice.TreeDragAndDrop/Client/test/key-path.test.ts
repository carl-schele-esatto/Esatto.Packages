import { describe, it, expect, beforeEach } from 'vitest';
import { registerAdapter, clearAdapters } from '../src/core/adapters';
import { isGrabStartPath, isInsideTree } from '../src/core/key-path';

// Duck-typed composed-path nodes, innermost first (as event.composedPath() returns them).
const el = (tagName: string, extra: Record<string, unknown> = {}) => ({ nodeType: 1, tagName, id: '', ...extra });
const dictionaryRow = () => ({
  ...el('UMB-TREE-ITEM'),
  getAttribute: (name: string) => (name === 'entitytype' ? 'dictionary' : null),
  props: { isMenu: true },
});

beforeEach(() => {
  clearAdapters();
  registerAdapter({ entityType: 'dictionary', rootEntityType: 'dictionary-root', innerTag: 'umb-default-tree-item', move: async () => {} });
});

// Review fix #3: Space must only grab when the row itself has focus, never when it
// lands on a control rendered inside the row.
describe('isGrabStartPath', () => {
  it('grabs from the row label', () => {
    const r = dictionaryRow();
    const path = [el('BUTTON', { id: 'label-button' }), el('UUI-MENU-ITEM'), el('UMB-DEFAULT-TREE-ITEM'), r, el('BODY')];
    expect(isGrabStartPath(path, r)).toBe(true);
  });

  it('does not grab from the expand caret', () => {
    const r = dictionaryRow();
    expect(isGrabStartPath([el('BUTTON', { id: 'caret-button' }), el('UUI-MENU-ITEM'), r], r)).toBe(false);
  });

  it('does not grab from the entity actions ("…" button or its menu)', () => {
    const r = dictionaryRow();
    expect(isGrabStartPath([el('BUTTON'), el('UUI-BUTTON'), el('UMB-ENTITY-ACTIONS-BUNDLE'), el('UUI-MENU-ITEM'), r], r)).toBe(false);
    expect(isGrabStartPath([el('UUI-MENU-ITEM'), el('UMB-ENTITY-ACTION-LIST'), el('UMB-ENTITY-ACTIONS-DROPDOWN'), r], r)).toBe(false);
  });

  it('does not grab from a button such as "Load more"', () => {
    const r = dictionaryRow();
    expect(isGrabStartPath([el('BUTTON'), el('UUI-BUTTON'), el('UMB-DEFAULT-TREE-ITEM'), r], r)).toBe(false);
  });

  it('does not grab from an editable element', () => {
    const r = dictionaryRow();
    expect(isGrabStartPath([el('INPUT'), r], r)).toBe(false);
    expect(isGrabStartPath([el('DIV', { isContentEditable: true }), r], r)).toBe(false);
  });
});

// Review fix #3: while grabbed, keys outside the grabbed tree (a translation textarea,
// a modal) must not be swallowed.
describe('isInsideTree', () => {
  it('is true for a key pressed on a row of the grabbed tree', () => {
    expect(isInsideTree([el('BUTTON', { id: 'label-button' }), dictionaryRow(), el('BODY')], 'dictionary')).toBe(true);
  });

  it('is false for a key pressed in a textarea elsewhere', () => {
    expect(isInsideTree([el('TEXTAREA'), el('UMB-WORKSPACE'), el('BODY')], 'dictionary')).toBe(false);
  });

  it('is false for a row of a different tree', () => {
    registerAdapter({ entityType: 'media', rootEntityType: 'media-root', innerTag: 'umb-media-tree-item', move: async () => {} });
    const mediaRow = { ...dictionaryRow(), getAttribute: (n: string) => (n === 'entitytype' ? 'media' : null) };
    expect(isInsideTree([mediaRow, el('BODY')], 'dictionary')).toBe(false);
  });
});
