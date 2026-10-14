// The drop indicator — a single fixed-position element reused across drags.
//
// 'outline'    boxes a row: the destination parent (or the target itself for
//              a drop "into" it).
// 'line-above' a 2px bar above a row: insert above it (sorted trees), or above
//              the first top-level row to mean "the root" (Dictionary, whose
//              tree hides its root row).
// 'line-below' a 2px bar below a row: insert below it (sorted trees).

import { getRowRect } from './drop-zone';

export type IndicatorShape = 'outline' | 'line-above' | 'line-below';

const ACCENT = 'var(--uui-color-selected, #3879ff)';
const TINT = 'rgba(56, 121, 255, 0.08)';

const indicator: HTMLDivElement = (() => {
  const el = document.createElement('div');
  el.id = 'esatto-tree-dnd-indicator';
  el.style.cssText = `
    position: fixed;
    pointer-events: none;
    z-index: 99999;
    display: none;
    box-sizing: border-box;
  `;
  document.body.appendChild(el);
  return el;
})();

export function renderIndicator(targetEl: Element, shape: IndicatorShape): void {
  const rect = getRowRect(targetEl);
  const common = `
    position: fixed;
    pointer-events: none;
    z-index: 99999;
    display: block;
    box-sizing: border-box;
    left: ${rect.left}px;
    width: ${rect.width}px;
  `;
  if (shape === 'outline') {
    indicator.style.cssText = `${common}
      top: ${rect.top}px;
      height: ${rect.height}px;
      border: 2px solid ${ACCENT};
      background: ${TINT};
    `;
    return;
  }
  indicator.style.cssText = `${common}
    top: ${(shape === 'line-above' ? rect.top : rect.bottom) - 1}px;
    height: 2px;
    background: ${ACCENT};
  `;
}

export function hideIndicator(): void {
  indicator.style.display = 'none';
}
