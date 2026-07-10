import { html, LitElement } from "@umbraco-cms/backoffice/external/lit";
import { UmbElementMixin } from "@umbraco-cms/backoffice/element-api";
import { umbHttpClient } from "@umbraco-cms/backoffice/http-client";
import { tryExecute } from "@umbraco-cms/backoffice/resources";

const LIST_URL = "/umbraco/management/api/v1/crosscontent/cases/list";
const SECURITY = [{ scheme: "bearer", type: "http" }];

// Single-select picker of cross-content items across one or more content types. Lists items from
// the server-side proxy (which holds the API key) and stores { key, type } as JSON
// (Umbraco.Plain.Json). `type` is the item's real content type, so the consumer can render per type.
export class BackofficeCrossContentCasePicker extends UmbElementMixin(LitElement) {
  static properties = {
    value: { type: Object },
    _items: { state: true },
    _types: { state: true },
    _selectedType: { state: true },
    _loading: { state: true },
    _error: { state: true },
  };

  constructor() {
    super();
    this._items = [];
    this._types = [];
    this._selectedType = "all";
    this._loading = false;
    this._error = "";
  }

  connectedCallback() {
    super.connectedCallback();
    this._fetchList();
  }

  async _fetchList() {
    this._loading = true;
    this._error = "";
    const { data, error } = await tryExecute(this, umbHttpClient.get({ url: LIST_URL, security: SECURITY }));
    this._loading = false;
    if (error) {
      this._error = "Could not load content.";
      this._items = [];
      this._types = [];
      return;
    }
    this._items = data?.items ?? [];
    this._types = data?.types ?? [];
    this._selectedType = "all";
  }

  get _visibleItems() {
    if (this._selectedType === "all") return this._items;
    return this._items.filter((i) => i.type === this._selectedType);
  }

  _onTypeChange(e) {
    this._selectedType = e.target.value ?? "all";
  }

  _select(item) {
    this.value = { key: item.key, type: item.type };
    this.dispatchEvent(new CustomEvent("property-value-change", { composed: true }));
  }

  _renderFilter() {
    if (this._types.length <= 1) return "";
    const options = [
      { name: "All", value: "all", selected: this._selectedType === "all" },
      ...this._types.map((t) => ({ name: t.label, value: t.alias, selected: this._selectedType === t.alias })),
    ];
    return html`
      <uui-select
        style="margin-bottom: var(--uui-size-space-3)"
        label="Filter by type"
        .options=${options}
        @change=${this._onTypeChange}></uui-select>`;
  }

  render() {
    if (this._loading) return html`<uui-loader-bar></uui-loader-bar>`;
    if (this._error) return html`<p style="color:var(--uui-color-danger)">${this._error}</p>`;
    if (!this._items.length) return html`<p style="color:var(--uui-color-text-alt)">No content found.</p>`;

    const selectedKey = this.value?.key;
    const items = this._visibleItems;
    return html`
      ${this._renderFilter()}
      <uui-radio-group>
        ${items.map(
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
