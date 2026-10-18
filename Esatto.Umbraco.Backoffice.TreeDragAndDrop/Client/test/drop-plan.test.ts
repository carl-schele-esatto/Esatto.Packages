import { describe, it, expect } from 'vitest';
import { planDrop, canCommit, describePlan } from '../src/core/drop-plan';

// Tree used throughout:
//   Frukt        (root)
//   ├ Grönsaker
//   │ └ Test
//   └ Päron
//   Äpple        (root)
describe('planDrop (tree without sort)', () => {
  it('middle of a row moves into that row', () => {
    expect(planDrop({ zone: 'into', sourceParentUnique: null, targetUnique: 'apple', targetParentUnique: null }))
      .toEqual({ kind: 'move', parentUnique: 'apple' });
  });

  it('top or bottom edge moves under the target row\'s parent', () => {
    const ctx = { sourceParentUnique: 'gronsaker', targetUnique: 'paron', targetParentUnique: 'frukt' };
    expect(planDrop({ ...ctx, zone: 'before' })).toEqual({ kind: 'move', parentUnique: 'frukt' });
    expect(planDrop({ ...ctx, zone: 'after' })).toEqual({ kind: 'move', parentUnique: 'frukt' });
  });

  it('edge of a top-level row moves to the root', () => {
    expect(planDrop({ zone: 'before', sourceParentUnique: 'gronsaker', targetUnique: 'apple', targetParentUnique: null }))
      .toEqual({ kind: 'move', parentUnique: null });
  });

  // Review Focus 3: dropping where the item already is does nothing.
  it('edge of a sibling is a no-op', () => {
    expect(planDrop({ zone: 'after', sourceParentUnique: 'frukt', targetUnique: 'paron', targetParentUnique: 'frukt' }))
      .toEqual({ kind: 'noop' });
  });

  it('edge of a top-level row is a no-op for a top-level source', () => {
    expect(planDrop({ zone: 'before', sourceParentUnique: null, targetUnique: 'apple', targetParentUnique: null }))
      .toEqual({ kind: 'noop' });
  });

  it('into the current parent is a no-op', () => {
    expect(planDrop({ zone: 'into', sourceParentUnique: 'gronsaker', targetUnique: 'gronsaker', targetParentUnique: 'frukt' }))
      .toEqual({ kind: 'noop' });
  });
});

// Review Focus 4: keyboard commit only fires for a real move.
describe('canCommit', () => {
  it('rejects a missing plan (blocked or foreign target)', () => {
    expect(canCommit(null)).toBe(false);
  });
  it('rejects a no-op', () => {
    expect(canCommit({ kind: 'noop' })).toBe(false);
  });
  it('accepts a move, including a move to the root', () => {
    expect(canCommit({ kind: 'move', parentUnique: 'frukt' })).toBe(true);
    expect(canCommit({ kind: 'move', parentUnique: null })).toBe(true);
  });
});

describe('describePlan', () => {
  it('into', () => {
    expect(describePlan({ kind: 'move', parentUnique: 'frukt' }, 'into', 'Frukt', 'Frukt')).toBe('Move into Frukt');
  });
  it('next to an item under a parent', () => {
    expect(describePlan({ kind: 'move', parentUnique: 'frukt' }, 'before', 'Päron', 'Frukt'))
      .toBe('Move next to Päron (under Frukt)');
  });
  it('to the root', () => {
    expect(describePlan({ kind: 'move', parentUnique: null }, 'after', 'Äpple', null))
      .toBe('Move to root (next to Äpple)');
  });
  it('no-op', () => {
    expect(describePlan({ kind: 'noop' }, 'before', 'Päron', null)).toBe('No change: already here');
  });
});

import { planSortedDrop } from '../src/core/drop-plan';

// Content tree used below:
//   Home        (root)
//   ├ About
//   ├ Blog
//   └ Contact
//   Settings    (root)
describe('planSortedDrop (tree with sort)', () => {
  const homeKids = ['about', 'blog', 'contact'];

  it('middle of a row moves into it', () => {
    expect(planSortedDrop({ zone: 'into', sourceUnique: 'contact', sourceParentUnique: 'home', targetUnique: 'settings', targetParentUnique: null, siblings: ['home', 'settings'] }))
      .toEqual({ kind: 'move', parentUnique: 'settings' });
  });

  it('into the current parent is a no-op', () => {
    expect(planSortedDrop({ zone: 'into', sourceUnique: 'about', sourceParentUnique: 'home', targetUnique: 'home', targetParentUnique: null, siblings: ['home', 'settings'] }))
      .toEqual({ kind: 'noop' });
  });

  it('same parent, above a sibling → reorder', () => {
    expect(planSortedDrop({ zone: 'before', sourceUnique: 'contact', sourceParentUnique: 'home', targetUnique: 'about', targetParentUnique: 'home', siblings: homeKids }))
      .toEqual({ kind: 'reorder', parentUnique: 'home', order: ['contact', 'about', 'blog'] });
  });

  it('same parent, below a sibling → reorder', () => {
    expect(planSortedDrop({ zone: 'after', sourceUnique: 'about', sourceParentUnique: 'home', targetUnique: 'contact', targetParentUnique: 'home', siblings: homeKids }))
      .toEqual({ kind: 'reorder', parentUnique: 'home', order: ['blog', 'contact', 'about'] });
  });

  // Review Focus 1: the legacy packages failed here ("Target sibling not found").
  it('reorders at the tree root', () => {
    expect(planSortedDrop({ zone: 'before', sourceUnique: 'settings', sourceParentUnique: null, targetUnique: 'home', targetParentUnique: null, siblings: ['home', 'settings'] }))
      .toEqual({ kind: 'reorder', parentUnique: null, order: ['settings', 'home'] });
  });

  // Review Focus 2: dropping where it already is does nothing.
  it('above its own next sibling is a no-op', () => {
    expect(planSortedDrop({ zone: 'before', sourceUnique: 'about', sourceParentUnique: 'home', targetUnique: 'blog', targetParentUnique: 'home', siblings: homeKids }))
      .toEqual({ kind: 'noop' });
  });

  it('below its own previous sibling is a no-op', () => {
    expect(planSortedDrop({ zone: 'after', sourceUnique: 'blog', sourceParentUnique: 'home', targetUnique: 'about', targetParentUnique: 'home', siblings: homeKids }))
      .toEqual({ kind: 'noop' });
  });

  it('cross-parent slot → move+sort with the source inserted', () => {
    expect(planSortedDrop({ zone: 'after', sourceUnique: 'settings', sourceParentUnique: null, targetUnique: 'about', targetParentUnique: 'home', siblings: homeKids }))
      .toEqual({ kind: 'move+sort', parentUnique: 'home', order: ['about', 'settings', 'blog', 'contact'] });
  });

  it('cross-parent slot to the root → move+sort under null', () => {
    expect(planSortedDrop({ zone: 'before', sourceUnique: 'blog', sourceParentUnique: 'home', targetUnique: 'settings', targetParentUnique: null, siblings: ['home', 'settings'] }))
      .toEqual({ kind: 'move+sort', parentUnique: null, order: ['home', 'blog', 'settings'] });
  });

  it('target missing from the rendered siblings → plain move (cross-parent) or no-op (same parent)', () => {
    expect(planSortedDrop({ zone: 'before', sourceUnique: 'x', sourceParentUnique: null, targetUnique: 'about', targetParentUnique: 'home', siblings: [] }))
      .toEqual({ kind: 'move', parentUnique: 'home' });
    expect(planSortedDrop({ zone: 'before', sourceUnique: 'blog', sourceParentUnique: 'home', targetUnique: 'about', targetParentUnique: 'home', siblings: [] }))
      .toEqual({ kind: 'noop' });
  });
});

describe('canCommit / describePlan for sorted plans', () => {
  it('commits reorder and move+sort', () => {
    expect(canCommit({ kind: 'reorder', parentUnique: null, order: ['a'] })).toBe(true);
    expect(canCommit({ kind: 'move+sort', parentUnique: 'p', order: ['a'] })).toBe(true);
  });

  it('describes above / below', () => {
    expect(describePlan({ kind: 'reorder', parentUnique: 'home', order: [] }, 'before', 'About', 'Home')).toBe('Move above About');
    expect(describePlan({ kind: 'move+sort', parentUnique: 'home', order: [] }, 'after', 'About', 'Home')).toBe('Move below About');
  });
});

import { placeInOrder } from '../src/core/drop-plan';

// Review (steps 2-3) Critical 1: Bellissima can render a middle window of a large
// parent ("Load previous"). The sort endpoint treats sortOrder as an absolute
// index, so the order sent must be the parent's FULL child order.
describe('placeInOrder (full server order)', () => {
  const full = ['a', 'b', 'c', 'd', 'e', 'f', 'g', 'h'];

  it('moves within a window without disturbing the rest', () => {
    // Sidebar shows only d, e, f; f is dropped above d.
    expect(placeInOrder(full, 'f', 'd', 'before')).toEqual(['a', 'b', 'c', 'f', 'd', 'e', 'g', 'h']);
  });

  it('places below the target', () => {
    expect(placeInOrder(full, 'b', 'g', 'after')).toEqual(['a', 'c', 'd', 'e', 'f', 'g', 'b', 'h']);
  });

  it('inserts a source that is not yet among the children (cross-parent)', () => {
    expect(placeInOrder(['a', 'b'], 'x', 'b', 'before')).toEqual(['a', 'x', 'b']);
  });

  it('returns null when the target is not a child of that parent', () => {
    expect(placeInOrder(full, 'a', 'zz', 'before')).toBeNull();
  });
});
