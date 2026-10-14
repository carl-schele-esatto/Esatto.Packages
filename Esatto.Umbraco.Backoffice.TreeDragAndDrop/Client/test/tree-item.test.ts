import { describe, it, expect, beforeEach, vi } from 'vitest';
import { registerAdapter, clearAdapters } from '../src/core/adapters';
import { isTreeItem, isRendered, expandRow } from '../src/core/tree-item';

// Duck-typed stand-ins for Bellissima elements: the helpers only touch these members.
function row(entityType: string, props?: Record<string, unknown>, extra: Record<string, unknown> = {}) {
  return {
    nodeType: 1,
    tagName: 'UMB-TREE-ITEM',
    getAttribute: (name: string) => (name === 'entitytype' ? entityType : null),
    props,
    ...extra,
  };
}

beforeEach(() => {
  clearAdapters();
  registerAdapter({ entityType: 'dictionary', rootEntityType: 'dictionary-root', innerTag: 'umb-default-tree-item', move: async () => {} });
});

// Review fix #2: pickers ("Move to…", "Insert dictionary item", umb-input-dictionary)
// render the same <umb-tree-item entitytype="dictionary"> rows with isMenu: false.
// Only the sidebar (menu) tree may be dragged or grabbed.
describe('isTreeItem', () => {
  it('accepts a sidebar row of a registered tree', () => {
    expect(isTreeItem(row('dictionary', { isMenu: true }))).toBe(true);
  });

  it('rejects a picker row (isMenu false)', () => {
    expect(isTreeItem(row('dictionary', { isMenu: false }))).toBe(false);
  });

  it('rejects a row whose props are not bound yet', () => {
    expect(isTreeItem(row('dictionary'))).toBe(false);
  });

  // Review Focus 4: recycle-bin rows render in the same sidebar but must not move.
  it('rejects a trashed row', () => {
    expect(isTreeItem(row('dictionary', { isMenu: true, item: { isTrashed: true } }))).toBe(false);
  });

  it('rejects a sidebar row of an unregistered tree', () => {
    expect(isTreeItem(row('document', { isMenu: true }))).toBe(false);
  });
});

// Review fix #5: children of a collapsed branch stay in the DOM, unslotted and invisible.
describe('isRendered', () => {
  it('is true when the row has layout boxes', () => {
    expect(isRendered({ getClientRects: () => [{}] } as unknown as Element)).toBe(true);
  });

  it('is false for an unslotted (collapsed) row', () => {
    expect(isRendered({ getClientRects: () => [] } as unknown as Element)).toBe(false);
  });
});

// Review fix #6: <umb-tree-item> has no toggleChildren and no shadow root; expansion
// goes through the inner kind element's tree-item context (api.showChildren()).
describe('expandRow', () => {
  it('calls showChildren on the inner kind element\'s api', () => {
    const showChildren = vi.fn();
    const querySelector = vi.fn(() => ({ api: { showChildren } }));
    expandRow(row('dictionary', { isMenu: true }, { querySelector }) as unknown as Element);
    expect(querySelector).toHaveBeenCalledWith(':scope > umb-default-tree-item');
    expect(showChildren).toHaveBeenCalledOnce();
  });

  it('does nothing when the inner element is not rendered yet', () => {
    const querySelector = vi.fn(() => null);
    expect(() => expandRow(row('dictionary', { isMenu: true }, { querySelector }) as unknown as Element)).not.toThrow();
  });
});
