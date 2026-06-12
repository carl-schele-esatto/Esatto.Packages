import {
  LitElement,
  css,
  html,
  customElement,
  property,
  state,
} from "@umbraco-cms/backoffice/external/lit";
import { UmbElementMixin } from "@umbraco-cms/backoffice/element-api";
import { buildMonthGrid, isDayDisabled, toDayKey } from "./calendar.logic.js";

const WEEKDAYS = ["Mo", "Tu", "We", "Th", "Fr", "Sa", "Su"];
const MONTHS = [
  "January", "February", "March", "April", "May", "June",
  "July", "August", "September", "October", "November", "December",
];

/**
 * An inline month calendar for a single date.
 * Fires a bubbling `change` event; read the selected day key from `.value`.
 */
@customElement("esatto-date-range-calendar")
export class BackofficeInlineCalendarElement extends UmbElementMixin(LitElement) {
  /** Selected day key (YYYY-MM-DD) or null. */
  @property({ type: String })
  public value: string | null = null;

  /** Earliest selectable date (ISO 8601) or null. */
  @property({ type: String })
  public min: string | null = null;

  /** Latest selectable date (ISO 8601) or null. */
  @property({ type: String })
  public max: string | null = null;

  /** When true, no day can be selected and the grid is dimmed. */
  @property({ type: Boolean })
  public disabled = false;

  @state()
  private _viewYear = new Date().getFullYear();

  @state()
  private _viewMonth = new Date().getMonth();

  override willUpdate(changed: Map<string, unknown>) {
    // Re-anchor the visible month only when the inputs that decide it change,
    // so the user's manual prev/next navigation is preserved between changes.
    if (changed.has("value") || changed.has("min")) {
      // A selected value wins; otherwise anchor to the lower bound (e.g. the
      // chosen start date for the end calendar); otherwise the current month.
      const anchor = this.value
        ? new Date(this.value)
        : this.min
          ? new Date(this.min)
          : new Date();
      this._viewYear = anchor.getFullYear();
      this._viewMonth = anchor.getMonth();
    }
  }

  #goPrev() {
    if (this._viewMonth === 0) {
      this._viewMonth = 11;
      this._viewYear -= 1;
    } else {
      this._viewMonth -= 1;
    }
  }

  #goNext() {
    if (this._viewMonth === 11) {
      this._viewMonth = 0;
      this._viewYear += 1;
    } else {
      this._viewMonth += 1;
    }
  }

  #selectDay(key: string) {
    const currentKey = this.value ? toDayKey(new Date(this.value)) : null;
    // Toggle: clicking the already-selected day clears the selection.
    this.value = currentKey === key ? null : key;
    this.dispatchEvent(new CustomEvent("change", { bubbles: true, composed: true }));
  }

  override render() {
    const grid = buildMonthGrid(this._viewYear, this._viewMonth);
    const selectedKey = this.value ? toDayKey(new Date(this.value)) : null;

    return html`
      <div class="header">
        <uui-button
          compact
          look="secondary"
          label="Previous month"
          ?disabled=${this.disabled}
          @click=${this.#goPrev}
        >‹</uui-button>
        <span class="title">${MONTHS[this._viewMonth]} ${this._viewYear}</span>
        <uui-button
          compact
          look="secondary"
          label="Next month"
          ?disabled=${this.disabled}
          @click=${this.#goNext}
        >›</uui-button>
      </div>

      <div class="grid ${this.disabled ? "dimmed" : ""}">
        ${WEEKDAYS.map((w) => html`<span class="weekday">${w}</span>`)}
        ${grid.map((d) => {
          const disabled =
            this.disabled || !d.inCurrentMonth || isDayDisabled(d.key, this.min, this.max);
          const selected = d.key === selectedKey;
          return html`
            <button
              class="day ${selected ? "selected" : ""} ${d.inCurrentMonth ? "" : "muted"}"
              ?disabled=${disabled}
              @click=${() => this.#selectDay(d.key)}
            >
              ${d.day}
            </button>
          `;
        })}
      </div>
    `;
  }

  static styles = css`
    :host {
      display: block;
      border: 1px solid var(--uui-color-border, #d8d7d9);
      border-radius: var(--uui-border-radius, 3px);
      padding: var(--uui-size-space-3, 9px);
      background: var(--uui-color-surface, #fff);
      min-width: 16rem;
    }
    .header {
      display: flex;
      align-items: center;
      justify-content: space-between;
      margin-bottom: var(--uui-size-space-2, 6px);
    }
    .title {
      font-weight: bold;
    }
    .grid {
      display: grid;
      grid-template-columns: repeat(7, 1fr);
      gap: 2px;
    }
    .grid.dimmed {
      opacity: 0.5;
    }
    .weekday {
      text-align: center;
      font-size: 0.75rem;
      color: var(--uui-color-text-alt, #68676b);
      padding: 2px 0;
    }
    .day {
      aspect-ratio: 1;
      border: none;
      background: transparent;
      border-radius: var(--uui-border-radius, 3px);
      cursor: pointer;
      color: inherit;
      font: inherit;
    }
    .day:hover:not(:disabled) {
      /* light blue tint of the selected colour, clearly visible (was faint gray) */
      background: #c5cdf5;
    }
    .day:disabled {
      color: var(--uui-color-disabled-contrast, #c4c4c4);
      cursor: not-allowed;
    }
    .day.muted {
      visibility: hidden;
    }
    .day.selected {
      background: var(--uui-color-selected, #3544b1);
      color: var(--uui-color-selected-contrast, #fff);
    }
    /* keep the selected day blue + readable on hover (don't let the
       generic hover background hide its white text) */
    .day.selected:hover:not(:disabled) {
      background: #283596;
      color: var(--uui-color-selected-contrast, #fff);
    }
  `;
}

export default BackofficeInlineCalendarElement;

declare global {
  interface HTMLElementTagNameMap {
    "esatto-date-range-calendar": BackofficeInlineCalendarElement;
  }
}
