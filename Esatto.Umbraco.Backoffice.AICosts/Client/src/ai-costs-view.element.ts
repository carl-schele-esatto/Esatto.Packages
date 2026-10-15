// AI section → Analytics → "Costs" tab. Registered as a workspaceView on Umbraco.AI's
// UmbracoAI.Workspace.AnalyticsRoot (next to its built-in "Usage" view) in public/umbraco-package.json.
//
// Data: AICostsController — the report (daily usage + cost in the base currency, the month to date,
// the previous period, ECB exchange rates) and, on demand, cost by profile/user/provider. Currency
// conversion happens here so switching currency is instant.
//
// Charts are hand-rolled SVG. Colours follow the dataviz reference palette, validated against Umbraco's
// light (#fff) and dark surfaces (see ai-costs-view.styles.ts). Dark mode is detected from the computed
// --uui-color-surface (the backoffice theme isn't a media query), see #detectTheme().

import { customElement, html, nothing, state, svg } from '@umbraco-cms/backoffice/external/lit';
import { UmbLitElement } from '@umbraco-cms/backoffice/lit-element';
import { tryExecute } from '@umbraco-cms/backoffice/resources';
import {
  getBreakdown,
  getReport,
  normalizeRates,
  type AICostBreakdown,
  type AICostDay,
  type AICostReport,
  type Dimension,
} from './api';
import { budgetInDisplayCurrency } from './budget';
import { toDays, toWeeks, type Bucket } from './buckets';
import { change } from './compare';
import { downloadCsv, toCsv } from './csv';
import { Latest } from './latest';
import { parseDate } from './dates';
import { convert, displayCurrency, formatMoney, formatNumber } from './money';
import { readPrefs, writePrefs } from './prefs';
import { binOf, columnPath, niceTicks, quartiles } from './scales';
import { CELL, styles } from './ai-costs-view.styles';

const PERIODS = [30, 90, 180, 365];
const METRICS = [
  { key: 'cost', label: 'Cost' },
  { key: 'tokens', label: 'Tokens' },
  { key: 'requests', label: 'Requests' },
] as const;
type Metric = (typeof METRICS)[number]['key'];
const DIMENSIONS: { key: Dimension; label: string }[] = [
  { key: 'model', label: 'Model' },
  { key: 'profile', label: 'Profile' },
  { key: 'user', label: 'User' },
  { key: 'provider', label: 'Provider' },
];

// Heatmap geometry (px). 2px surface gap between cells.
const GAP = 2;
const HEAT_LEFT = 30;
const HEAT_TOP = 18;

// Column chart geometry.
const CHART_H = 220;
const AXIS_W = 64;
const AXIS_H = 26;
const MAX_BAR = 24;

/** One row of the "Cost by" card, whichever dimension it groups by. */
interface CostRow {
  name: string;
  requests: number;
  inputTokens: number;
  cachedInputTokens: number;
  outputTokens: number;
  totalTokens: number;
  cost: number | null;
}

interface Tooltip {
  card: HTMLElement;
  x: number;
  y: number;
  title: string;
  lines: { value: string; label: string }[];
}

@customElement('esatto-ai-costs-view')
export class EsattoAICostsViewElement extends UmbLitElement {
  @state() private _report?: AICostReport;
  @state() private _loading = false;
  @state() private _error: string | null = null;
  @state() private _days: number;
  @state() private _currency: string | null;
  @state() private _metric: Metric;
  @state() private _showTable = false;
  @state() private _tooltip: Tooltip | null = null;
  @state() private _dark = false;
  @state() private _chartWidth = 900;
  @state() private _dimension: Dimension = 'model';
  @state() private _breakdowns = new Map<string, AICostBreakdown>();
  @state() private _breakdownLoading = false;
  @state() private _breakdownError: string | null = null;

  #resizeObserver = new ResizeObserver((entries) => {
    const w = Math.floor(entries[0]?.contentRect.width ?? 0);
    if (w > 0 && w !== this._chartWidth) this._chartWidth = w;
  });
  #observedChart?: Element;
  // Only the newest report/breakdown request may update the tab (see Latest).
  #reports = new Latest();
  #breakdownRequests = new Latest();

  constructor() {
    super();
    const prefs = readPrefs();
    this._days = PERIODS.includes(prefs.days ?? 0) ? prefs.days! : 365;
    this._currency = prefs.currency ?? null;
    this._metric = METRICS.some((m) => m.key === prefs.metric) ? (prefs.metric as Metric) : 'cost';
  }

  override connectedCallback(): void {
    super.connectedCallback();
    void this.#load();
  }

  override disconnectedCallback(): void {
    this.#resizeObserver.disconnect();
    super.disconnectedCallback();
  }

  protected override firstUpdated(): void {
    this.#detectTheme();
  }

  protected override willUpdate(changed: Map<PropertyKey, unknown>): void {
    if (changed.has('_dark')) this.toggleAttribute('dark', this._dark);
  }

  protected override updated(): void {
    const chart = this.shadowRoot?.querySelector('.column-chart');
    if (chart && chart !== this.#observedChart) {
      this.#resizeObserver.disconnect();
      this.#resizeObserver.observe(chart);
      this.#observedChart = chart;
    }
  }

  async #load(): Promise<void> {
    const ticket = this.#reports.next();
    this._loading = true;
    this.#detectTheme();
    const { data, error } = await tryExecute(this, getReport(this._days), { disableNotifications: true });
    if (!this.#reports.isCurrent(ticket)) return;
    this._loading = false;
    if (error || !data) {
      this._error = 'Could not load AI cost data.';
      return;
    }
    this._error = null;
    this._report = normalizeRates(data as AICostReport);
    if (!this._currency || !this._report.currencies.includes(this._currency)) {
      this._currency = this._report.defaultCurrency;
    }
    if (this._dimension !== 'model') void this.#loadBreakdown(this._dimension);
  }

  async #loadBreakdown(dimension: Exclude<Dimension, 'model'>): Promise<void> {
    const key = `${dimension}:${this._days}`;
    if (this._breakdowns.has(key)) return;
    const ticket = this.#breakdownRequests.next();
    this._breakdownLoading = true;
    this._breakdownError = null;
    const { data, error } = await tryExecute(this, getBreakdown(dimension, this._days), { disableNotifications: true });
    if (!this.#breakdownRequests.isCurrent(ticket)) return;
    this._breakdownLoading = false;
    if (error || !data) {
      this._breakdownError = 'Could not load the breakdown.';
      return;
    }
    this._breakdowns = new Map(this._breakdowns).set(key, data as AICostBreakdown);
  }

  // ---- preferences & theme ------------------------------------------------------------

  #savePrefs(): void {
    writePrefs({ days: this._days, currency: this._currency ?? undefined, metric: this._metric });
  }

  #detectTheme(): void {
    const probe = this.shadowRoot?.querySelector('.theme-probe');
    if (!probe) return;
    const match = getComputedStyle(probe).color.match(/\d+(\.\d+)?/g);
    if (!match) return;
    const [r, g, b] = match.map(Number);
    this._dark = (0.2126 * r + 0.7152 * g + 0.0722 * b) / 255 < 0.45;
  }

  #setDays(days: number): void {
    if (days === this._days) return;
    this._days = days;
    this._breakdowns = new Map();
    this.#savePrefs();
    void this.#load();
  }

  #setCurrency(e: Event): void {
    this._currency = (e.target as HTMLSelectElement).value;
    this.#savePrefs();
  }

  #setMetric(metric: Metric): void {
    this._metric = metric;
    this.#savePrefs();
  }

  #setDimension(dimension: Dimension): void {
    this._dimension = dimension;
    if (dimension !== 'model') void this.#loadBreakdown(dimension);
  }

  // ---- money & numbers ----------------------------------------------------------------

  get #currency(): string {
    return this._report ? displayCurrency(this._report, this._currency) : 'USD';
  }

  #convert(baseAmount: number): number {
    return this._report ? convert(this._report, baseAmount, this.#currency) : baseAmount;
  }

  #money(baseAmount: number, opts: { compact?: boolean } = {}): string {
    return formatMoney(this.#convert(baseAmount), this.#currency, opts);
  }

  #metricValue(day: AICostDay): number {
    if (this._metric === 'tokens') return day.totalTokens;
    if (this._metric === 'requests') return day.requests;
    return day.cost;
  }

  #formatMetric(value: number): string {
    if (this._metric === 'cost') return this.#money(value);
    if (this._metric === 'tokens') return `${formatNumber(value)} tokens`;
    return `${formatNumber(value)} ${value === 1 ? 'request' : 'requests'}`;
  }

  #periodName(): string {
    return this._days === 365 ? '12 months' : `${this._days} days`;
  }

  // ---- tooltip ------------------------------------------------------------------------

  #showTip(e: Event, title: string, lines: Tooltip['lines']): void {
    const target = e.currentTarget as Element;
    const card = target.closest('.card') as HTMLElement | null;
    if (!card) return;
    const box = card.getBoundingClientRect();
    const rect = target.getBoundingClientRect();
    this._tooltip = { card, x: rect.left - box.left + rect.width / 2, y: rect.top - box.top, title, lines };
  }

  #hideTip = (): void => {
    this._tooltip = null;
  };

  #renderTip(card: string) {
    const t = this._tooltip;
    if (!t || t.card.dataset.card !== card) return nothing;
    return html`<div class="tooltip" style="left:${t.x}px;top:${t.y}px" role="status">
      <div class="tooltip-title">${t.title}</div>
      ${t.lines.map((l) => html`<div class="tooltip-line"><strong>${l.value}</strong><span>${l.label}</span></div>`)}
    </div>`;
  }

  // ---- render -------------------------------------------------------------------------

  override render() {
    return html`
      <span class="theme-probe" aria-hidden="true"></span>
      ${this.#renderFilters()}
      ${this._error ? html`<uui-box><p class="error">${this._error}</p></uui-box>` : nothing}
      ${this._report
        ? html`<div class="content ${this._loading ? 'is-loading' : ''}">
            ${this.#renderTiles()} ${this.#renderHeatmap()} ${this.#renderColumns()} ${this.#renderCostBy()}
            ${this.#renderFootnote()}
          </div>`
        : !this._error
          ? html`<uui-loader-bar></uui-loader-bar>`
          : nothing}
    `;
  }

  #renderFilters() {
    const currencies = this._report?.currencies ?? [];
    const ratesMissing = !!this._report && !this._report.rates;
    return html`<div class="filters">
      <uui-button-group aria-label="Period">
        ${PERIODS.map(
          (d) => html`<uui-button
            compact
            look=${d === this._days ? 'primary' : 'secondary'}
            aria-pressed=${d === this._days}
            @click=${() => this.#setDays(d)}
            >${d === 365 ? '12 months' : `${d} days`}</uui-button
          >`,
        )}
      </uui-button-group>
      <label class="currency">
        <span>Currency</span>
        <select @change=${this.#setCurrency} ?disabled=${ratesMissing} aria-label="Currency">
          ${currencies.map((c) => html`<option value=${c} ?selected=${c === this.#currency}>${c}</option>`)}
        </select>
      </label>
    </div>`;
  }

  #renderChange(current: number, previous: number | undefined) {
    if (!this._report?.previous || previous === undefined) return nothing;
    const c = change(current, previous);
    const label = `vs previous ${this.#periodName()}`;
    if (c.kind === 'new') return html`<div class="tile-change">New ${label}</div>`;
    if (c.kind === 'change') return html`<div class="tile-change">${c.percent > 0 ? '+' : ''}${c.percent} % ${label}</div>`;
    return nothing;
  }

  #renderMonthTile() {
    const r = this._report!;
    const budget = budgetInDisplayCurrency(r, this.#currency);
    return html`<uui-box class="tile">
      <div class="tile-label">This month</div>
      <div class="tile-value">${this.#money(r.month.cost)}</div>
      <div class="tile-note">Projected ${this.#money(r.month.projectedCost)} by month end</div>
      ${budget.kind === 'set'
        ? html`<div
              class="budget budget-${budget.level}"
              role="meter"
              aria-valuemin="0"
              aria-valuemax="100"
              aria-valuenow=${Math.round(budget.spentShare * 100)}
              aria-label="Share of the monthly budget used">
              <span class="budget-fill" style="width:${Math.min(100, budget.spentShare * 100)}%"></span>
            </div>
            <div class="tile-note">${formatNumber(budget.spentShare * 100)} % of ${formatMoney(budget.amount, this.#currency)}</div>`
        : budget.kind === 'unavailable'
          ? html`<div class="tile-note">${budget.reason}</div>`
          : nothing}
    </uui-box>`;
  }

  #renderTiles() {
    const r = this._report!;
    const t = r.totals;
    const p = r.previous?.totals;
    const input = r.days.reduce((s, d) => s + d.inputTokens, 0);
    const output = r.days.reduce((s, d) => s + d.outputTokens, 0);
    const perRequest = t.requests > 0 ? t.cost / t.requests : 0;
    const previousPerRequest = p && p.requests > 0 && t.requests > 0 ? p.cost / p.requests : undefined;
    return html`<div class="tiles">
      ${this.#renderMonthTile()}
      <uui-box class="tile tile-hero">
        <div class="tile-label">Estimated cost</div>
        <div class="tile-value hero">${this.#money(t.cost)}</div>
        ${t.unpricedTokens > 0
          ? html`<div class="tile-note">
              <uui-icon name="icon-alert"></uui-icon> ${formatNumber(t.unpricedTokens)} tokens from models without a price
            </div>`
          : html`<div class="tile-note">${this.#periodLabel()}</div>`}
        ${this.#renderChange(t.cost, p?.cost)}
      </uui-box>
      <uui-box class="tile">
        <div class="tile-label">Tokens</div>
        <div class="tile-value">${formatNumber(t.totalTokens, { compact: true })}</div>
        <div class="tile-note">${formatNumber(input, { compact: true })} in · ${formatNumber(output, { compact: true })} out</div>
        ${this.#renderChange(t.totalTokens, p?.totalTokens)}
      </uui-box>
      <uui-box class="tile">
        <div class="tile-label">Requests</div>
        <div class="tile-value">${formatNumber(t.requests)}</div>
        <div class="tile-note">${formatNumber(t.requests / r.days.length)} per day on average</div>
        ${this.#renderChange(t.requests, p?.requests)}
      </uui-box>
      <uui-box class="tile">
        <div class="tile-label">Cost per request</div>
        <div class="tile-value">${t.requests > 0 ? this.#money(perRequest) : '–'}</div>
        <div class="tile-note">Priced models only</div>
        ${this.#renderChange(perRequest, previousPerRequest)}
      </uui-box>
    </div>`;
  }

  #periodLabel(): string {
    const fmt = new Intl.DateTimeFormat(undefined, { day: 'numeric', month: 'short', year: 'numeric' });
    return `${fmt.format(parseDate(this._report!.from))} – ${fmt.format(parseDate(this._report!.to))}`;
  }

  // Calendar heatmap: weeks as columns (Monday first), one cell per day, 4 bins by quartile of the
  // non-zero values plus a neutral "no usage" cell.
  #renderHeatmap() {
    const days = this._report!.days;
    const values = days.map((d) => this.#metricValue(d));
    const thresholds = quartiles(values.filter((v) => v > 0));
    const offset = (parseDate(days[0].date).getUTCDay() + 6) % 7; // Monday = 0
    const weeks = Math.ceil((offset + days.length) / 7);
    const width = HEAT_LEFT + weeks * (CELL + GAP);
    const height = HEAT_TOP + 7 * (CELL + GAP);
    const monthFmt = new Intl.DateTimeFormat(undefined, { month: 'short' });
    const dayFmt = new Intl.DateTimeFormat(undefined, { weekday: 'short', day: 'numeric', month: 'short', year: 'numeric' });
    const weekdayFmt = new Intl.DateTimeFormat(undefined, { weekday: 'short' });

    const months: { col: number; label: string }[] = [];
    const cells = days.map((day, i) => {
      const pos = offset + i;
      const col = Math.floor(pos / 7);
      const row = pos % 7;
      const date = parseDate(day.date);
      if (date.getUTCDate() === 1 || i === 0) months.push({ col, label: monthFmt.format(date) });
      const value = values[i];
      const bin = value > 0 ? binOf(value, thresholds) : -1;
      return svg`<rect
        class="cell ${bin < 0 ? 'cell-empty' : `cell-${bin}`}"
        x=${HEAT_LEFT + col * (CELL + GAP)}
        y=${HEAT_TOP + row * (CELL + GAP)}
        width=${CELL}
        height=${CELL}
        rx="2"
        @pointerenter=${(e: Event) =>
          this.#showTip(e, dayFmt.format(date), [
            { value: this.#money(day.cost), label: 'cost' },
            { value: formatNumber(day.totalTokens), label: 'tokens' },
            { value: formatNumber(day.requests), label: day.requests === 1 ? 'request' : 'requests' },
          ])}
        @pointerleave=${this.#hideTip}
      ></rect>`;
    });

    // Drop a month label that would collide with the previous one (first partial week).
    const monthLabels = months.filter((m, i) => i === 0 || m.col - months[i - 1].col >= 3);

    return html`<uui-box class="card" data-card="heatmap" headline="Usage calendar">
      <div slot="header-actions">
        <uui-button-group aria-label="Heatmap metric">
          ${METRICS.map(
            (m) => html`<uui-button
              compact
              look=${m.key === this._metric ? 'primary' : 'secondary'}
              aria-pressed=${m.key === this._metric}
              @click=${() => this.#setMetric(m.key)}
              >${m.label}</uui-button
            >`,
          )}
        </uui-button-group>
      </div>
      <div class="heatmap-scroll">
        <svg
          class="heatmap"
          width=${width}
          height=${height}
          viewBox="0 0 ${width} ${height}"
          role="img"
          aria-label="Daily ${this._metric} over the selected period. The table under the cost chart lists every value.">
          ${monthLabels.map((m) => svg`<text class="axis-label" x=${HEAT_LEFT + m.col * (CELL + GAP)} y="11">${m.label}</text>`)}
          ${[0, 2, 4].map((row) => {
            // 2024-01-01 was a Monday — row 0.
            const label = weekdayFmt.format(new Date(Date.UTC(2024, 0, 1 + row)));
            return svg`<text class="axis-label" x="0" y=${HEAT_TOP + row * (CELL + GAP) + CELL - 2}>${label}</text>`;
          })}
          ${cells}
        </svg>
      </div>
      <div class="scale-legend" aria-hidden="true">
        <span class="swatch cell-empty"></span><span>No usage</span>
        <span class="scale-gap"></span>
        <span>Less</span>
        ${[0, 1, 2, 3].map((b) => html`<span class="swatch cell-${b}"></span>`)}
        <span>More</span>
        ${thresholds.length ? html`<span class="scale-range">up to ${this.#formatMetric(Math.max(...values))} a day</span>` : nothing}
      </div>
      ${this.#renderTip('heatmap')}
    </uui-box>`;
  }

  // Single-series column chart of cost. Daily for ≤ 92 days, weekly beyond (365 daily columns would be
  // sub-pixel with the 2px surface gap).
  #buckets(): { buckets: Bucket[]; weekly: boolean } {
    const weekly = this._report!.days.length > 92;
    return { buckets: weekly ? toWeeks(this._report!.days) : toDays(this._report!.days), weekly };
  }

  #renderColumns() {
    const { buckets, weekly } = this.#buckets();
    const max = Math.max(0, ...buckets.map((b) => b.cost));
    const ticks = niceTicks(this.#convert(max));
    const top = ticks[ticks.length - 1] || 1;
    const plotW = Math.max(200, this._chartWidth - AXIS_W);
    const slot = plotW / buckets.length;
    const barW = Math.max(1, Math.min(MAX_BAR, slot - GAP));
    const y = (v: number) => CHART_H - (this.#convert(v) / top) * CHART_H;
    const dateFmt = new Intl.DateTimeFormat(undefined, { day: 'numeric', month: 'short' });
    const dayLabelFmt = new Intl.DateTimeFormat(undefined, { weekday: 'short', day: 'numeric', month: 'short' });
    const labelEvery = Math.max(1, Math.ceil(buckets.length / Math.max(2, Math.floor(plotW / 90))));
    const allZero = max === 0;

    return html`<uui-box class="card" data-card="columns" headline=${weekly ? 'Weekly cost' : 'Daily cost'}>
      <div slot="header-actions" class="header-actions">
        <uui-button compact look="secondary" @click=${() => this.#downloadPeriodCsv(buckets, weekly)}>Download CSV</uui-button>
        <uui-button compact look="secondary" @click=${() => (this._showTable = !this._showTable)}>
          ${this._showTable ? 'Show chart' : 'Show as table'}
        </uui-button>
      </div>
      ${this._showTable
        ? this.#renderPeriodTable(buckets, weekly)
        : html`<div class="column-chart">
            ${allZero
              ? html`<p class="empty">
                  ${this._report!.totals.unpricedTokens > 0
                    ? 'No cost to show — the models used in this period have no configured price.'
                    : 'No AI usage in this period.'}
                </p>`
              : html`<svg
                  width=${AXIS_W + plotW}
                  height=${CHART_H + AXIS_H}
                  role="img"
                  aria-label="${weekly ? 'Weekly' : 'Daily'} estimated cost. Use “Show as table” for the values.">
                  ${ticks.map(
                    (t) => svg`
                      <line class="grid" x1=${AXIS_W} x2=${AXIS_W + plotW} y1=${CHART_H - (t / top) * CHART_H} y2=${CHART_H - (t / top) * CHART_H}></line>
                      <text class="axis-label tick" x=${AXIS_W - 8} y=${CHART_H - (t / top) * CHART_H + 4}>${new Intl.NumberFormat(undefined, {
                        style: 'currency',
                        currency: this.#currency,
                        notation: 'compact',
                        maximumFractionDigits: t < 10 ? 2 : 0,
                      }).format(t)}</text>`,
                  )}
                  ${buckets.map((b, i) => {
                    const x = AXIS_W + i * slot + (slot - barW) / 2;
                    const h = CHART_H - y(b.cost);
                    const label = weekly ? `Week of ${dateFmt.format(parseDate(b.start))}` : dayLabelFmt.format(parseDate(b.start));
                    return svg`
                      <rect class="hit" x=${AXIS_W + i * slot} y="0" width=${slot} height=${CHART_H}
                        @pointerenter=${(e: Event) =>
                          this.#showTip(e, label, [
                            { value: this.#money(b.cost), label: 'cost' },
                            { value: formatNumber(b.totalTokens), label: 'tokens' },
                            { value: formatNumber(b.requests), label: b.requests === 1 ? 'request' : 'requests' },
                          ])}
                        @pointerleave=${this.#hideTip}></rect>
                      ${h > 0 ? svg`<path class="bar" d=${columnPath(x, CHART_H, barW, h)}></path>` : nothing}
                      ${i % labelEvery === 0
                        ? svg`<text class="axis-label" x=${x + barW / 2} y=${CHART_H + 18} text-anchor="middle">${dateFmt.format(parseDate(b.start))}</text>`
                        : nothing}`;
                  })}
                  <line class="baseline" x1=${AXIS_W} x2=${AXIS_W + plotW} y1=${CHART_H} y2=${CHART_H}></line>
                </svg>`}
          </div>`}
      ${this.#renderTip('columns')}
    </uui-box>`;
  }

  #renderPeriodTable(buckets: Bucket[], weekly: boolean) {
    const fmt = new Intl.DateTimeFormat(undefined, { day: 'numeric', month: 'short', year: 'numeric' });
    const rows = buckets.filter((b) => b.requests > 0).reverse();
    if (!rows.length) return html`<p class="empty">No AI usage in this period.</p>`;
    return html`<div class="table-scroll">
      <uui-table aria-label="${weekly ? 'Weekly' : 'Daily'} usage and cost">
        <uui-table-head>
          <uui-table-head-cell>${weekly ? 'Week of' : 'Date'}</uui-table-head-cell>
          <uui-table-head-cell class="num">Requests</uui-table-head-cell>
          <uui-table-head-cell class="num">Input tokens</uui-table-head-cell>
          <uui-table-head-cell class="num">Output tokens</uui-table-head-cell>
          <uui-table-head-cell class="num">Cost</uui-table-head-cell>
        </uui-table-head>
        ${rows.map(
          (b) => html`<uui-table-row>
            <uui-table-cell>${fmt.format(parseDate(b.start))}</uui-table-cell>
            <uui-table-cell class="num">${formatNumber(b.requests)}</uui-table-cell>
            <uui-table-cell class="num">${formatNumber(b.inputTokens)}</uui-table-cell>
            <uui-table-cell class="num">${formatNumber(b.outputTokens)}</uui-table-cell>
            <uui-table-cell class="num">${this.#money(b.cost)}</uui-table-cell>
          </uui-table-row>`,
        )}
      </uui-table>
    </div>`;
  }

  // ---- cost by ------------------------------------------------------------------------

  /** The rows for the selected dimension, or undefined while a breakdown hasn't loaded. */
  #costRows(): CostRow[] | undefined {
    if (this._dimension === 'model') {
      return this._report!.models.map((m) => ({ ...m, name: m.modelId }));
    }
    return this._breakdowns.get(`${this._dimension}:${this._days}`)?.rows;
  }

  #renderCostBy() {
    const label = DIMENSIONS.find((d) => d.key === this._dimension)!.label;
    const rows = this.#costRows();
    const totalCost = this._report!.totals.cost;
    const noPriceTitle =
      this._dimension === 'model'
        ? 'Add this model to Esatto:AICosts:PricesPerMillionTokens in appsettings'
        : 'Some of this usage is from models without a configured price';
    return html`<uui-box class="card" headline="Cost by">
      <div slot="header-actions" class="header-actions">
        <uui-button-group aria-label="Group cost by">
          ${DIMENSIONS.map(
            (d) => html`<uui-button
              compact
              look=${d.key === this._dimension ? 'primary' : 'secondary'}
              aria-pressed=${d.key === this._dimension}
              @click=${() => this.#setDimension(d.key)}
              >${d.label}</uui-button
            >`,
          )}
        </uui-button-group>
        <uui-button compact look="secondary" ?disabled=${!rows?.length} @click=${() => this.#downloadCostByCsv(label, rows ?? [])}
          >Download CSV</uui-button
        >
      </div>
      ${this._breakdownError && this._dimension !== 'model'
        ? html`<p class="error">${this._breakdownError}</p>`
        : !rows
          ? html`<uui-loader-bar></uui-loader-bar>`
          : rows.length === 0
            ? html`<p class="empty">No AI usage in this period.</p>`
            : html`<div class="table-scroll">
                <uui-table aria-label="Cost by ${label.toLowerCase()}">
                  <uui-table-head>
                    <uui-table-head-cell>${label}</uui-table-head-cell>
                    <uui-table-head-cell class="num">Requests</uui-table-head-cell>
                    <uui-table-head-cell class="num">Input</uui-table-head-cell>
                    <uui-table-head-cell class="num">Cached input</uui-table-head-cell>
                    <uui-table-head-cell class="num">Output</uui-table-head-cell>
                    <uui-table-head-cell class="num">Cost</uui-table-head-cell>
                    <uui-table-head-cell class="num">Share</uui-table-head-cell>
                  </uui-table-head>
                  ${rows.map(
                    (row) => html`<uui-table-row>
                      <uui-table-cell>${row.name}</uui-table-cell>
                      <uui-table-cell class="num">${formatNumber(row.requests)}</uui-table-cell>
                      <uui-table-cell class="num">${formatNumber(row.inputTokens)}</uui-table-cell>
                      <uui-table-cell class="num">${formatNumber(row.cachedInputTokens)}</uui-table-cell>
                      <uui-table-cell class="num">${formatNumber(row.outputTokens)}</uui-table-cell>
                      <uui-table-cell class="num">
                        ${row.cost === null ? html`<uui-tag look="secondary" title=${noPriceTitle}>No price</uui-tag>` : this.#money(row.cost)}
                      </uui-table-cell>
                      <uui-table-cell class="num">
                        ${row.cost !== null && totalCost > 0
                          ? html`<span class="share"
                              ><span class="share-track"
                                ><span class="share-fill" style="width:${(row.cost / totalCost) * 100}%"></span></span
                              >${formatNumber((row.cost / totalCost) * 100)}%</span
                            >`
                          : '–'}
                      </uui-table-cell>
                    </uui-table-row>`,
                  )}
                </uui-table>
              </div>`}
      ${this._breakdownLoading && rows ? html`<uui-loader-bar></uui-loader-bar>` : nothing}
    </uui-box>`;
  }

  // ---- CSV ----------------------------------------------------------------------------

  #csvCost(baseAmount: number): number {
    return Number(this.#convert(baseAmount).toFixed(4));
  }

  #downloadPeriodCsv(buckets: Bucket[], weekly: boolean): void {
    const r = this._report!;
    const header = [weekly ? 'Week of' : 'Date', 'Requests', 'Input tokens', 'Cached input tokens', 'Output tokens', 'Total tokens', `Cost (${this.#currency})`, 'Unpriced tokens'];
    const rows = buckets.map((b) => [b.start, b.requests, b.inputTokens, b.cachedInputTokens, b.outputTokens, b.totalTokens, this.#csvCost(b.cost), b.unpricedTokens]);
    downloadCsv(`ai-costs-${r.from}-${r.to}.csv`, toCsv(header, rows));
  }

  #downloadCostByCsv(label: string, rows: CostRow[]): void {
    const r = this._report!;
    const header = [label, 'Requests', 'Input tokens', 'Cached input tokens', 'Output tokens', 'Total tokens', `Cost (${this.#currency})`];
    const data = rows.map((row) => [row.name, row.requests, row.inputTokens, row.cachedInputTokens, row.outputTokens, row.totalTokens, row.cost === null ? '' : this.#csvCost(row.cost)]);
    downloadCsv(`ai-costs-${r.from}-${r.to}-${this._dimension}.csv`, toCsv(header, data));
  }

  #renderFootnote() {
    const r = this._report!;
    const rateDate = r.rates
      ? new Intl.DateTimeFormat(undefined, { day: 'numeric', month: 'short', year: 'numeric' }).format(parseDate(r.rates.date))
      : null;
    return html`<p class="footnote">
      Estimates: tokens × list prices per model${r.pricesSource ? html` (${r.pricesSource})` : nothing}, in ${r.baseCurrency}.
      Cached input tokens are billed at the cache-read price. Excludes taxes, discounts and provider surcharges.
      ${r.rates
        ? html`Converted with ${r.rates.source} reference rates of ${rateDate}.`
        : html`<strong>Exchange rates are unavailable right now — amounts are shown in ${r.baseCurrency}.</strong>`}
      Times are UTC.
    </p>`;
  }

  static override styles = [styles];
}

export default EsattoAICostsViewElement;

declare global {
  interface HTMLElementTagNameMap {
    'esatto-ai-costs-view': EsattoAICostsViewElement;
  }
}
