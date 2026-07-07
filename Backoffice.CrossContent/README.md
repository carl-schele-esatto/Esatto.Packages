# Backoffice.CrossContent

Cross-site content teasers for Umbraco 17 & 18.

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

The backoffice picker UI alias is `Backoffice.CrossContent.CasePicker` (backed by `Umbraco.Plain.Json`).
