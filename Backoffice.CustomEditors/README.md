# Backoffice.CustomEditors

A small library of reusable **property editor UIs** for the Umbraco backoffice.

## Editors

### Masked Text Box — `Backoffice.CustomEditors.MaskedTextBox`

A text input that hides its value behind a reveal (👁) toggle — for API keys, secrets,
tokens and other sensitive fields. Masking is **on by default**, so it's safe to bind to
sensitive fields without configuration. On a content data type, a **Mask value** toggle lets
editors turn masking off.

> Masking is a UI affordance (it stops shoulder-surfing / screenshots). It does **not**
> encrypt the value — persistence/encryption is the responsibility of the consuming feature.

#### Use on a content Data Type
Create a Data Type using the **Masked Text Box** editor (stores a plain string), then assign
it to a document type property like any other editor.

#### Use from code (e.g. a custom settings schema)
Reference the editor UI alias directly:

```
Backoffice.CustomEditors.MaskedTextBox
```

For example, an Umbraco.AI provider can point a sensitive field's `EditorUiAlias` at this
alias so its connection setting renders masked.

## Install

```
dotnet add package Backoffice.CustomEditors
```

## Develop

The backoffice client lives in `Client/` (TypeScript + Lit + Vite). `dotnet build` runs the
client build automatically; the compiled assets are emitted to
`wwwroot/App_Plugins/Backoffice.CustomEditors/`.

```
cd Client
npm install
npm run build      # or: npm run watch
```
