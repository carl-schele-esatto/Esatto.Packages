import { html, LitElement } from "@umbraco-cms/backoffice/external/lit";
import { UmbElementMixin } from "@umbraco-cms/backoffice/element-api";
import { umbHttpClient } from "@umbraco-cms/backoffice/http-client";
import { tryExecute } from "@umbraco-cms/backoffice/resources";

const LIST_URL = "/umbraco/management/api/v1/crosscontent/cases/list";
const SECURITY = [{ scheme: "bearer", type: "http" }];

// Single-select picker of cross-content cases. Lists cases from the server-side proxy (which holds
// the API key) and stores { key, type: "casePage" } as JSON (Umbraco.Plain.Json). `type` is stored
// now so other content types can be added later with no data migration.
export class BackofficeCrossContentCasePicker extends UmbElementMixin(LitElement) {
  static properties = {
    value: { type: Object },
    _cases: { state: true },
    _loading: { state: true },
    _error: { state: true },
  };

  constructor() {
    super();
    this._cases = [];
    this._loading = false;
    this._error = "";
  }

  connectedCallback() {
    super.connectedCallback();
    this._fetchCases();
  }

  async _fetchCases() {
    this._loading = true;
    this._error = "";
    const { data, error } = await tryExecute(this, umbHttpClient.get({ url: LIST_URL, security: SECURITY }));
    this._loading = false;
    if (error) {
      this._error = "Kunde inte hämta case.";
      this._cases = [];
      return;
    }
    this._cases = data ?? [];
  }

  _select(caseItem) {
    this.value = { key: caseItem.key, type: "casePage" };
    this.dispatchEvent(new CustomEvent("property-value-change", { composed: true }));
  }

  render() {
    if (this._loading) return html`<uui-loader-bar></uui-loader-bar>`;
    if (this._error) return html`<p style="color:var(--uui-color-danger)">${this._error}</p>`;
    if (!this._cases.length) return html`<p style="color:var(--uui-color-text-alt)">Inga case hittades.</p>`;
    const selectedKey = this.value?.key;
    return html`
      <uui-radio-group>
        ${this._cases.map(
          (c) => html`
            <uui-radio
              label=${c.title}
              value=${c.key}
              ?checked=${c.key === selectedKey}
              @change=${() => this._select(c)}></uui-radio>`
        )}
      </uui-radio-group>`;
  }
}

customElements.define("backoffice-cross-content-case-picker", BackofficeCrossContentCasePicker);
