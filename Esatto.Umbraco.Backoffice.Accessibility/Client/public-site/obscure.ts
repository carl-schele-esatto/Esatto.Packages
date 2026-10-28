// Keeps the floating preferences button from hiding the element that has keyboard focus
// (WCAG 2.4.11 Focus Not Obscured). Pure, so it can be tested without a layout engine.

export interface Box { top: number; bottom: number; left: number; right: number }

/** Space left between a scrolled-up element and the button. */
const GAP = 8;

const overlaps = (a: Box, b: Box) => a.left < b.right && b.left < a.right && a.top < b.bottom && b.top < a.bottom;

/**
 * What to do for a focused element: nothing (null), scroll the page so the element sits above the button,
 * or — when the page can't scroll that far (its very end) — have the button step aside ('yield').
 * @param canScrollBy How far the page can still scroll down, in px.
 */
export function unobscure(target: Box, button: Box, canScrollBy: number): { scrollBy: number } | 'yield' | null {
  if (!overlaps(target, button)) return null;
  const needed = target.bottom - button.top + GAP;
  return needed <= canScrollBy ? { scrollBy: needed } : 'yield';
}
