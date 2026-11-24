// Shared styles for the dashboard and its elements. Copied from the SEO Health package so that the
// two dashboards look alike; the custom property names (--seo-*) are kept on purpose. Copied, not imported: a package must not depend
// on another package's files.
//
// Colour works in the light, dark and high-contrast backoffice themes alike. The dark theme only sets the custom
// property --uui-color-scheme, which CSS can't branch on, so nothing here depends on knowing the theme:
// - status colours are used as solid fills with their own text colour (--tone / --tone-on), so their contrast is
//   fixed whatever surrounds them;
// - tints are the status colour mixed into the theme's own surface, so they follow the theme;
// - text is always a theme colour, never a status colour.
// Status is never shown by colour alone: every badge also has an icon or an arrow and a text label.

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
  .visually-hidden {
    position: absolute; width: 1px; height: 1px; margin: -1px; padding: 0; overflow: hidden;
    clip: rect(0 0 0 0); white-space: nowrap; border: 0;
  }
  h2, h3, h4 { margin: 0; }
  p { margin: 0; }
  code, pre { font-family: var(--uui-font-monospace, monospace); font-size: 0.9em; }
  code { padding: 1px 6px; border-radius: 6px; background: var(--seo-wash); border: 1px solid var(--seo-line); }
  a { color: var(--uui-color-interactive); text-underline-offset: 2px; }
  a:hover { color: var(--uui-color-interactive-emphasis); }
  :focus-visible { outline: 2px solid var(--uui-color-focus); outline-offset: 2px; }

  /* Surfaces */
  .card {
    background: var(--uui-color-surface); color: var(--uui-color-text);
    border: 1px solid var(--seo-line); border-radius: var(--seo-radius); box-shadow: var(--seo-shadow);
    padding: var(--uui-size-space-5) var(--uui-size-space-6);
  }
  .card-head { display: flex; flex-wrap: wrap; gap: var(--uui-size-space-3); align-items: center; margin-bottom: var(--uui-size-space-4); }
  .card-head h3 { font-size: 16px; font-weight: 700; }
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
  .banner .actions { display: flex; flex-wrap: wrap; gap: var(--uui-size-space-3); margin-top: var(--uui-size-space-2); }

  /* Form controls */
  .field { display: grid; gap: 6px; align-content: start; }
  .field > label { font-size: 13px; font-weight: 600; }
  select {
    font: inherit; min-height: 40px; padding: 0 var(--uui-size-space-3); box-sizing: border-box;
    border: 1px solid var(--uui-color-border-standalone); border-radius: var(--seo-radius-sm);
    background: var(--uui-color-surface); color: var(--uui-color-text);
    transition: border-color var(--seo-ease), box-shadow var(--seo-ease);
  }
  select:hover { border-color: var(--uui-color-border-emphasis); }
  select:focus-visible {
    outline: 2px solid var(--uui-color-focus); outline-offset: 0; border-color: var(--uui-color-focus);
    box-shadow: 0 0 0 4px color-mix(in srgb, var(--uui-color-focus) 22%, transparent);
  }
  select:disabled { opacity: 0.65; cursor: not-allowed; }

  @media (prefers-reduced-motion: reduce) {
    *, *::before, *::after { transition: none !important; animation: none !important; }
  }
`;
