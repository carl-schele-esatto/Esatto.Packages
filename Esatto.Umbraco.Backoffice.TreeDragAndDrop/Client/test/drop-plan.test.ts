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
