// Shared styles for the dashboard and its tabs. Colours come from UUI tokens so the light and dark
// backoffice themes both work; the status palette always appears with an icon and a text label.

import { css } from '@umbraco-cms/backoffice/external/lit';

export const shared = css`
  :host {
    display: block;
    --seo-good: #0ca30c;
    --seo-warning: #fab219;
    --seo-critical: #d03b3b;
    --seo-info: var(--uui-color-selected, #3544b1);
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
  p { margin: 0; }
  table { border-collapse: collapse; width: 100%; }
  th, td { text-align: left; vertical-align: top; padding: var(--uui-size-space-2) var(--uui-size-space-3); border-bottom: 1px solid var(--uui-color-divider); }
  th { font-weight: 600; color: var(--uui-color-text-alt); }
  td.num, th.num { text-align: right; font-variant-numeric: tabular-nums; }
  code, pre { font-family: var(--uui-font-monospace, monospace); font-size: 0.9em; }
  pre { white-space: pre-wrap; word-break: break-word; margin: 0; }
  a { color: var(--uui-color-interactive); }
  a:hover { color: var(--uui-color-interactive-emphasis); }
  :focus-visible { outline: 2px solid var(--uui-color-focus); outline-offset: 2px; }
  .tag {
    display: inline-flex; gap: 4px; align-items: center; padding: 0 var(--uui-size-space-2); border-radius: var(--uui-border-radius);
    border: 1px solid var(--uui-color-border); font-size: 0.85em; line-height: 1.6; white-space: nowrap;
  }
  .sev-critical uui-icon { color: var(--seo-critical); }
  .sev-warning uui-icon { color: var(--seo-warning); }
  .sev-notice uui-icon { color: var(--seo-info); }
  .banner { display: flex; gap: var(--uui-size-space-3); align-items: flex-start; padding: var(--uui-size-space-4); border-radius: var(--uui-border-radius);
    background: var(--uui-color-surface); border: 1px solid var(--uui-color-border); }
  .banner uui-icon { flex: none; margin-top: 2px; }
  .banner.critical { border-left: 6px solid var(--seo-critical); }
  .banner.critical uui-icon { color: var(--seo-critical); }
  .banner.warning { border-left: 6px solid var(--seo-warning); }
  .banner.info { border-left: 6px solid var(--seo-info); background: color-mix(in srgb, var(--seo-info) 6%, var(--uui-color-surface)); }
  .banner.info uui-icon { color: var(--seo-info); }
  details > summary { cursor: pointer; }
`;
