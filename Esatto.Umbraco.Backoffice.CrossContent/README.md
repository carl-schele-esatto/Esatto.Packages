# Esatto.Umbraco.Backoffice.CrossContent

Cross-site content teasers for Umbraco 17 & 18.

> **Scope:** this package is oriented around a "case"-style content type. By default it lists `contentType:casePage` from the target's Delivery API and the picker stores `type: "casePage"`. For a differently-named content type, override `CrossContent:CaseListPath` with your own Delivery-API filter (the consumer only uses each item's `id`/`name`).

## Install

1. Add the package.
2. Configure the site you want to read:

   ```json
   "CrossContent": {
     "BaseUrl": "https://othersite.example",
     "ApiKey": "<the other site's Umbraco:CMS:DeliveryApi:ApiKey>"
   }
   ```

3. To let *other* sites read this one, enable the Delivery API and set its key, and register
   your own `ICrossContentTeaserMapper` (see the interface docs):

   ```csharp
   builder.Services.AddSingleton<ICrossContentTeaserMapper, MyMapper>();
   ```

   The package defaults to the shipped `NullCrossContentTeaserMapper` (registered via
   `TryAddSingleton`), so the producer endpoint returns 404 instead of failing to activate on
   consumer-only installs. A producer site's own registration above wins over this default
   regardless of registration order, so no manual step is required to opt out of the default.

The backoffice picker UI alias is `Esatto.Umbraco.Backoffice.CrossContent.CasePicker` (backed by `Umbraco.Plain.Json`).

## Install (full — consumer + producer)

To **consume** another site's cases:

1. Reference the package.
2. `CrossContent` config → the site you read:
   ```json
   "CrossContent": { "BaseUrl": "https://othersite.example", "ApiKey": "<other site's DeliveryApi key>", "TeaserPath": "api/crosscontent/teaser" }
   ```
   (Override `TeaserPath` only if the target serves its teaser on a non-default/legacy route instead of this package's `api/crosscontent/teaser`.)
3. Copy the three sample configs into your site's uSync folders, **regenerating every GUID**, and add `crossContentCasePage` to your `caseListingPage`'s `<Structure>`. They live under `docs/sample-usync/{DataTypes,ContentTypes,Templates}/`, mirroring your site's own uSync folder structure — copy each file into the matching uSync folder:
   - `docs/sample-usync/DataTypes/CrossContentCasePicker.config` → your uSync `DataTypes/`
   - `docs/sample-usync/ContentTypes/crosscontentcasepage.config` → your uSync `ContentTypes/`
   - `docs/sample-usync/Templates/CrossContentCasePage.config` → your uSync `Templates/`

   The backoffice picker (`Esatto.Umbraco.Backoffice.CrossContent.CasePicker`) ships with the package — no manifest entry needed.
4. Add a redirect template `CrossContentCasePage.cshtml` (non-generic `UmbracoViewPage`, read `{key}` via `Value<object>()`, `@inject ICrossContentTeaserClient`, 302 → teaser `Url`).
5. Render: generalize your case-listing rendering to a source-agnostic card that reads either local media or the teaser's absolute `TeaserImageUrl`; fetch cross-content teasers via `ICrossContentTeaserClient`.
6. Add the target host to your CSP `img-src` (teaser images are absolute URLs on the other site).

To be **read by** another site (producer):

1. Enable the Delivery API: `Umbraco:CMS:DeliveryApi { "Enabled": true, "PublicAccess": false, "ApiKey": "<your key>" }`.
2. Register a mapper: `builder.Services.AddSingleton<ICrossContentTeaserMapper, MyMapper>();` mapping your case doc type → `CrossContentTeaser`. (Without one, the shipped `NullCrossContentTeaserMapper` default makes the endpoint 404.)

The consumer's `CrossContent:ApiKey` must equal the producer's `Umbraco:CMS:DeliveryApi:ApiKey`.
