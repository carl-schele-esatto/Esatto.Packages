// What the two editors (the month's note, the target queries) share: a labelled text box, its counter, a Save button that is never disabled, and the
// polite "Saved" line. Shared styles and one small template function, so the two read and behave the same.
//
// THE SAVE BUTTON IS NEVER DISABLED. A control that disables itself the moment it is used loses the keyboard's place (focus falls back to the
// page, and nothing gives it back when the work is done), so while a save is on its way the button stays focusable and says so with aria-disabled
// and aria-busy, shows it with a spinner (never by colour alone) and ignores the click. It is a real button, so that aria-disabled and aria-busy
// mean something (uui-button hands neither to its inner button). When the save is done the button is still there and still has the focus.

import { css, html, nothing } from '@umbraco-cms/backoffice/external/lit';
import type { SaveState } from '../analysis-model';
import { text, type Localizer } from '../localize';

/**
 * The row under a text box: the Save button and the polite "Saved" line. The line is always in the page (empty until a save succeeds): a live
 * region that is inserted together with its text is not announced, one that is filled is.
 */
export function renderSaveRow(host: Localizer, save: SaveState, onSave: () => void) {
  return html`
    <div class="save-row">
      <button type="button" class="save" aria-disabled=${save.saving ? 'true' : 'false'} aria-busy=${save.saving ? 'true' : 'false'} @click=${onSave}>
        ${save.saving ? html`<uui-loader-circle aria-hidden="true"></uui-loader-circle>` : nothing}${text(host, 'uiSaveLabel')}
      </button>
      <span class="saved" role="status" aria-live="polite">${save.saved ? html`<uui-icon name="icon-check" aria-hidden="true"></uui-icon>${text(host, 'uiSavedText')}` : nothing}</span>
    </div>
  `;
}

export const editorStyles = css`
  .editor { display: grid; gap: var(--uui-size-space-3); }
  .editor > label { font-size: 13px; font-weight: 600; line-height: 1.5; }
  .help { font-size: 0.85rem; line-height: 1.5; max-width: 70ch; }
  textarea {
    font: inherit; line-height: 1.5; box-sizing: border-box; width: 100%; min-height: 6rem; padding: var(--uui-size-space-3); resize: vertical;
    border: 1px solid var(--uui-color-border-standalone); border-radius: var(--seo-radius-sm);
    background: var(--uui-color-surface); color: var(--uui-color-text);
    transition: border-color var(--seo-ease), box-shadow var(--seo-ease);
  }
  textarea:hover { border-color: var(--uui-color-border-emphasis); }
  textarea:focus-visible {
    outline: 2px solid var(--uui-color-focus); outline-offset: 0; border-color: var(--uui-color-focus);
    box-shadow: 0 0 0 4px color-mix(in srgb, var(--uui-color-focus) 22%, transparent);
  }
  /* Too long: a heavier border, and the counter and the message say it in words with an icon. */
  textarea[aria-invalid='true'] { border-width: 2px; border-color: var(--seo-critical); }

  .counters { display: flex; flex-wrap: wrap; gap: var(--uui-size-space-2) var(--uui-size-space-4); align-items: center; font-size: 13px; }
  .counter { color: var(--seo-muted); font-variant-numeric: tabular-nums; }
  .counter.over { color: var(--uui-color-text); font-weight: 700; }
  .counter uui-icon, .error uui-icon, .warning uui-icon { margin-right: 4px; vertical-align: -2px; }
  .warning { font-size: 13px; font-weight: 600; margin: 0; }
  .error { font-size: 13px; font-weight: 700; margin: 0; padding: var(--uui-size-space-3) var(--uui-size-space-4); border-radius: var(--seo-radius-sm); border: 1px solid var(--seo-critical); box-shadow: inset 4px 0 0 var(--seo-critical); }

  .save-row { display: flex; flex-wrap: wrap; gap: var(--uui-size-space-4); align-items: center; }
  .save {
    display: inline-flex; gap: var(--uui-size-space-2); align-items: center; min-height: 40px; box-sizing: border-box;
    padding: 0 var(--uui-size-space-5); font: inherit; font-weight: 700; cursor: pointer;
    border: 1px solid var(--uui-color-default); border-radius: var(--seo-radius-sm);
    background: var(--uui-color-default); color: var(--uui-color-default-contrast);
    transition: background-color var(--seo-ease);
  }
  .save:hover { background: var(--uui-color-default-emphasis); border-color: var(--uui-color-default-emphasis); }
  .save[aria-disabled='true'] { cursor: progress; opacity: 0.8; }
  .save[aria-disabled='true']:hover { background: var(--uui-color-default); border-color: var(--uui-color-default); }
  .save uui-loader-circle { font-size: 16px; }
  .saved { display: inline-flex; align-items: center; gap: 4px; min-height: 24px; font-weight: 700; }
`;
