// Shared styles for the dashboard and its tabs. The same design as SEO Health (Esatto.Umbraco.Backoffice.Seo),
// under this package's own --a11y- prefix: packages in this repo don't share client code.
//
// Colour works in the light, dark and high-contrast backoffice themes alike. The dark theme only sets the custom
// property --uui-color-scheme, which CSS can't branch on, so nothing here depends on knowing the theme:
// - status colours are used as solid fills with their own text colour (--tone / --tone-on), so their contrast is
//   fixed whatever surrounds them;
// - tints are the status colour mixed into the theme's own surface, so they follow the theme;
// - text is always a theme colour, never a status colour.
// Status is never shown by colour alone: every badge also has an icon or a text label.

import { css } from '@umbraco-cms/backoffice/external/lit';

export const shared = css`
  :host {
    display: block;
    --a11y-critical: #c9374c;
    --a11y-critical-on: #fff;
    --a11y-warning: #f59e0b;
    --a11y-warning-on: #1d1d1f;
    --a11y-notice: #2f6fde;
    --a11y-notice-on: #fff;
    --a11y-good: #0e7f41;
    --a11y-good-on: #fff;
    --a11y-neutral: color-mix(in srgb, var(--uui-color-text) 70%, var(--uui-color-surface));
    --a11y-neutral-on: var(--uui-color-surface);

    --a11y-muted: color-mix(in srgb, var(--uui-color-text) 68%, var(--uui-color-surface));
    --a11y-line: var(--uui-color-border);
    --a11y-wash: color-mix(in srgb, var(--uui-color-text) 4%, var(--uui-color-surface));
    --a11y-radius: 12px;
    --a11y-radius-sm: 8px;
    --a11y-shadow: var(--uui-shadow-depth-1);
    --a11y-shadow-raised: var(--uui-shadow-depth-3);
    --a11y-ease: 160ms ease;
    /* One gap between all blocks on a tab (cards, rows of cards, the save button), so spacing reads as one rhythm. */
    --a11y-gap: var(--uui-size-space-5);
  }

  /* Tones: set --tone/--tone-on once, every component below reads them. */
  .tone-critical { --tone: var(--a11y-critical); --tone-on: var(--a11y-critical-on); }
  .tone-warning { --tone: var(--a11y-warning); --tone-on: var(--a11y-warning-on); }
  .tone-notice { --tone: var(--a11y-notice); --tone-on: var(--a11y-notice-on); }
  .tone-good { --tone: var(--a11y-good); --tone-on: var(--a11y-good-on); }
  .tone-none { --tone: var(--a11y-neutral); --tone-on: var(--a11y-neutral-on); }

  .muted { color: var(--a11y-muted); }
  .small { font-size: 13px; }
  .row { display: flex; flex-wrap: wrap; gap: var(--uui-size-space-4); align-items: center; }
  .stack > * + * { margin-top: var(--uui-size-space-4); }
  .page { display: grid; grid-template-columns: minmax(0, 1fr); gap: var(--a11y-gap); }
  .error { color: var(--uui-color-danger-standalone); }
  .visually-hidden {
    position: absolute; width: 1px; height: 1px; margin: -1px; padding: 0; overflow: hidden;
    clip: rect(0 0 0 0); white-space: nowrap; border: 0;
  }
  h2, h3, h4 { margin: 0; }
  p { margin: 0; }
  code, pre { font-family: var(--uui-font-monospace, monospace); font-size: 0.9em; }
  code { padding: 1px 6px; border-radius: 6px; background: var(--a11y-wash); border: 1px solid var(--a11y-line); word-break: break-word; }
  pre {
    white-space: pre-wrap; word-break: break-all; margin: 0; padding: var(--uui-size-space-3) var(--uui-size-space-4);
    background: var(--a11y-wash); border: 1px solid var(--a11y-line); border-radius: var(--a11y-radius-sm);
  }
  a { color: var(--uui-color-interactive); text-underline-offset: 2px; }
  a:hover { color: var(--uui-color-interactive-emphasis); }
  :focus-visible { outline: 2px solid var(--uui-color-focus); outline-offset: 2px; }
  .url { word-break: break-all; font-size: 13px; }

  /* Surfaces */
  .card {
    background: var(--uui-color-surface); color: var(--uui-color-text);
    border: 1px solid var(--a11y-line); border-radius: var(--a11y-radius); box-shadow: var(--a11y-shadow);
    padding: var(--uui-size-space-5) var(--uui-size-space-6);
  }
  .card-head { display: flex; flex-wrap: wrap; gap: var(--uui-size-space-3); align-items: center; margin-bottom: var(--uui-size-space-4); }
  .card-head h3 { font-size: 16px; font-weight: 700; }
  .card-head .end { margin-left: auto; }
  .icon-badge {
    flex: none; display: inline-grid; place-items: center; width: 34px; height: 34px; border-radius: 10px; font-size: 18px;
    background: color-mix(in srgb, var(--tone, var(--a11y-notice)) 14%, var(--uui-color-surface));
    color: color-mix(in srgb, var(--tone, var(--a11y-notice)) 80%, var(--uui-color-text));
  }
  .icon-badge.solid { background: var(--tone); color: var(--tone-on); }
  .icon-badge.round { border-radius: 50%; }
  .section-label { font-size: 12px; font-weight: 700; letter-spacing: 0.08em; text-transform: uppercase; color: var(--a11y-muted); margin-bottom: var(--uui-size-space-3); }

  /* Badges: .tag is solid in its tone (neutral wash without one); .chip is a quiet fact. */
  .tag {
    display: inline-flex; gap: 5px; align-items: center; padding: 2px 10px; border-radius: 999px;
    font-size: 12px; font-weight: 700; line-height: 18px; letter-spacing: 0.01em; white-space: nowrap;
    background: var(--a11y-wash); color: var(--uui-color-text); border: 1px solid var(--a11y-line);
  }
  .tag[class*='tone-'] { background: var(--tone); color: var(--tone-on); border-color: transparent; }
  .tag uui-icon { font-size: 13px; }
  .chip {
    display: inline-flex; gap: 6px; align-items: center; padding: 2px 10px; border-radius: 999px; font-size: 13px; line-height: 20px;
    white-space: nowrap; background: var(--a11y-wash); border: 1px solid var(--a11y-line); color: var(--uui-color-text);
  }
  .chip uui-icon { font-size: 14px; color: var(--a11y-muted); }

  /* Alerts */
  .banner {
    display: flex; gap: var(--uui-size-space-4); align-items: flex-start; padding: var(--uui-size-space-4) var(--uui-size-space-5);
    border-radius: var(--a11y-radius); color: var(--uui-color-text);
    background: color-mix(in srgb, var(--tone) 9%, var(--uui-color-surface));
    border: 1px solid color-mix(in srgb, var(--tone) 45%, var(--a11y-line));
    box-shadow: inset 4px 0 0 var(--tone);
  }
  .banner > div { display: grid; gap: 4px; padding-top: 6px; }
  .banner.compact { padding: var(--uui-size-space-3) var(--uui-size-space-4); gap: var(--uui-size-space-3); align-items: center; }
  .banner.compact .icon-badge { width: 26px; height: 26px; font-size: 14px; }
  .banner.compact > div { padding-top: 0; }

  /* Form controls */
  .field { display: grid; gap: 6px; align-content: start; }
  .field > label { font-size: 13px; font-weight: 600; }
  select, input:not([type='checkbox']):not([type='radio']), textarea {
    font: inherit; min-height: 40px; padding: 0 var(--uui-size-space-3); box-sizing: border-box;
    border: 1px solid var(--uui-color-border-standalone); border-radius: var(--a11y-radius-sm);
    background: var(--uui-color-surface); color: var(--uui-color-text);
    transition: border-color var(--a11y-ease), box-shadow var(--a11y-ease);
  }
  textarea { padding: var(--uui-size-space-2) var(--uui-size-space-3); }
  select:hover, input:not([type='checkbox']):not([type='radio']):hover, textarea:hover { border-color: var(--uui-color-border-emphasis); }
  select:focus-visible, input:focus-visible, textarea:focus-visible {
    outline: 2px solid var(--uui-color-focus); outline-offset: 0; border-color: var(--uui-color-focus);
    box-shadow: 0 0 0 4px color-mix(in srgb, var(--uui-color-focus) 22%, transparent);
  }
  input[type='checkbox'], input[type='radio'] { accent-color: var(--uui-color-selected); }
  input:disabled, select:disabled, textarea:disabled { opacity: 0.65; cursor: not-allowed; }

  /* Tables */
  .table-wrap { overflow-x: auto; border: 1px solid var(--a11y-line); border-radius: var(--a11y-radius-sm); }
  table { border-collapse: collapse; width: 100%; }
  th, td { text-align: left; vertical-align: top; padding: var(--uui-size-space-3) var(--uui-size-space-4); border-bottom: 1px solid var(--a11y-line); }
  tbody tr:last-child > * { border-bottom: 0; }
  thead th {
    font-size: 12px; font-weight: 700; letter-spacing: 0.05em; text-transform: uppercase; color: var(--a11y-muted);
    background: var(--a11y-wash); white-space: nowrap;
  }
  tbody tr { transition: background-color var(--a11y-ease); }
  tbody tr:hover { background: var(--a11y-wash); }
  td.num, th.num { text-align: right; font-variant-numeric: tabular-nums; }

  /* Disclosures: a chevron instead of the browser's triangle, and the hand pointer. */
  summary { cursor: pointer; list-style: none; }
  summary::-webkit-details-marker { display: none; }
  .chevron { flex: none; color: var(--a11y-muted); transition: transform var(--a11y-ease); }
  details[open] > summary .chevron { transform: rotate(90deg); }
  .disclosure > summary { display: inline-flex; gap: 6px; align-items: center; font-weight: 600; color: var(--uui-color-interactive); }
  .disclosure > summary .chevron { color: inherit; }
  .disclosure > summary:hover { color: var(--uui-color-interactive-emphasis); }

  /* Clickable cards lift on hover. */
  .lift { transition: transform var(--a11y-ease), box-shadow var(--a11y-ease), border-color var(--a11y-ease); }
  .lift:hover { transform: translateY(-2px); box-shadow: var(--a11y-shadow-raised); }

  /* Progress */
  .progress-bar { height: 8px; border-radius: 999px; background: var(--a11y-wash); border: 1px solid var(--a11y-line); overflow: hidden; }
  .progress-bar > span { display: block; height: 100%; border-radius: inherit; background: linear-gradient(90deg, #2f6fde, #6d4ce0); transition: width 400ms ease; }

  @media (prefers-reduced-motion: reduce) {
    *, .lift, .chevron, .progress-bar > span { transition: none !important; }
    .lift:hover { transform: none; }
  }
`;
