// The daily traffic card at the top of the Traffic tab: visits per day for the last thirty days or a calendar month, live from GA4, and where one
// day's visits came from. The state machine is daily-model.ts, what is written is daily-view.ts; this draws them.
//
// THIS IS AN EXPLICIT READ FROM GA4, through the server's cached service, and it is not the archive: it happens when the Traffic tab is opened,
// when another month is chosen and when a day is chosen, and never otherwise (no polling, no refresh by itself). It never starts the backfill.
// A failure is shown inside the card with "Try again", and the monthly charts under it are not affected.
//
// A DAY CAN BE CHOSEN BY THE KEYBOARD. Clicking a point of the chart is the quick way, but a chart cannot be operated with the keyboard, so the
// same choice is a list of the days of the window under the figures.

import { css, customElement, html, live, nothing, state } from '@umbraco-cms/backoffice/external/lit';
import { UmbLitElement } from '@umbraco-cms/backoffice/lit-element';
import { getDaily, getDailySources } from '../api';
import type { PointSelectDetail } from '../components/chart-card.element';
import { shared } from '../dashboard.styles';
import { DailyModel } from '../daily-model';
import { dailyMonthOptions, dailyView, type DailyView } from '../daily-view';
import { formatDayLong } from '../format';
import { culture, text } from '../localize';
import { sourceColumns, sourceRows } from '../table-rows';
import type { DailyResponse } from '../types';
import { renderTrend, trendStyles } from '../components/trend';
import '../components/chart-card.element';
import '../components/data-table.element';
import '../problem.element';

@customElement('esatto-seo-report-daily-card')
export class EsattoSeoReportDailyCardElement extends UmbLitElement {
  // Not used for drawing: reading the model's state is what a render does. Declared so that a change of the model is a change of the element.
  @state() private _version = 0;

  readonly #model = new DailyModel({ getDaily, getDailySources }, () => (this._version += 1));

  // dailyView writes the figures from a response; a render that has not got a new response keeps the one it made (the chart is redrawn when the
  // block or its arguments change, and a new object every render would be both).
  #view: { daily: DailyResponse; view: DailyView } | null = null;

  override connectedCallback() {
    super.connectedCallback();
    void this.#model.connect();
  }

  override disconnectedCallback() {
    this.#model.disconnect();
    super.disconnectedCallback();
  }

  #viewOf(daily: DailyResponse): DailyView {
    if (this.#view?.daily !== daily) this.#view = { daily, view: dailyView(this, daily) };
    return this.#view.view;
  }

  // ---- Showing -------------------------------------------------------------------------------------------------------------

  override render() {
    const { daily, loading, problem } = this.#model.state;
    const view = daily ? this.#viewOf(daily) : null;

    return html`
      <section class="card" aria-labelledby="title">
        <header>
          <div class="card-head">
            <span class="icon-badge" aria-hidden="true"><uui-icon name="icon-calendar"></uui-icon></span>
            <h3 id="title">${text(this, 'uiDailyTitle')}</h3>
          </div>
          <div class="controls">${this.#monthPicker()} ${view ? this.#dayPicker(view) : nothing}</div>
          ${view ? this.#figures(view) : nothing}
        </header>

        ${loading ? html`<p class="muted" role="status">${text(this, 'uiDailyLoadingText')}</p>` : nothing}
        ${problem ? html`<esatto-seo-report-problem .error=${problem} @retry=${() => void this.#model.retry()}></esatto-seo-report-problem>` : nothing}
        ${view
          ? html`<esatto-seo-report-chart-card
              embedded
              selectable
              .block=${view.chart}
              .args=${view.textArgs}
              @point-select=${(event: CustomEvent<PointSelectDetail>) => this.#onPoint(event, daily!)}
            ></esatto-seo-report-chart-card>`
          : nothing}
        ${view?.peak ? html`<p class="peak muted">${view.peak}</p>` : nothing} ${this.#sources()}
      </section>
    `;
  }

  // The month picker: the rolling window and the last twelve months, as the server offers them. It stays on the month being read, and it is not
  // disabled while it reads (a control that disables itself loses the keyboard's place): a second choice is simply the newest one.
  #monthPicker() {
    const { months, rollingDays, loading } = this.#model.state;
    if (months.length === 0) return nothing;

    const options = dailyMonthOptions(this, months, rollingDays);
    const selected = this.#model.selected;

    return html`
      <div class="field">
        <label for="period">${text(this, 'uiDailyPeriodLabel')}</label>
        <select id="period" aria-busy=${loading ? 'true' : 'false'} @change=${(event: Event) => void this.#model.selectMonth((event.target as HTMLSelectElement).value)}>
          ${options.map((option) => html`<option value=${option.value} .selected=${live(option.value === selected)}>${option.label}</option>`)}
        </select>
      </div>
    `;
  }

  #dayPicker(view: DailyView) {
    if (view.days.length === 0) return nothing;
    const { day } = this.#model.state;

    return html`
      <div class="field">
        <label for="day">${text(this, 'uiDailyDayPickerLabel')}</label>
        <select id="day" @change=${(event: Event) => void this.#model.selectDay((event.target as HTMLSelectElement).value || null)}>
          <option value="" .selected=${live(!day)}>${text(this, 'uiDailyPickDayLabel')}</option>
          ${view.days.map((item) => html`<option value=${item.date} .selected=${live(item.date === day)}>${item.label}</option>`)}
        </select>
      </div>
    `;
  }

  // The visits of the window and the change against the one before it: an arrow, a word and the size, never colour alone. No change is shown
  // for a window with no days, and "no comparison" says so when the period before had none.
  #figures(view: DailyView) {
    if (!view.hasDays) return nothing;

    return html`
      <div class="figures">
        <span class="total">${view.total}</span>
        <span class="muted">${text(this, 'uiDailyVisitsLabel')}</span>
        ${view.change
          ? renderTrend({ pill: view.change, note: view.change.against })
          : view.noComparison
            ? html`<span class="muted">${text(this, 'uiDailyNoComparisonText')}</span>`
            : nothing}
      </div>
    `;
  }

  #onPoint(event: CustomEvent<PointSelectDetail>, daily: DailyResponse) {
    event.stopPropagation();
    const date = daily.chart.labels[event.detail.index];
    if (date) void this.#model.selectDay(date);
  }

  // Where the visits of the chosen day came from: in a polite live region, so the arrival of the table is announced.
  #sources() {
    const { day, sources, sourcesLoading, sourcesProblem } = this.#model.state;
    if (!day) return nothing;

    const written = formatDayLong(day, culture(this));

    return html`
      <div class="sources" aria-live="polite">
        ${sourcesLoading ? html`<p class="muted">${text(this, 'uiDailySourcesLoadingText', { day: written })}</p>` : nothing}
        ${sourcesProblem
          ? html`<esatto-seo-report-problem .error=${sourcesProblem} @retry=${() => void this.#model.retry()}></esatto-seo-report-problem>`
          : nothing}
        ${sources && sources.sources.length === 0 ? html`<p class="muted">${text(this, 'uiDailySourcesNoneText', { day: written })}</p>` : nothing}
        ${sources && sources.sources.length > 0
          ? html`
              <h4>${text(this, 'uiDailySourcesTitle', { day: written })}</h4>
              <esatto-seo-report-data-table
                .caption=${text(this, 'uiDailySourcesTitle', { day: written })}
                .columns=${sourceColumns(this)}
                .rows=${sourceRows(this, sources.sources)}
              ></esatto-seo-report-data-table>
            `
          : nothing}
      </div>
    `;
  }

  static override styles = [
    shared,
    trendStyles,
    css`
      header { display: grid; gap: var(--uui-size-space-3); margin-bottom: var(--uui-size-space-4); }
      .controls { display: flex; flex-wrap: wrap; gap: var(--uui-size-space-4); }
      .field { min-width: 14rem; }
      .figures { display: flex; flex-wrap: wrap; align-items: baseline; gap: var(--uui-size-space-2); }
      .figures .trend { margin-top: 0; }
      .total { font-size: 2rem; font-weight: 800; line-height: 1; letter-spacing: -0.02em; font-variant-numeric: tabular-nums; }
      .peak { margin-top: var(--uui-size-space-4); font-size: 0.85rem; }
      .sources { margin-top: var(--uui-size-space-5); display: grid; gap: var(--uui-size-space-3); }
      .sources h4 { font-size: 1rem; }
    `,
  ];
}

declare global {
  interface HTMLElementTagNameMap {
    'esatto-seo-report-daily-card': EsattoSeoReportDailyCardElement;
  }
}
