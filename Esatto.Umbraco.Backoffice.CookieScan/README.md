# Esatto.Umbraco.Backoffice.CookieScan

The site-side half of the Esatto cookie scanner, for Umbraco 18. Adds **one** management-API endpoint
that appends a scan's findings to the cookie policy page as a draft, plus the API user and client
credentials that authenticate the post.

- Append-only by construction — an existing declaration is never rewritten
- Never publishes — findings land as a draft for an editor to read
- Self-registering: install it and the endpoint exists
- Idempotent API-user seeding, and a failure can never take the site down

Companion to
[`Esatto.Umbraco.Backoffice.CookieBanner`](https://www.nuget.org/packages/Esatto.Umbraco.Backoffice.CookieBanner),
whose `cookieDefinition` element type and `cookiePolicy` page it writes into. Run the scan itself
with [`Esatto.CookieScan.Cli`](https://www.nuget.org/packages/Esatto.CookieScan.Cli).

## Install

```bash
dotnet add package Esatto.Umbraco.Backoffice.CookieScan
```

The endpoint needs no wiring — `CookieScanComposer` registers it, and MVC finds the controller the
same way it finds every other management-API controller. Two things are yours to add:

```csharp
// After BootUmbracoAsync(). Creates the scanner's API user if configured to; never throws.
await app.BootUmbracoAsync();
await app.Services.SeedCookieScanApiUserAsync();
```

```jsonc
// appsettings.json
{
  "Esatto": {
    "CookieScan": {
      "ApiUser": {
        "Enabled": true,
        "ClientId": "cookie-scanner",
        "Name": "Cookie scanner",
        "Email": "cookie-scanner@localhost"
        // ClientSecret belongs in appsettings.Secrets.json or an environment variable,
        // never in a tracked file.
      }
    }
  }
}
```

The seeder is registered but deliberately **not run** from a boot notification. Creating the API user
needs a booted site — the user service, and OpenIddict's application store — and the seeder swallows
its own failures so a missing scanner credential can never take a site down. Those two facts
together are why the trigger stays in the host's hands: run a moment too early, a seeder that fails
silently leaves an operator with a token endpoint rejecting a client id nobody can see is missing.

A site that already keeps these settings under a section of its own binds it explicitly instead:

```csharp
builder.Services.ConfigureCookieScanApiUser(
    builder.Configuration.GetSection("MySite:CookieScanApiUser"));
```

## Endpoint

```
POST /umbraco/management/api/v1/cookie-scan/merge
```

Requires `AuthorizationPolicies.BackOfficeAccess`. `dryRun: true` plans and reports without saving.

**A narrow, purpose-built endpoint rather than the generic document endpoint, on purpose.**
`UpdateDocumentRequestModel` makes a document `PUT` a whole-document *replace*: an omitted property
is erased, so a client rebuilding the payload from outside could silently blank the policy page's
introduction or outro. Here the merge happens server-side with Umbraco's own Block List types, and
the only thing that can be touched is one property of one node.

## What it will not do

- **It never rewrites an existing declaration.** A declaration's `purpose` is legal wording an editor
  may have hand-written, and a tool that silently rewrote it would be worse than no tool. See
  `MergePlanner.Plan` and `CookieScanWriter.Append`.
- **It never publishes.** A successful write-back calls `IContentService.Save`, never `Publish`.
- **It never declares a `Pixel`.** The storage-type list offers one; nothing a browser exposes can
  detect a tracking pixel, so the scanner does not guess and the writer refuses the value.
- Everything a caller could fix comes back as a `400` with the reason in plain text, because the
  caller is a command-line tool printing it straight to an operator.

## The client id has a prefix you did not type

`IUserService.AddClientIdAsync` stores exactly the string it is given, but Umbraco's own token
endpoint normalises a client id through `ClientCredentialsManagerBase.SafeClientId`, which prepends
`umbraco-back-office-`. The user↔client-id association therefore has to be stored **prefixed** or
the lookup can never find it, while the OpenIddict application registration is matched
byte-for-byte against the unprefixed `--client-id` the tool puts on the wire. The seeder applies that
normalisation to the association and only to the association. This is verified against decompiled
Umbraco 18.1.1; re-verify it on a future major.

## Compatibility

| Umbraco | Status |
| --- | --- |
| 18.x | Verified |
| 17.x | Untested — the seeder is built on 18.1.1 internals whose declaration sites may move |

## License

MIT.
