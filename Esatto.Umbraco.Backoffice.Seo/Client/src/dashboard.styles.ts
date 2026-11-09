// Shared styles for the dashboard and its tabs.
//
// Colour works in the light, dark and high-contrast backoffice themes alike. The dark theme only sets the custom
// property --uui-color-scheme, which CSS can't branch on, so nothing here depends on knowing the theme:
// - status colours are used as solid fills with their own text colour (--tone / --tone-on), so their contrast is
//   fixed whatever surrounds them;
// - tints are the status colour mixed into the theme's own surface, so they follow the theme;
// - text is always a theme colour, never a status colour.
// Status is never shown by colour alone: every badge also has an icon and a text label.

import { css } from '@umbraco-cms/backoffice/external/lit';

export const shared = css`
  :host {
    display: block;
    --seo-critical: #c9374c;
    --seo-critical-on: #fff;
    --seo-warning: #f59e0b;
    --seo-warning-on: #1d1d1f;
    --seo-notice: #2f6fde;
    --seo-notice-on: #fff;
    --seo-good: #0e7f41;
    --seo-good-on: #fff;
    --seo-neutral: color-mix(in srgb, var(--uui-color-text) 70%, var(--uui-color-surface));
    --seo-neutral-on: var(--uui-color-surface);

    --seo-muted: color-mix(in srgb, var(--uui-color-text) 68%, var(--uui-color-surface));
    --seo-line: var(--uui-color-border);
    --seo-wash: color-mix(in srgb, var(--uui-color-text) 4%, var(--uui-color-surface));
    --seo-radius: 12px;
    --seo-radius-sm: 8px;
    --seo-shadow: var(--uui-shadow-depth-1);
    --seo-shadow-raised: var(--uui-shadow-depth-3);
    --seo-ease: 160ms ease;
    /* One gap between all blocks on a tab (cards, rows of cards, the save button), so spacing reads as one rhythm. */
    --seo-gap: var(--uui-size-space-5);
  }

  /* Tones: set --tone/--tone-on once, every component below reads them. */
  .tone-critical { --tone: var(--seo-critical); --tone-on: var(--seo-critical-on); }
  .tone-warning { --tone: var(--seo-warning); --tone-on: var(--seo-warning-on); }
  .tone-notice { --tone: var(--seo-notice); --tone-on: var(--seo-notice-on); }
  .tone-good { --tone: var(--seo-good); --tone-on: var(--seo-good-on); }
  .tone-none { --tone: var(--seo-neutral); --tone-on: var(--seo-neutral-on); }

  .muted { color: var(--seo-muted); }
  .small { font-size: 13px; }
  .row { display: flex; flex-wrap: wrap; gap: var(--uui-size-space-4); align-items: center; }
  .stack > * + * { margin-top: var(--uui-size-space-4); }
  .page { display: grid; grid-template-columns: minmax(0, 1fr); gap: var(--seo-gap); }
  .error { color: var(--uui-color-danger-standalone); }
  .visually-hidden {
    position: absolute; width: 1px; height: 1px; margin: -1px; padding: 0; overflow: hidden;
    clip: rect(0 0 0 0); white-space: nowrap; border: 0;
  }
  h2, h3, h4 { margin: 0; }
  p { margin: 0; }
  code, pre { font-family: var(--uui-font-monospace, monospace); font-size: 0.9em; }
  code { padding: 1px 6px; border-radius: 6px; background: var(--seo-wash); border: 1px solid var(--seo-line); }
  pre { white-space: pre-wrap; word-break: break-word; margin: 0; }
  a { color: var(--uui-color-interactive); text-underline-offset: 2px; }
  a:hover { color: var(--uui-color-interactive-emphasis); }
  :focus-visible { outline: 2px solid var(--uui-color-focus); outline-offset: 2px; }
  .url { word-break: break-all; font-size: 13px; }

  /* Surfaces */
  .card {
    background: var(--uui-color-surface); color: var(--uui-color-text);
    border: 1px solid var(--seo-line); border-radius: var(--seo-radius); box-shadow: var(--seo-shadow);
    padding: var(--uui-size-space-5) var(--uui-size-space-6);
  }
  .card-head { display: flex; flex-wrap: wrap; gap: var(--uui-size-space-3); align-items: center; margin-bottom: var(--uui-size-space-4); }
  .card-head h3 { font-size: 16px; font-weight: 700; }
  .card-head .end { margin-left: auto; }
  .icon-badge {
    flex: none; display: inline-grid; place-items: center; width: 34px; height: 34px; border-radius: 10px; font-size: 18px;
    background: color-mix(in srgb, var(--tone, var(--seo-notice)) 14%, var(--uui-color-surface));
    color: color-mix(in srgb, var(--tone, var(--seo-notice)) 80%, var(--uui-color-text));
  }
  .icon-badge.solid { background: var(--tone); color: var(--tone-on); }
  .icon-badge.round { border-radius: 50%; }

  /* Badges */
  .tag {
    display: inline-flex; gap: 5px; align-items: center; padding: 2px 10px 2px 8px; border-radius: 999px;
    font-size: 12px; font-weight: 700; line-height: 18px; letter-spacing: 0.01em; white-space: nowrap;
    background: var(--tone, var(--seo-wash)); color: var(--tone-on, var(--uui-color-text));
  }
  .tag uui-icon { font-size: 13px; }
  .chip {
    display: inline-flex; gap: 6px; align-items: center; padding: 2px 10px; border-radius: 999px; font-size: 13px; line-height: 20px;
    white-space: nowrap; background: var(--seo-wash); border: 1px solid var(--seo-line); color: var(--uui-color-text);
  }
  .chip uui-icon { font-size: 14px; color: var(--seo-muted); }

  /* Alerts */
  .banner {
    display: flex; gap: var(--uui-size-space-4); align-items: flex-start; padding: var(--uui-size-space-4) var(--uui-size-space-5);
    border-radius: var(--seo-radius); color: var(--uui-color-text);
    background: color-mix(in srgb, var(--tone) 9%, var(--uui-color-surface));
    border: 1px solid color-mix(in srgb, var(--tone) 45%, var(--seo-line));
    box-shadow: inset 4px 0 0 var(--tone);
  }
  .banner > div { display: grid; gap: 4px; padding-top: 6px; }

  /* Form controls */
  .field { display: grid; gap: 6px; align-content: start; }
  .field > label { font-size: 13px; font-weight: 600; }
  select, input:not([type='checkbox']), textarea {
    font: inherit; min-height: 40px; padding: 0 var(--uui-size-space-3); box-sizing: border-box;
    border: 1px solid var(--uui-color-border-standalone); border-radius: var(--seo-radius-sm);
    background: var(--uui-color-surface); color: var(--uui-color-text);
    transition: border-color var(--seo-ease), box-shadow var(--seo-ease);
  }
  textarea { padding: var(--uui-size-space-2) var(--uui-size-space-3); }
  select:hover, input:not([type='checkbox']):hover, textarea:hover { border-color: var(--uui-color-border-emphasis); }
  select:focus-visible, input:focus-visible, textarea:focus-visible {
    outline: 2px solid var(--uui-color-focus); outline-offset: 0; border-color: var(--uui-color-focus);
    box-shadow: 0 0 0 4px color-mix(in srgb, var(--uui-color-focus) 22%, transparent);
  }
  input:disabled, select:disabled, textarea:disabled { opacity: 0.65; cursor: not-allowed; }

  /* Tables */
  .table-wrap { overflow-x: auto; border: 1px solid var(--seo-line); border-radius: var(--seo-radius-sm); }
  table { border-collapse: collapse; width: 100%; }
  th, td { text-align: left; vertical-align: top; padding: var(--uui-size-space-3) var(--uui-size-space-4); border-bottom: 1px solid var(--seo-line); }
  tbody tr:last-child > * { border-bottom: 0; }
  th {
    font-size: 12px; font-weight: 700; letter-spacing: 0.05em; text-transform: uppercase; color: var(--seo-muted);
    background: var(--seo-wash); white-space: nowrap;
  }
  tbody tr { transition: background-color var(--seo-ease); }
  tbody tr:hover { background: var(--seo-wash); }
  td.num, th.num { text-align: right; font-variant-numeric: tabular-nums; }

  /* Disclosures: a chevron instead of the browser's triangle. */
  summary { cursor: pointer; list-style: none; }
  summary::-webkit-details-marker { display: none; }
  .chevron { flex: none; color: var(--seo-muted); transition: transform var(--seo-ease); }
  details[open] > summary .chevron { transform: rotate(90deg); }

  /* Clickable cards lift on hover. */
  .lift { transition: transform var(--seo-ease), box-shadow var(--seo-ease), border-color var(--seo-ease); }
  .lift:hover { transform: translateY(-2px); box-shadow: var(--seo-shadow-raised); }

  @media (prefers-reduced-motion: reduce) {
    *, .lift, .chevron { transition: none !important; }
    .lift:hover { transform: none; }
  }
`;
