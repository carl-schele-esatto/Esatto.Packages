# Esatto.Umbraco.Backoffice.CustomEditors

A library of reusable **property editor UIs** for the Umbraco backoffice, including an **Encrypted Textbox** for secure data at rest and a **Date Range** editor with dual inline calendars.

![Date Range editor](https://raw.githubusercontent.com/carl-schele-esatto/Esatto.Packages/main/Esatto.Umbraco.Backoffice.CustomEditors/docs/preview.png)

## Editors

### Encrypted Textbox — `Esatto.Umbraco.Backoffice.CustomEditors.EncryptedTextbox`

A text input that encrypts its value at rest using **ASP.NET Core Data Protection**, and transparently decrypts it in the backoffice editor and in `Model.Value(alias)` on the front-end (so templates receive plaintext).

The input includes a reveal (👁) toggle that **masks the value on screen** — masking is **on by default**, so it's safe to bind to sensitive fields without configuration. On a content data type, a **Mask value** toggle lets editors turn masking off.

⚠️ **Operational requirement**: The site must **persist and (for multi-server/containers) share its ASP.NET Core Data Protection key ring**. If that key ring is lost, encrypted values **cannot be recovered**. Additionally, encryption protects data **at rest in the database** — it does not restrict who can read the decrypted value in the backoffice or in templates.

#### Use on a content Data Type
Create a Data Type using the **Encrypted Textbox** editor (stores an encrypted string), then assign
it to a document type property like any other editor.

#### Use from code (e.g. a custom settings schema)
Reference the editor UI alias directly:

```
Esatto.Umbraco.Backoffice.CustomEditors.EncryptedTextbox
```

For example, an Umbraco.AI provider can point a sensitive field's `EditorUiAlias` at this
alias so its connection setting renders encrypted and masked.

#### Read the value in Razor

The value is **transparently decrypted on read**, so it behaves like an ordinary string — only
the database holds ciphertext:

```cshtml
@{
    // Replace "yourPropertyAlias" with your Encrypted Textbox property's alias.
    var secret = Model.Value<string>("yourPropertyAlias");
}
```

`secret` is the decrypted plaintext. Treat it as sensitive — it is only protected at rest in the
database, not wherever you render or pass it.

### Date Range — `Esatto.Umbraco.Backoffice.CustomEditors.DateRange`

A date range editor that presents two **inline calendars** — a **start** and an **end** —
shown side by side with no popups.

- The end calendar is **constrained to the start**: days earlier than the chosen start are
  disabled and cannot be selected.
- Moving the start **past** an already-chosen end **clears the end**, so the range is never
  invalid.
- Click the already-selected day to **deselect** it (clearing the start clears the whole
  range).
- Configurable **date-only vs date+time** per data type — when time is enabled, each end gets
  a time input.
- Optional **min/max bounds** restrict the selectable dates on both calendars.
- The value is stored as JSON `{ "from": "...", "to": "..." }`, where each value is an
  **ISO 8601** string (`2026-05-01` in date-only mode, `2026-05-01T09:00:00` in date+time
  mode), or `null` when unset.

#### Use on a Data Type

1. Create a new **Data Type** and choose the **Date Range** editor.
2. Configure the available settings:
   - **Include time** — also capture a time for each end of the range (date+time mode).
   - **Earliest selectable date** — optional; dates before this cannot be chosen.
   - **Latest selectable date** — optional; dates after this cannot be chosen.
3. Assign the data type to a document type property like any other editor.

![Data type settings](https://raw.githubusercontent.com/carl-schele-esatto/Esatto.Packages/main/Esatto.Umbraco.Backoffice.CustomEditors/docs/settings.png)

#### Read the value in Razor

The `Umbraco.Plain.Json` storage schema returns the value as a
`System.Text.Json.JsonDocument`, e.g. `{ "from": "2026-05-01", "to": "2026-05-10" }`
(with a time component in date+time mode). Parse it with `DateTimeStyles.RoundtripKind`,
which handles both the date-only and date+time forms:

```cshtml
@using System.Text.Json
@using System.Globalization

@{
    // Replace "yourPropertyAlias" with the alias of your Date Range property.
    var range = Model.Value<JsonDocument>("yourPropertyAlias");

    string? fromRaw = null;
    string? toRaw = null;

    if (range is not null && range.RootElement.ValueKind == JsonValueKind.Object)
    {
        if (range.RootElement.TryGetProperty("from", out var fromEl)
            && fromEl.ValueKind == JsonValueKind.String)
        {
            fromRaw = fromEl.GetString();
        }
        if (range.RootElement.TryGetProperty("to", out var toEl)
            && toEl.ValueKind == JsonValueKind.String)
        {
            toRaw = toEl.GetString();
        }
    }

    // RoundtripKind parses BOTH "2026-05-01" and "2026-05-01T09:00:00".
    DateTime? from = DateTime.TryParse(fromRaw, CultureInfo.InvariantCulture,
        DateTimeStyles.RoundtripKind, out var f) ? f : null;
    DateTime? to = DateTime.TryParse(toRaw, CultureInfo.InvariantCulture,
        DateTimeStyles.RoundtripKind, out var t) ? t : null;
}

@if (from.HasValue && to.HasValue)
{
    <p><strong>@from.Value.ToString("d MMM yyyy")</strong> &ndash;
       <strong>@to.Value.ToString("d MMM yyyy")</strong></p>
}
else
{
    <p><em>No date range set.</em></p>
}
```

> The property alias (`yourPropertyAlias` above) is whatever you named the property
> when you added the Date Range data type to your document type.

## Install

```
dotnet add package Esatto.Umbraco.Backoffice.CustomEditors
```

## Develop

The backoffice client lives in `Client/` (TypeScript + Lit + Vite). `dotnet build` runs the
client build automatically; the compiled assets are emitted to
`wwwroot/App_Plugins/Esatto.Umbraco.Backoffice.CustomEditors/`.

```
cd Client
npm install
npm run build      # or: npm run watch
npm test           # runs the Date Range logic tests (vitest)
```
