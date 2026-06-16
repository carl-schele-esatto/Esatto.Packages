# Esatto.Umbraco.Backoffice.SharedPreviewLink

Shareable preview links for Umbraco 17.

Editors click **Share preview** in any document workspace; a tokenized URL is minted; recipients open the link in any browser (no backoffice login) and see the current draft for that exact page.

- 7-day token validity (data-protected, tampering-proof)
- 5-minute recipient cookie scoped to the specific page path
- Works on **never-published drafts**, not just published-with-edits
- Three friendly error pages: expired (410), invalid (400), not-found (404)

## How it works

1. In any document workspace, click **Share preview**:

   ![Share preview button](https://raw.githubusercontent.com/carl-schele-esatto/Esatto.Packages/main/Esatto.Umbraco.Backoffice.SharedPreviewLink/docs/share-preview-button.png)

2. The document is saved first (the usual variant picker appears for multi-variant docs):

   ![Save modal](https://raw.githubusercontent.com/carl-schele-esatto/Esatto.Packages/main/Esatto.Umbraco.Backoffice.SharedPreviewLink/docs/save-modal.png)

3. A tokenized preview link is minted — copy it, or open it directly:

   ![Preview link modal](https://raw.githubusercontent.com/carl-schele-esatto/Esatto.Packages/main/Esatto.Umbraco.Backoffice.SharedPreviewLink/docs/preview-link-modal.png)

## Install

```bash
dotnet add package Esatto.Umbraco.Backoffice.SharedPreviewLink
```

Then add ONE line to your `Program.cs`, **before** `app.UseUmbraco()`:

```csharp
app.UsePreviewLink();
app.UseUmbraco()
    // ...
```

That's it. The Management API endpoint (`POST /umbraco/management/api/v1/backoffice/preview-link`), the workspace action button, the error page, and the validating middleware are all wired in automatically.

## Architecture

- **Mint** ([`PreviewLinkController`](src/PreviewLinkController.cs)) — `[Authorize(SectionAccessContent)]` Management API endpoint. Takes `{ contentKey }`, uses `ITimeLimitedDataProtector` to protect the GUID for 7 days, appends `?preview-token=...` to the content's absolute URL, returns `{ url, expiresAt }`.
- **Verify** ([`PreviewLinkMiddleware`](src/PreviewLinkMiddleware.cs)) — runs before `app.UseUmbraco()` so the cookie it sets takes effect on the redirected request. Validates the token, ensures the token matches the request path (so a token for page A can't preview page B), generates an Umbraco preview cookie via `IPreviewTokenGenerator`, sets the cookie scoped to the canonical page path, then 302-redirects to the same URL minus the token param.
- **Workspace action** ([`share-preview-action.js`](wwwroot/App_Plugins/Esatto.Umbraco.Backoffice.SharedPreviewLink/share-preview-action.js) + [`share-preview-button.js`](wwwroot/App_Plugins/Esatto.Umbraco.Backoffice.SharedPreviewLink/share-preview-button.js)) — saves the draft via `ctx.requestSave()` before minting, then shows a modal with Copy + Show-in-browser buttons.
- **Error page** ([`Views/PreviewLinkError/Index.cshtml`](Views/PreviewLinkError/Index.cshtml)) — self-contained, inline CSS, no design-system dependency. Adapts to `prefers-color-scheme: dark`. Consumer can override by placing a view at the same path in their app.

## Customizing the error page

Razor view discovery checks the consumer's project first, then referenced assemblies. To override the bundled error page, create `/Views/PreviewLinkError/Index.cshtml` (or `/Views/Shared/Index.cshtml`) in the consuming project with your own markup.

## Endpoint security

The mint endpoint requires `SectionAccessContent` (any backoffice user with Content access can mint links). The verify middleware is anonymous because the token IS the authentication — the data-protection signature is what proves a recipient was authorized.

## Compatibility

| Umbraco | Status |
|---------|--------|
| 17.x    | Verified |

Depends on `Umbraco.Cms.Core`, `Umbraco.Cms.Api.Management`, and `Umbraco.Cms.Web.Common` — all 17.x.

## License

MIT.
