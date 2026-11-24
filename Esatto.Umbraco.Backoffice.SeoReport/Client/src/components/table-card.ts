// A card around a data table: an icon, a heading (and what it covers), a line that explains the columns, the table, and notes under it. Used by the
// search tabs, so the brand and non-brand figures, the top queries and the top pages are framed the same way.

import { css, html, nothing } from '@umbraco-cms/backoffice/external/lit';
import type { NoteView } from '../notes-view';
import type { TableColumn, TableRow } from '../table-rows';
import { renderNotes } from './notes';
import './data-table.element';

export interface TableCardOptions {
  icon: string;
  title: string;
  subtitle?: string;
  /** What the columns mean, in a sentence. */
  help?: string;
  columns: TableColumn[];
  rows: TableRow[];
  notes?: readonly NoteView[];
}

export function renderTableCard({ icon, title, subtitle, help, columns, rows, notes }: TableCardOptions) {
  return html`
    <section class="card">
      <div class="card-head">
        <span class="icon-badge" aria-hidden="true"><uui-icon name=${icon}></uui-icon></span>
        <span class="card-titles">
          <h3>${title}</h3>
          ${subtitle ? html`<span class="muted">${subtitle}</span>` : nothing}
        </span>
      </div>
      ${help ? html`<p class="help muted">${help}</p>` : nothing}
      <esatto-seo-report-data-table .caption=${title} .columns=${columns} .rows=${rows}></esatto-seo-report-data-table>
      ${notes ? renderNotes(notes) : nothing}
    </section>
  `;
}

export const tableCardStyles = css`
  .card-titles { display: grid; gap: 2px; }
  .help { margin-bottom: var(--uui-size-space-4); max-width: 70ch; font-size: 0.85rem; line-height: 1.5; }
`;
