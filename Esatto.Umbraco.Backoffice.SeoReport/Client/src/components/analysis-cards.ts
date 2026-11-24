// The cards of the Analysis tab that only show something: the month in words, the biggest movement and the quick wins. What each says is decided in
// analysis-view.ts and movement-view.ts; these only draw it. (The two cards that edit something, the note and the target queries, are in the tab:
// they are wired to the model.) Each is a section named by its heading, and the headings come in order under the dashboard's own.

import { css, html, nothing } from '@umbraco-cms/backoffice/external/lit';
import type { OpportunityView, StatementView } from '../analysis-view';
import { text, type Localizer } from '../localize';
import type { MovementView } from '../movement-view';
import './data-table.element';

/** A card's head: an icon in a badge (decoration) and the heading that names the section. */
export function renderCardHead(icon: string, id: string, title: string) {
  return html`
    <div class="card-head">
      <span class="icon-badge" aria-hidden="true"><uui-icon name=${icon}></uui-icon></span>
      <h3 id=${id}>${title}</h3>
    </div>
  `;
}

/** What the figures say about the month, as sentences. Without any, it says why there are none. */
export function renderSummaryCard(host: Localizer, statements: readonly StatementView[]) {
  return html`
    <section class="card" aria-labelledby="summary-title">
      ${renderCardHead('icon-lightbulb-active', 'summary-title', text(host, 'uiSummaryTitle'))}
      ${statements.length > 0
        ? html`<ul class="says">${statements.map((statement) => html`<li>${statement.text}</li>`)}</ul>`
        : html`<p class="muted">${text(host, 'uiSummaryEmptyText')}</p>`}
    </section>
  `;
}

/** The pages that gained and lost the most: what they are sorted on, and a table for each list. */
export function renderMovementCard(host: Localizer, view: MovementView) {
  return html`
    <section class="card" aria-labelledby="movement-title">
      ${renderCardHead('icon-chart-curve', 'movement-title', text(host, 'uiMovementTitle'))}
      ${view.state === 'none'
        ? html`<p class="muted">${text(host, 'uiMovementNoneText')}</p>`
        : html`
            <p class="help muted">${text(host, 'uiMovementHelpText')}</p>
            <p class="help muted">${view.ranked}</p>
            ${view.state === 'empty'
              ? html`<p class="muted">${text(host, 'uiMovementEmptyText')}</p>`
              : view.lists.map(
                  (list) => html`
                    <h4>${list.title}</h4>
                    <esatto-seo-report-data-table .caption=${list.title} .columns=${list.columns} .rows=${list.rows}></esatto-seo-report-data-table>
                  `,
                )}
          `}
    </section>
  `;
}

/** The quick wins, cheapest first, each with its effort in words. Nothing at all when there are none: no empty card. */
export function renderOpportunitiesCard(host: Localizer, items: readonly OpportunityView[]) {
  if (items.length === 0) return nothing;

  return html`
    <section class="card" aria-labelledby="opportunities-title">
      ${renderCardHead('icon-wand', 'opportunities-title', text(host, 'uiOpportunitiesTitle'))}
      <p class="help muted">${text(host, 'uiOpportunitiesHelpText')}</p>
      <ul class="wins">
        ${items.map(
          (item) => html`
            <li class="win">
              <div class="win-head">
                <strong>${item.title}</strong>
                ${item.effort ? html`<span class="chip"><uui-icon name="icon-time" aria-hidden="true"></uui-icon>${item.effort.label}</span>` : nothing}
              </div>
              <p class="muted">${item.text}</p>
            </li>
          `,
        )}
      </ul>
    </section>
  `;
}

export const analysisCardStyles = css`
  h4 { font-size: 14px; font-weight: 700; margin: var(--uui-size-space-5) 0 var(--uui-size-space-3); }
  .help { margin-bottom: var(--uui-size-space-3); }
  .says { margin: 0; padding-left: 1.2em; display: grid; gap: var(--uui-size-space-3); }
  .says li { line-height: 1.6; max-width: 80ch; }
  .wins { list-style: none; margin: 0; padding: 0; display: grid; gap: var(--uui-size-space-3); }
  .win { padding: var(--uui-size-space-3) var(--uui-size-space-4); border: 1px solid var(--seo-line); border-radius: var(--seo-radius-sm); background: var(--seo-wash); }
  .win-head { display: flex; flex-wrap: wrap; gap: var(--uui-size-space-2) var(--uui-size-space-4); align-items: center; }
  .win p { margin-top: var(--uui-size-space-2); line-height: 1.5; max-width: 80ch; }
`;
