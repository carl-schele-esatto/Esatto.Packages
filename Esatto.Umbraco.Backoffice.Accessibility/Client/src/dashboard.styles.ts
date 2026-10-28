// Shared styles for the dashboard and its tabs. Colours come from UUI tokens so the light and dark
// backoffice themes both work; the only hard-coded colours are the status palette (good / warning /
// critical), which always appears with an icon and a text label, never on its own.

import { css } from '@umbraco-cms/backoffice/external/lit';

export const shared = css`
  :host {
    display: block;
    --a11y-good: #0ca30c;
    --a11y-warning: #fab219;
    --a11y-critical: #d03b3b;
  }
  .muted { color: var(--uui-color-text-alt); }
  .row { display: flex; flex-wrap: wrap; gap: var(--uui-size-space-4); align-items: center; }
  .stack > * + * { margin-top: var(--uui-size-space-4); }
  .error { color: var(--uui-color-danger-standalone); }
  .visually-hidden {
    position: absolute; width: 1px; height: 1px; margin: -1px; padding: 0; overflow: hidden;
    clip: rect(0 0 0 0); white-space: nowrap; border: 0;
  }
  h2, h3, h4 { margin: 0; }
  table { border-collapse: collapse; width: 100%; }
  th, td { text-align: left; padding: var(--uui-size-space-2) var(--uui-size-space-3); border-bottom: 1px solid var(--uui-color-divider); }
  th { font-weight: 600; color: var(--uui-color-text-alt); }
  td.num, th.num { text-align: right; font-variant-numeric: tabular-nums; }
  code, pre { font-family: var(--uui-font-monospace, monospace); font-size: 0.9em; }
  pre { white-space: pre-wrap; word-break: break-all; background: var(--uui-color-surface-alt); padding: var(--uui-size-space-3); border-radius: var(--uui-border-radius); margin: 0; }
  a { color: var(--uui-color-interactive); }
  a:hover { color: var(--uui-color-interactive-emphasis); }
  :focus-visible { outline: 2px solid var(--uui-color-focus); outline-offset: 2px; }
  .tag {
    display: inline-block; padding: 0 var(--uui-size-space-2); border-radius: var(--uui-border-radius);
    border: 1px solid var(--uui-color-border); font-size: 0.85em; line-height: 1.6;
  }
`;
