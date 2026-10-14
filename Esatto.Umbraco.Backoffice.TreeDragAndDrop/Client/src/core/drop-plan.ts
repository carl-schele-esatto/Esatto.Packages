// Pure: what a drop does. Both the pointer and keyboard paths resolve a
// (zone, target) to a DropPlan here; the host only executes plans.
//
// Trees WITHOUT sort (Dictionary) use planDrop: a drop only chooses a
// destination parent, because the tree lists children alphabetically.
// Trees WITH sort (Content, Media) use planSortedDrop.

import type { DropZone } from './drop-zone';

export interface DropContext {
  zone: DropZone;
  sourceParentUnique: string | null;
  targetUnique: string;
  targetParentUnique: string | null;
}

// Trees with sort also need the source and the target parent's current child
// order (as rendered; may include the source).
export interface SortedDropContext extends DropContext {
  sourceUnique: string;
  siblings: string[];
}

export type MovePlan = { kind: 'move'; parentUnique: string | null };
export type ReorderPlan = { kind: 'reorder'; parentUnique: string | null; order: string[] };
export type MoveSortPlan = { kind: 'move+sort'; parentUnique: string | null; order: string[] };
export type DropPlan = { kind: 'noop' } | MovePlan | ReorderPlan | MoveSortPlan;
export type ActionPlan = Exclude<DropPlan, { kind: 'noop' }>;

// Middle of a row → into the target. Top/bottom edge → next to the target,
// i.e. under the target's parent (null = root). Landing under the parent the
// source already has is a no-op.
export function planDrop(ctx: DropContext): DropPlan {
  const parentUnique = ctx.zone === 'into' ? ctx.targetUnique : ctx.targetParentUnique;
  return parentUnique === ctx.sourceParentUnique ? { kind: 'noop' } : { kind: 'move', parentUnique };
}

// Trees WITH sort (Content, Media). Middle of a row → into it (the server puts
// it last). Top/bottom edge → above/below the target, under the target's
// parent: a reorder when that is the current parent, otherwise move + sort.
export function planSortedDrop(ctx: SortedDropContext): DropPlan {
  const sameParent = ctx.targetParentUnique === ctx.sourceParentUnique;
  if (ctx.zone === 'into') {
    return ctx.targetUnique === ctx.sourceParentUnique
      ? { kind: 'noop' }
      : { kind: 'move', parentUnique: ctx.targetUnique };
  }

  const without = ctx.siblings.filter((u) => u !== ctx.sourceUnique);
  const targetIdx = without.indexOf(ctx.targetUnique);
  if (targetIdx === -1) {
    // Can't place it among siblings we can't see: still honour a parent change.
    return sameParent ? { kind: 'noop' } : { kind: 'move', parentUnique: ctx.targetParentUnique };
  }
  const insertAt = ctx.zone === 'before' ? targetIdx : targetIdx + 1;
  const order = [...without.slice(0, insertAt), ctx.sourceUnique, ...without.slice(insertAt)];

  if (!sameParent) return { kind: 'move+sort', parentUnique: ctx.targetParentUnique, order };
  const unchanged = order.length === ctx.siblings.length && order.every((u, i) => u === ctx.siblings[i]);
  return unchanged ? { kind: 'noop' } : { kind: 'reorder', parentUnique: ctx.targetParentUnique, order };
}

// A plan is worth committing only if it actually changes something. `null`
// means the target was illegal (blocked by the cycle guard or another tree).
export function canCommit(plan: DropPlan | null): plan is ActionPlan {
  return plan !== null && plan.kind !== 'noop';
}

// Screen-reader text for a plan. `parentName` is the destination parent's
// name (null when the destination is the root).
export function describePlan(plan: DropPlan, zone: DropZone, targetName: string, parentName: string | null): string {
  if (plan.kind === 'noop') return 'No change: already here';
  if (zone === 'into') return `Move into ${targetName}`;
  if (plan.kind === 'reorder' || plan.kind === 'move+sort') {
    return zone === 'before' ? `Move above ${targetName}` : `Move below ${targetName}`;
  }
  return parentName === null
    ? `Move to root (next to ${targetName})`
    : `Move next to ${targetName} (under ${parentName})`;
}
