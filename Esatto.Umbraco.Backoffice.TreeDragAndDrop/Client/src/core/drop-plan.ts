// Pure: what a drop does. Both the pointer and keyboard paths resolve a
// (zone, target) to a DropPlan here; the host only executes plans.
//
// 1.0.0 covers trees WITHOUT sort (Dictionary): a drop only chooses a
// destination parent, because the tree lists children alphabetically.
// Step 2 (Content) extends DropPlan with reorder / move+sort for trees
// whose adapter has `sort`.

import type { DropZone } from './drop-zone';

export interface DropContext {
  zone: DropZone;
  sourceParentUnique: string | null;
  targetUnique: string;
  targetParentUnique: string | null;
}

export type MovePlan = { kind: 'move'; parentUnique: string | null };
export type DropPlan = { kind: 'noop' } | MovePlan;

// Middle of a row → into the target. Top/bottom edge → next to the target,
// i.e. under the target's parent (null = root). Landing under the parent the
// source already has is a no-op.
export function planDrop(ctx: DropContext): DropPlan {
  const parentUnique = ctx.zone === 'into' ? ctx.targetUnique : ctx.targetParentUnique;
  return parentUnique === ctx.sourceParentUnique ? { kind: 'noop' } : { kind: 'move', parentUnique };
}

// A plan is worth committing only if it actually moves something. `null`
// means the target was illegal (blocked by the cycle guard or another tree).
export function canCommit(plan: DropPlan | null): plan is MovePlan {
  return plan?.kind === 'move';
}

// Screen-reader text for a plan. `parentName` is the destination parent's
// name (null when the destination is the root).
export function describePlan(plan: DropPlan, zone: DropZone, targetName: string, parentName: string | null): string {
  if (plan.kind === 'noop') return 'No change: already here';
  if (zone === 'into') return `Move into ${targetName}`;
  return parentName === null
    ? `Move to root (next to ${targetName})`
    : `Move next to ${targetName} (under ${parentName})`;
}
