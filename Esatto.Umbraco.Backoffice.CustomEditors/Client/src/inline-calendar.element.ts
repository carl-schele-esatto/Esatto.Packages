import {
  LitElement,
  css,
  html,
  customElement,
  property,
  state,
} from "@umbraco-cms/backoffice/external/lit";
import { UmbElementMixin } from "@umbraco-cms/backoffice/element-api";
import {
  buildMonthGrid,
  isDayDisabled,
  parseDayKey,
  toDayKey,
  type CalendarDay,
} from "./calendar.logic.js";

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

  /** Local key (YYYY-MM-DD) of "today", for aria-current marking. */
  private _todayKey = toDayKey(new Date());

  /**
   * Memoized month grid for the currently-viewed year/month. Recomputed in
   * willUpdate only when _viewYear/_viewMonth change, so render() does no work.
   */
  private _grid: CalendarDay[] = [];

  override willUpdate(changed: Map<string, unknown>) {
    // Re-anchor the visible month only when the inputs that decide it change,
    // so the user's manual prev/next navigation is preserved between changes.
    if (changed.has("value") || changed.has("min")) {
      // A selected value wins; otherwise anchor to the lower bound (e.g. the
      // chosen start date for the end calendar); otherwise the current month.
      // Day keys must be parsed as LOCAL dates (parseDayKey), never via
      // new Date(string) which treats "YYYY-MM-DD" as UTC midnight.
      const anchor = this.value
        ? parseDayKey(this.value)
        : this.min
          ? parseDayKey(this.min)
          : new Date();
      this._viewYear = anchor.getFullYear();
      this._viewMonth = anchor.getMonth();
    }

    // Rebuild the (potentially expensive) grid only when the viewed month moves.
    if (
      changed.has("_viewYear") ||
      changed.has("_viewMonth") ||
      this._grid.length === 0
    ) {
      this._grid = buildMonthGrid(this._viewYear, this._viewMonth);
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
    // `this.value` is already a day key (the parent slices to YYYY-MM-DD), so
    // compare directly — no re-parsing via new Date(string) (UTC day-shift).
    const currentKey = this.value;
    // Toggle: clicking the already-selected day clears the selection.
    this.value = currentKey === key ? null : key;
    this.dispatchEvent(new CustomEvent("change", { bubbles: true, composed: true }));
  }

  /** True if a day cell is non-selectable for any reason. */
  #isDisabled(d: CalendarDay): boolean {
    return (
      this.disabled || !d.inCurrentMonth || isDayDisabled(d.key, this.min, this.max)
    );
  }

  /** Delegated click handler on the grid: read the day key off the button. */
  #onGridClick = (e: Event) => {
    const button = (e.target as HTMLElement)?.closest<HTMLButtonElement>(
      "button.day"
    );
    if (!button || button.disabled) return;
    const key = button.dataset.key;
    if (key) this.#selectDay(key);
  };

  /** Keyboard navigation across the date grid (roving tabindex). */
  #onGridKeydown = (e: KeyboardEvent) => {
    let deltaDays = 0;
    switch (e.key) {
      case "ArrowLeft":
        deltaDays = -1;
        break;
      case "ArrowRight":
        deltaDays = 1;
        break;
      case "ArrowUp":
        deltaDays = -7;
        break;
      case "ArrowDown":
        deltaDays = 7;
        break;
      default:
        return;
    }
    e.preventDefault();

    // Anchor on the focused button's key, else the selected/first in-month day.
    const focused = (e.target as HTMLElement)?.closest<HTMLButtonElement>(
      "button.day"
    );
    const fromKey =
      focused?.dataset.key ??
      this.value ??
      this._grid.find((d) => d.inCurrentMonth)?.key;
    if (!fromKey) return;

    const target = parseDayKey(fromKey);
    target.setDate(target.getDate() + deltaDays);
    const targetKey = toDayKey(target);

    // Move the view to the target's month if it crossed a boundary.
    const targetYear = target.getFullYear();
    const targetMonth = target.getMonth();
    if (targetYear !== this._viewYear || targetMonth !== this._viewMonth) {
      this._viewYear = targetYear;
      this._viewMonth = targetMonth;
    }

    // Focus the target day once Lit has rendered the (possibly new) month.
    this.updateComplete.then(() => {
      const next = this.shadowRoot?.querySelector<HTMLButtonElement>(
        `button.day[data-key="${targetKey}"]`
      );
      next?.focus();
    });
  };

  override render() {
    const grid = this._grid;
    // `this.value` is already a day key from the parent — compare directly.
    const selectedKey = this.value;
    const monthLabel = `${MONTHS[this._viewMonth]} ${this._viewYear}`;

    // Roving tabindex: the focusable day is the selected one, else the first
    // selectable in-month day, so Tab lands on a single, sensible cell.
    const rovingKey =
      grid.find((d) => d.key === selectedKey && !this.#isDisabled(d))?.key ??
      grid.find((d) => d.inCurrentMonth && !this.#isDisabled(d))?.key ??
      null;

    return html`
      <div class="header">
        <uui-button
          compact
          look="secondary"
          label="Previous month"
          ?disabled=${this.disabled}
          @click=${this.#goPrev}
        >‹</uui-button>
        <span class="title">${monthLabel}</span>
        <uui-button
          compact
          look="secondary"
          label="Next month"
          ?disabled=${this.disabled}
          @click=${this.#goNext}
        >›</uui-button>
      </div>

      <div
        class="grid ${this.disabled ? "dimmed" : ""}"
        role="grid"
        aria-label=${monthLabel}
        @click=${this.#onGridClick}
        @keydown=${this.#onGridKeydown}
      >
        ${WEEKDAYS.map((w) => html`<span class="weekday">${w}</span>`)}
        ${grid.map((d) => {
          const disabled = this.#isDisabled(d);
          const selected = d.key === selectedKey;
          const isToday = d.key === this._todayKey;
          // Out-of-month cells are visually hidden + disabled; also hide them
          // from assistive tech so the grid reads as a clean month.
          if (!d.inCurrentMonth) {
            return html`<button class="day muted" disabled aria-hidden="true" tabindex="-1">
              ${d.day}
            </button>`;
          }
          const fullDate = parseDayKey(d.key).toLocaleDateString(undefined, {
            dateStyle: "full",
          });
          return html`
            <button
              class="day ${selected ? "selected" : ""}"
              data-key=${d.key}
              ?disabled=${disabled}
              aria-label=${fullDate}
              aria-selected=${selected ? "true" : "false"}
              aria-current=${isToday ? "date" : "false"}
              tabindex=${d.key === rovingKey ? "0" : "-1"}
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
