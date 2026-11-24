// The notes under a chart or a table: annotations (facts about the data) and insights (what the curve means), as a list. Each carries an icon AND a
// word for its kind ("Warning", "Note", "Insight"), shown to the eye as well as read out, so a kind is never told by colour alone; the tinted
// border is an extra signal. What a note says is decided in notes-view.ts; this only draws it.

import { css, html, nothing } from '@umbraco-cms/backoffice/external/lit';
import type { NoteView } from '../notes-view';

/** The list of notes; nothing for none. */
export function renderNotes(notes: readonly NoteView[]) {
  if (notes.length === 0) return nothing;

  return html`
    <ul class="notes-list">
      ${notes.map(
        (note) => html`
          <li class="note ${note.severity}">
            <span class="note-kind"><uui-icon name=${note.icon} aria-hidden="true"></uui-icon>${note.label}</span>
            <div class="note-body">
              ${note.title ? html`<strong>${note.title}</strong> ` : nothing}${note.text}
            </div>
          </li>
        `,
      )}
    </ul>
  `;
}

export const notesStyles = css`
  .notes-list { list-style: none; margin: var(--uui-size-space-4) 0 0; padding: 0; display: grid; gap: var(--uui-size-space-3); }
  .note {
    --tone: var(--seo-line);
    display: flex; flex-wrap: wrap; gap: var(--uui-size-space-2) var(--uui-size-space-4); align-items: baseline;
    padding: var(--uui-size-space-4) var(--uui-size-space-5); border-radius: var(--seo-radius-sm); font-size: 0.9rem;
    color: var(--uui-color-text); background: var(--seo-wash);
    border: 1px solid color-mix(in srgb, var(--tone) 35%, var(--seo-line)); box-shadow: inset 4px 0 0 var(--tone);
  }
  .note.warning { --tone: var(--seo-warning); background: color-mix(in srgb, var(--seo-warning) 8%, var(--uui-color-surface)); }
  .note.insight { --tone: var(--seo-notice); background: color-mix(in srgb, var(--seo-notice) 7%, var(--uui-color-surface)); }
  /* The kind in words, beside its icon: the text carries it, the colour only repeats it. */
  .note-kind {
    display: inline-flex; gap: 5px; align-items: center; font-size: 0.75rem; font-weight: 700; letter-spacing: 0.04em; text-transform: uppercase;
    color: var(--seo-muted);
  }
  .note-body { flex: 1 1 24rem; min-width: 0; line-height: 1.5; }
`;
