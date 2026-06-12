import {
  LitElement,
  html,
  css,
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

/**
 * A text box that masks its value (like a password field) with a reveal toggle.
 *
 * Masking is controlled by the `mask` config value and defaults to ON, so the editor is
 * safe to bind to sensitive fields without any configuration. On a content data type the
 * "Mask value" toggle lets editors turn it off, which is why this is reusable beyond any
 * single feature.
 *
 * Note: masking is a UI affordance only — it does not encrypt the stored value.
 */
@customElement("bce-masked-text-box")
export default class BceMaskedTextBoxElement
  extends UmbElementMixin(LitElement)
  implements UmbPropertyEditorUiElement
{
  @property({ type: String })
  public value: string = "";

  @state()
  private _mask = true;

  @state()
  private _reveal = false;

  @property({ attribute: false })
  public set config(config: UmbPropertyEditorConfigCollection | undefined) {
    const mask = config?.getValueByAlias<boolean>("mask");
    // Default to masking when unset so sensitive fields are protected by default.
    this._mask = mask === undefined ? true : Boolean(mask);
  }

  #onInput(e: Event) {
    this.value = (e.target as HTMLInputElement).value;
    this.dispatchEvent(new UmbChangeEvent());
  }

  #toggleReveal(e: Event) {
    e.preventDefault();
    e.stopPropagation();
    this._reveal = !this._reveal;
  }

  // Belt-and-suspenders against password-manager extensions: the attributes they
  // honour (1Password, LastPass, Dashlane) must sit on the *native* <input>, which
  // uui-input renders inside its own shadow root. Tag it once it exists.
  protected override async firstUpdated() {
    const uuiInput = this.shadowRoot?.querySelector("uui-input") as
      | (HTMLElement & { updateComplete?: Promise<unknown> })
      | null;
    await uuiInput?.updateComplete;
    const native = uuiInput?.shadowRoot?.querySelector("input");
    if (native) {
      native.setAttribute("data-1p-ignore", "true"); // 1Password
      native.setAttribute("data-lpignore", "true"); // LastPass
      native.setAttribute("data-form-type", "other"); // Dashlane
      native.setAttribute("autocomplete", "new-password");
    }
  }

  render() {
    const type = this._mask && !this._reveal ? "password" : "text";
    const revealLabel = this._reveal ? "Hide value" : "Show value";
    return html`
      <uui-input
        type=${type}
        .value=${this.value ?? ""}
        name="bce-masked-value"
        autocomplete="new-password"
        spellcheck="false"
        data-1p-ignore="true"
        data-lpignore="true"
        data-form-type="other"
        @input=${this.#onInput}
        @change=${this.#onInput}
      >
        ${this._mask
          ? html`<uui-button
              slot="append"
              compact
              look="default"
              label=${revealLabel}
              title=${revealLabel}
              @click=${this.#toggleReveal}
            >
              <uui-icon name=${this._reveal ? "icon-eye-off" : "icon-eye"}></uui-icon>
            </uui-button>`
          : ""}
      </uui-input>
    `;
  }

  static styles = css`
    :host {
      display: block;
    }
    uui-input {
      width: 100%;
    }
  `;
}

declare global {
  interface HTMLElementTagNameMap {
    "bce-masked-text-box": BceMaskedTextBoxElement;
  }
}
