import {
  LitElement,
  css,
  html,
  customElement,
  property,
  state,
} from "@umbraco-cms/backoffice/external/lit";
import { UmbElementMixin } from "@umbraco-cms/backoffice/element-api";
import { UmbChangeEvent } from "@umbraco-cms/backoffice/event";
import type {
  UmbPropertyEditorUiElement,
  UmbPropertyEditorConfigCollection,
} from "@umbraco-cms/backoffice/property-editor";
import {
  UMB_VALIDATION_CONTEXT,
} from "@umbraco-cms/backoffice/validation";
import type { UmbDateRangeValue, DateRangeConfig } from "./types.js";
import {
  normalizeValue,
  applyStartChange,
  applyEndChange,
  isRangeValid,
} from "./date-range.logic.js";
import "./inline-calendar.element.js";

const VALIDATION_KEY = "esatto-date-range-invalid";

@customElement("esatto-date-range")
export class BackofficeDateRangeEditorElement
  extends UmbElementMixin(LitElement)
  implements UmbPropertyEditorUiElement
{
  @property({ type: Object })
  public value: UmbDateRangeValue | null = null;

  @state()
  private _config: DateRangeConfig = { includeTime: false, minDate: null, maxDate: null };

  #validationContext?: typeof UMB_VALIDATION_CONTEXT.TYPE;

  @property({ attribute: false })
  public set config(config: UmbPropertyEditorConfigCollection | undefined) {
    this._config = {
      includeTime: config?.getValueByAlias<boolean>("includeTime") ?? false,
      minDate: config?.getValueByAlias<string>("minDate") ?? null,
      maxDate: config?.getValueByAlias<string>("maxDate") ?? null,
    };
  }

  constructor() {
    super();
    this.consumeContext(UMB_VALIDATION_CONTEXT, (context) => {
      this.#validationContext = context;
      this.#syncValidation();
    });
  }

  /** The current range, always normalized to a non-null object for rendering. */
  get #range(): UmbDateRangeValue {
    return normalizeValue(this.value);
  }

  #commit(next: UmbDateRangeValue) {
    // Store null when completely empty so mandatory validation treats it as unset.
    this.value = next.from === null && next.to === null ? null : next;
    this.dispatchEvent(new UmbChangeEvent());
    this.#syncValidation();
  }

  /** Add or clear a client validation message for an invalid (from > to) range. */
  #syncValidation() {
    const ctx = this.#validationContext;
    if (!ctx) return;
    ctx.messages.removeMessageByKey(VALIDATION_KEY);
    if (!isRangeValid(this.#range)) {
      ctx.messages.addMessage(
        "client",
        "$",
        "The end date cannot be before the start date.",
        VALIDATION_KEY
      );
    }
  }

  /** Combine a day key with a time string into the stored ISO value. */
  #compose(dayKey: string, time: string | null): string {
    if (!this._config.includeTime) return dayKey;
    return `${dayKey}T${time && time.length ? time : "00:00"}:00`;
  }

  #dayPart(iso: string | null): string | null {
    return iso ? iso.slice(0, 10) : null;
  }

  #timePart(iso: string | null): string {
    if (!iso || iso.length < 16) return "00:00";
    return iso.slice(11, 16);
  }

  #onStartDay = (e: Event) => {
    const dayKey = (e.target as HTMLElement & { value: string | null }).value;
    // Deselecting the start clears the whole range (the end needs a start).
    if (!dayKey) {
      this.#commit({ from: null, to: null });
      return;
    }
    const composed = this.#compose(dayKey, this.#timePart(this.#range.from));
    this.#commit(applyStartChange(this.#range, composed));
  };

  #onEndDay = (e: Event) => {
    // An end date is only meaningful once a start exists.
    if (!this.#range.from) return;
    const dayKey = (e.target as HTMLElement & { value: string | null }).value;
    // Deselecting the end just clears the end.
    if (!dayKey) {
      this.#commit({ from: this.#range.from, to: null });
      return;
    }
    const composed = this.#compose(dayKey, this.#timePart(this.#range.to));
    this.#commit(applyEndChange(this.#range, composed));
  };

  #onStartTime = (e: Event) => {
    const time = (e.target as HTMLInputElement).value;
    const day = this.#dayPart(this.#range.from);
    if (!day) return;
    this.#commit(applyStartChange(this.#range, this.#compose(day, time)));
  };

  #onEndTime = (e: Event) => {
    const time = (e.target as HTMLInputElement).value;
    const day = this.#dayPart(this.#range.to);
    if (!day) return;
    this.#commit(applyEndChange(this.#range, this.#compose(day, time)));
  };

  override render() {
    const range = this.#range;
    // The end calendar can never go before the start.
    const endMin = range.from ?? this._config.minDate;

    return html`
      <div class="calendars">
        <div class="cal-col">
          <span class="label">Start</span>
          <esatto-date-range-calendar
            .value=${this.#dayPart(range.from)}
            .min=${this._config.minDate}
            .max=${this._config.maxDate}
            @change=${this.#onStartDay}
          ></esatto-date-range-calendar>
          ${this._config.includeTime && range.from
            ? html`<input
                type="time"
                .value=${this.#timePart(range.from)}
                @change=${this.#onStartTime}
              />`
            : null}
        </div>

        <div class="cal-col">
          <span class="label">End</span>
          <esatto-date-range-calendar
            .value=${this.#dayPart(range.to)}
            .min=${endMin}
            .max=${this._config.maxDate}
            ?disabled=${!range.from}
            @change=${this.#onEndDay}
          ></esatto-date-range-calendar>
          ${!range.from
            ? html`<span class="hint">Select a start date first.</span>`
            : null}
          ${this._config.includeTime && range.to
            ? html`<input
                type="time"
                .value=${this.#timePart(range.to)}
                @change=${this.#onEndTime}
              />`
            : null}
        </div>
      </div>
    `;
  }

  static styles = css`
    :host {
      display: block;
    }
    .calendars {
      display: flex;
      gap: var(--uui-size-layout-1, 24px);
      flex-wrap: wrap;
    }
    .cal-col {
      display: flex;
      flex-direction: column;
      gap: var(--uui-size-space-2, 6px);
    }
    .label {
      font-weight: bold;
    }
    .hint {
      font-size: 0.8rem;
      color: var(--uui-color-text-alt, #68676b);
    }
    input[type="time"] {
      padding: var(--uui-size-space-2, 6px);
      border: 1px solid var(--uui-color-border, #d8d7d9);
      border-radius: var(--uui-border-radius, 3px);
    }
  `;
}

export default BackofficeDateRangeEditorElement;

declare global {
  interface HTMLElementTagNameMap {
    "esatto-date-range": BackofficeDateRangeEditorElement;
  }
}
