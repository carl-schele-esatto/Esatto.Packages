// The controls of the report month: which month, when it was fetched, and the button that fetches it again.
//
// "Refresh" is a person's click and nothing else: it asks Google for the selected month (about fifteen seconds) and replaces the archived
// one. Opening the dashboard, or picking another month here, never fetches: both only read the archive. Choosing a month tells the shell
// (`period-change`), the button too (`refresh`); the shell owns the requests and says here, with `refreshing` and `busy`, what is going on.
//
// NEITHER CONTROL IS EVER DISABLED. A control that disables itself the moment it is used loses the keyboard's place (focus falls back to the
// page, and nothing gives it back when the work is done), and on some platforms an arrow key on a closed list fires a change on each press,
// so a keyboard user could not step through the months. While something is going on they stay focusable and say so with aria-disabled and
// aria-busy, show it with a spinner (not by colour alone), and ignore input; the status line below announces it politely.

import { css, customElement, html, live, nothing, property } from '@umbraco-cms/backoffice/external/lit';
import { UmbLitElement } from '@umbraco-cms/backoffice/lit-element';
import { shared } from './dashboard.styles';
import { formatDateTime, formatPeriod, formatRelative } from './format';
import { culture, text } from './localize';
import { shouldAcceptPeriodChange } from './view-state';

@customElement('esatto-seo-report-period-bar')
export class EsattoSeoReportPeriodBarElement extends UmbLitElement {
  /** The archived months, newest first. With fewer than two there is nothing to choose, and the month is shown as a chip instead. */
  @property({ attribute: false }) periods: string[] = [];

  /** The selected month, "YYYY-MM". */
  @property() period = '';

  /** When the selected month's report was fetched (an ISO moment), or null. */
  @property({ attribute: false }) fetchedUtc: string | null = null;

  /** A month is being fetched from Google: the button waits and says so. */
  @property({ type: Boolean }) refreshing = false;

  /** The report of another month is being read: nothing can be changed meanwhile. */
  @property({ type: Boolean }) busy = false;

  get #working(): boolean {
    return this.busy || this.refreshing;
  }

  #pick = (event: Event) => {
    const select = event.target as HTMLSelectElement;

    // Not acted on while something is going on (or for the month that is already selected): the list shows the month the shell is on.
    if (!shouldAcceptPeriodChange(this.#working, this.period, select.value, this.periods)) {
      select.value = this.period;
      return;
    }

    this.dispatchEvent(new CustomEvent<string>('period-change', { detail: select.value, bubbles: true, composed: true }));
  };

  #refresh = () => {
    if (this.#working) return;
    this.dispatchEvent(new CustomEvent('refresh', { bubbles: true, composed: true }));
  };

  override render() {
    const c = culture(this);
    const picker = this.periods.length > 1;

    // Relative while it is recent ("3 hours ago"), the date and time when it is not; the exact moment is always in the title.
    const absolute = formatDateTime(this.fetchedUtc, c);
    const when = formatRelative(this.fetchedUtc, c) || absolute;

    return html`
      <div class="bar">
        ${picker
          ? html`<div class="field">
              <label for="month">${text(this, 'uiMonthPickerLabel')}</label>
              <select id="month" aria-disabled=${this.#working ? 'true' : 'false'} aria-busy=${this.#working ? 'true' : 'false'} @change=${this.#pick}>
                ${this.periods.map(
                  // live(): compared with what the list shows now, not with what was bound last, so it follows the shell after a month was refused.
                  (period) => html`<option value=${period} .selected=${live(period === this.period)}>${formatPeriod(period, c)}</option>`,
                )}
              </select>
            </div>`
          : nothing}
        <ul class="meta">
          ${!picker && this.period
            ? html`<li class="chip"><uui-icon name="icon-calendar" aria-hidden="true"></uui-icon>${text(this, 'uiReportPeriodText', { period: this.period })}</li>`
            : nothing}
          ${when
            ? html`<li class="chip" title=${absolute}>
                <uui-icon name="icon-time" aria-hidden="true"></uui-icon>${text(this, 'uiFetchedText', { when })}
              </li>`
            : nothing}
        </ul>
        <button
          type="button"
          class="refresh"
          aria-disabled=${this.#working ? 'true' : 'false'}
          aria-busy=${this.refreshing ? 'true' : 'false'}
          @click=${this.#refresh}
        >
          ${this.refreshing ? html`<uui-loader-circle aria-hidden="true"></uui-loader-circle>` : nothing}${text(this, 'uiRefreshLabel')}
        </button>
      </div>
      ${this.refreshing
        ? html`<p class="muted small" role="status" aria-live="polite">${text(this, 'uiRefreshingText', { period: this.period })}</p>`
        : nothing}
    `;
  }

  static override styles = [
    shared,
    css`
      .bar { display: flex; flex-wrap: wrap; gap: var(--uui-size-space-4); align-items: end; }
      .meta { list-style: none; margin: 0; padding: 0; display: flex; flex-wrap: wrap; gap: var(--uui-size-space-2); align-items: center; }
      select { min-width: 12rem; }
      select[aria-disabled='true'] { opacity: 0.65; cursor: progress; }

      /* A real button, so that aria-disabled and aria-busy mean something (uui-button hands neither to its inner button), in the look of
         the backoffice's outline button. The spinner is how busy is SHOWN: never colour alone. */
      .refresh {
        display: inline-flex; gap: var(--uui-size-space-2); align-items: center; min-height: 40px; box-sizing: border-box;
        padding: 0 var(--uui-size-space-5); font: inherit; font-weight: 700; cursor: pointer;
        border: 1px solid var(--uui-color-interactive); border-radius: var(--seo-radius-sm);
        background: transparent; color: var(--uui-color-interactive);
        transition: background-color var(--seo-ease), color var(--seo-ease);
      }
      .refresh:hover { background: var(--uui-color-surface-emphasis); color: var(--uui-color-interactive-emphasis); }
      .refresh[aria-disabled='true'] { cursor: progress; opacity: 0.8; }
      .refresh[aria-disabled='true']:hover { background: transparent; color: var(--uui-color-interactive); }
      .refresh uui-loader-circle { font-size: 16px; }
    `,
  ];
}

declare global {
  interface HTMLElementTagNameMap {
    'esatto-seo-report-period-bar': EsattoSeoReportPeriodBarElement;
  }
}
