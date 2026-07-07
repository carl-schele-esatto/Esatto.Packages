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
   an `ICrossContentTeaserMapper` (see the interface docs). Consumer-only sites may register the
   shipped `NullCrossContentTeaserMapper`.

The backoffice picker UI alias is `Backoffice.CrossContent.CasePicker` (backed by `Umbraco.Plain.Json`).
