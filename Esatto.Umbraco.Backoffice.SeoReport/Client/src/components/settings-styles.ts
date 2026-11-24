// What the Settings tab's elements share on top of editor-styles.ts (the labelled text box, the help line, the Save button that is never disabled):
// text inputs, the checkbox and radio rows, a secondary button, the "Set in configuration" note and the status lines. The look follows the SEO
// Health package's Settings tab and the tokens of dashboard.styles.ts.
//
// EVERY BUTTON STAYS FOCUSABLE. A button that is busy, or that has nothing to do right now, says so with aria-disabled (and aria-busy while it
// works) and ignores the click; it is never `disabled`, which would throw the keyboard's place back to the page (see editor-styles.ts).

import { css } from '@umbraco-cms/backoffice/external/lit';

export const settingsStyles = css`
  .fields { display: grid; gap: var(--uui-size-space-5); }
  .field { display: grid; gap: 6px; align-content: start; }
  .field > label, fieldset > legend { font-size: 13px; font-weight: 600; line-height: 1.5; }
  fieldset { border: 0; margin: 0; padding: 0; min-width: 0; display: grid; gap: var(--uui-size-space-2); }
  fieldset > legend { padding: 0; margin-bottom: var(--uui-size-space-2); }

  input[type='text'] {
    font: inherit; box-sizing: border-box; width: 100%; max-width: 36rem; min-height: 40px; padding: 0 var(--uui-size-space-3);
    border: 1px solid var(--uui-color-border-standalone); border-radius: var(--seo-radius-sm);
    background: var(--uui-color-surface); color: var(--uui-color-text);
    transition: border-color var(--seo-ease), box-shadow var(--seo-ease);
  }
  input[type='text']:hover { border-color: var(--uui-color-border-emphasis); }
  input[type='text']:focus-visible {
    outline: 2px solid var(--uui-color-focus); outline-offset: 0; border-color: var(--uui-color-focus);
    box-shadow: 0 0 0 4px color-mix(in srgb, var(--uui-color-focus) 22%, transparent);
  }
  /* Read-only (set in configuration, or a save on its way): the wash and a dashed border say it, with the words beside it. */
  input[readonly], textarea[readonly] { background: var(--seo-wash); border-style: dashed; }

  .check { display: grid; grid-template-columns: auto 1fr; column-gap: var(--uui-size-space-3); row-gap: 2px; align-items: center; min-height: 32px; }
  .check input { width: 20px; height: 20px; margin: 0; accent-color: var(--uui-color-selected); }
  .check > label { font-weight: 600; }
  .check .help, .check .lock { grid-column: 2; margin: 0; }
  .check input[aria-readonly='true'] { cursor: not-allowed; }
  .choices { display: grid; gap: var(--uui-size-space-2); }

  .lock { color: var(--seo-muted); font-size: 13px; display: inline-flex; gap: 4px; align-items: center; }
  .status-line { display: inline-flex; gap: 6px; align-items: center; min-height: 24px; font-size: 13px; }
  .status-line uui-icon { flex: none; }
  .preview { font-size: 13px; color: var(--seo-muted); overflow-wrap: anywhere; }

  .buttons { display: flex; flex-wrap: wrap; gap: var(--uui-size-space-3); align-items: center; }
  .secondary {
    display: inline-flex; gap: var(--uui-size-space-2); align-items: center; min-height: 40px; box-sizing: border-box;
    padding: 0 var(--uui-size-space-4); font: inherit; font-weight: 600; cursor: pointer;
    border: 1px solid var(--uui-color-border-standalone); border-radius: var(--seo-radius-sm);
    background: var(--uui-color-surface); color: var(--uui-color-text);
    transition: border-color var(--seo-ease), background-color var(--seo-ease);
  }
  .secondary:hover { border-color: var(--uui-color-border-emphasis); background: var(--seo-wash); }
  .secondary[aria-disabled='true'] { cursor: not-allowed; opacity: 0.7; }
  .secondary[aria-busy='true'] { cursor: progress; }
  .secondary uui-loader-circle, .save uui-loader-circle { font-size: 16px; }
  .danger { border-color: var(--seo-critical); box-shadow: inset 3px 0 0 var(--seo-critical); }
`;
