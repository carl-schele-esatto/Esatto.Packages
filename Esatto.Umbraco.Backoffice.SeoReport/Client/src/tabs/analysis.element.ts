// The Analysis tab: what the archive says about the selected month and the months around it, and the two things a person writes here, the month's
// note and the target queries. The state machine is analysis-model.ts (what is read, what a late answer does, how the editors save), what is shown
// is analysis-view.ts, movement-view.ts and target-view.ts; this draws them.
//
// IT READS THE ARCHIVE AND NOTHING ELSE: `GET analysis` is archive-only on the server, and saving a note or the target queries never starts a fetch.
// The tab is only in the page while it is the active one, so it reads when it is opened and again when the shell shows another month (the shell
// passes `.period`); an answer for a month that is no longer shown is dropped by the model.
//
//   .period  the selected month, "YYYY-MM"
//   .note    the note of that month as the shell holds it (it comes with the report; the shell keeps it in step with a save, see `note-saved`)
//   .revision  counts the changes of the archive the shell has seen (a month refreshed from Google, a backfill that ended): the month shown is read
//            again when it changes, and the months already read are forgotten
//   note-saved  (bubbles) a note was saved: detail { period, note }, what the server kept. The shell updates the note it holds.
//
// WHAT IS SHOWN. `{ hasAnalysis: false }`: a short text and no cards (there is no month to write a note on, and nothing to edit the targets against).
// Otherwise: the month in words, the note, the target queries (the editor, and the table of them against the month once there are some), their
// positions month by month, the biggest movement and the quick wins. The quick wins are withheld by the server from a user outside the findings
// groups: their card is then not drawn at all. While the target queries are being saved and read again the cards stay where they are, so that the
// keyboard's place and the "Saved" announcement are not lost, and what depends on the terms says that it is being updated.

import { css, customElement, html, nothing, property, state, type PropertyValues } from '@umbraco-cms/backoffice/external/lit';
import { UmbLitElement } from '@umbraco-cms/backoffice/lit-element';
import { getAnalysis, putNote, putTargets } from '../api';
import { AnalysisModel, NOTE_SAVED_EVENT, type AnalysisModelState, type NoteSavedDetail } from '../analysis-model';
import { analysisState, analysisView, isAnalysis, type AnalysisView } from '../analysis-view';
import { analysisCardStyles, renderCardHead, renderMovementCard, renderOpportunitiesCard, renderSummaryCard } from '../components/analysis-cards';
import { renderTableCard, tableCardStyles } from '../components/table-card';
import { shared } from '../dashboard.styles';
import { text } from '../localize';
import '../components/data-table.element';
import '../components/note-editor.element';
import '../components/terms-editor.element';
import '../problem.element';

@customElement('esatto-seo-report-analysis')
export class EsattoSeoReportAnalysisElement extends UmbLitElement {
  /** The selected month, "YYYY-MM". */
  @property() period = '';

  /** The note of the selected month as the shell holds it, or null. */
  @property({ attribute: false }) note: string | null = null;

  /** Counts the changes of the archive the shell has seen (a refresh of the month, the end of a backfill): the analysis is read again when it changes. */
  @property({ type: Number }) revision = 0;

  // Not used for drawing: reading the model's state is what a render does. Declared so that a change of the model is a change of the element.
  @state() private _version = 0;

  readonly #model = new AnalysisModel({ getAnalysis, putNote, putTargets }, () => (this._version += 1), {
    noteSaved: (period, note) =>
      this.dispatchEvent(new CustomEvent<NoteSavedDetail>(NOTE_SAVED_EVENT, { detail: { period, note }, bubbles: true, composed: true })),
  });

  override connectedCallback() {
    super.connectedCallback();
    this.#model.connect();
  }

  override disconnectedCallback() {
    // An answer that arrives later must not touch an element that has left.
    this.#model.disconnect();
    super.disconnectedCallback();
  }

  override willUpdate(changed: PropertyValues) {
    // A new month is read (or taken from the months already read). The same month after the archive changed is read again: what was held for it,
    // and for every other month, was computed from the archive as it was. The first update has no earlier revision, and reads anyway.
    if (changed.has('period')) void this.#model.show(this.period);
    else if (changed.has('revision') && changed.get('revision') !== undefined) void this.#model.invalidate();
  }

  // ---- Showing ------------------------------------------------------------------------------------------------------------------

  override render() {
    const model = this.#model.state;

    switch (analysisState(model)) {
      case 'idle':
        return nothing;
      case 'loading':
        return html`
          <section class="card">
            <p class="muted loading" role="status"><uui-loader-circle aria-hidden="true"></uui-loader-circle>${text(this, 'uiAnalysisLoadingText')}</p>
          </section>
        `;
      case 'failed':
        return this.#problem(model);
      case 'none':
        return html`<section class="card"><p class="muted">${text(this, 'uiAnalysisNoneText')}</p></section>`;
      default:
        return isAnalysis(model.analysis) ? this.#ready(model, analysisView(this, model.analysis)) : nothing;
    }
  }

  // A failed read, shown where it happened, with Try again.
  #problem(model: Readonly<AnalysisModelState>) {
    return model.problem ? html`<esatto-seo-report-problem .error=${model.problem} @retry=${() => void this.#model.retry()}></esatto-seo-report-problem>` : nothing;
  }

  #ready(model: Readonly<AnalysisModelState>, view: AnalysisView) {
    // The terms changed and the analysis is being read again (or that failed): what depends on the terms is out of date and is not drawn.
    const stale = model.targetsStale;

    return html`
      <div class="page">
        ${this.#problem(model)} ${renderSummaryCard(this, view.statements)} ${this.#noteCard(model)} ${this.#targetsCard(model, view, stale)}
        ${view.history && !stale
          ? renderTableCard({
              icon: 'icon-target',
              title: text(this, 'uiHistoryTitle'),
              help: text(this, 'uiHistoryHelpText'),
              columns: view.history.columns,
              rows: view.history.rows,
            })
          : nothing}
        ${renderMovementCard(this, view.movement)} ${renderOpportunitiesCard(this, view.opportunities)}
      </div>
    `;
  }

  // The note of the month. The month of the click goes with the save: the month can change while it is on its way.
  #noteCard(model: Readonly<AnalysisModelState>) {
    return html`
      <section class="card" aria-labelledby="note-title">
        ${renderCardHead('icon-notepad', 'note-title', text(this, 'uiMonthNoteTitle'))}
        <esatto-seo-report-note-editor
          .note=${this.note}
          .period=${this.period}
          .saveState=${model.note}
          @save=${(event: CustomEvent<{ text: string }>) => void this.#model.saveNote(this.period, event.detail.text)}
          @edit=${() => this.#model.noteEdited()}
          @retry=${() => void this.#model.retryNote()}
        ></esatto-seo-report-note-editor>
      </section>
    `;
  }

  // The target queries: the editor, then the table of them against the month, or how to start when there are none.
  #targetsCard(model: Readonly<AnalysisModelState>, view: AnalysisView, stale: boolean) {
    const title = text(this, 'uiTargetsTitle');

    return html`
      <section class="card" aria-labelledby="targets-title">
        ${renderCardHead('icon-target', 'targets-title', title)}
        <p class="help muted">${text(this, 'uiTargetsHelpText', { label: text(this, 'targetMissingLabel'), rowsFetched: !stale && view.targets.rowsFetched > 0 ? view.targets.rowsFetched : null })}</p>
        <esatto-seo-report-terms-editor
          .terms=${this.#model.terms}
          .saveState=${model.targets}
          @save=${(event: CustomEvent<{ text: string }>) => void this.#model.saveTargets(event.detail.text)}
          @edit=${() => this.#model.termsEdited()}
          @retry=${() => void this.#model.retryTargets()}
        ></esatto-seo-report-terms-editor>
        <div class="targets-table">${this.#targetsTable(model, view, stale, title)}</div>
      </section>
    `;
  }

  #targetsTable(model: Readonly<AnalysisModelState>, view: AnalysisView, stale: boolean, title: string) {
    if (stale) return model.loading ? html`<p class="muted" role="status">${text(this, 'uiAnalysisUpdatingText')}</p>` : nothing;
    if (view.targets.state === 'noTerms') return html`<p class="muted">${text(this, 'uiTargetsEmptyText')}</p>`;

    return html`<esatto-seo-report-data-table .caption=${title} .columns=${view.targets.columns} .rows=${view.targets.rows}></esatto-seo-report-data-table>`;
  }

  static override styles = [
    shared,
    tableCardStyles,
    analysisCardStyles,
    css`
      .loading { display: flex; align-items: center; gap: var(--uui-size-space-3); }
      .targets-table { margin-top: var(--uui-size-space-5); }
    `,
  ];
}

declare global {
  interface HTMLElementTagNameMap {
    'esatto-seo-report-analysis': EsattoSeoReportAnalysisElement;
  }
}
