# Esatto.CookieScan.Cli

A .NET tool that audits a site's cookie consent banner. It drives a real headless browser through
the site once for **every consent decision a visitor can make**, and compares what actually gets set
against what the cookie policy page already declares.

- Anything set outside the consent it needed is a **violation** — a non-zero exit code
- Anything set but undeclared becomes a **draft** addition to the policy page for an editor to review
- Cookies, `localStorage` and `sessionStorage`, per pass, with the page that set them
- Keeps the last 50 scans, so two runs can be compared
- Writes a JSON and a Markdown report; exit code gates CI

## Install

```bash
dotnet tool install -g Esatto.CookieScan.Cli
esatto-cookiescan --url https://example.com
```

## Options

| Flag | Meaning |
| --- | --- |
| `--url` | The site to scan. Required, and must include a scheme. |
| `--target` | The site holding the policy page, when it is not the site being scanned. Defaults to `--url`. |
| `--max-pages` | How many pages to crawl. Default 25. |
| `--locale` | `sv` or `en` — the language the generated wording is written in. Default `sv`. |
| `--member-email`, `--member-password` | Sign in and scan the member area too. Omit both to scan only public pages. |
| `--client-id` | The API user that may write findings back. Omit to run report-only. |
| `--consent-cookie` | What this site calls the banner's own consent cookie, if it is not `cookie-consent`. |
| `--dry-run` | Plan the write-back and report it without saving. |
| `--report-dir` | Where the two report files go. Default: the working directory. |
| `--headed` | Show the browser. For debugging the scanner itself. |

**There is no `--client-secret`.** It is read from `ESATTO_COOKIESCAN_CLIENT_SECRET`, because a
secret passed as an argument ends up in shell history and in every process listing.

### `--consent-cookie` is worth reading twice

The banner's consent cookie is the one entry in the catalogue whose name is per-site configuration
rather than a fact about a product: `CookieBannerOptions.CookieName` defaults to `cookie-consent`,
and a site may change it. Leave it wrong and you get two loud false findings in one run — the site's
real consent cookie is unrecognised, so it takes the catalogue's unknown category, is seen on the
reject-all pass, and is reported as a **violation**; while `cookie-consent` is simultaneously
reported as declared-but-never-found. On the one cookie that exists to record a refusal.

## Exit codes

| Code | Meaning |
| --- | --- |
| `0` | No violations. Report-only counts as success — a missing credential is not an error. |
| `1` | At least one violation. |
| `2` | A write-back was configured, attempted and failed. Findings outrank plumbing, so `1` wins over `2`. |

## What it deliberately does not do

- **It never runs on the production server.** Chromium runs on the machine running the tool.
- **It never rewrites an existing declaration.** The merge is append-only: a declaration's purpose
  text is legal wording an editor may have hand-written.
- **It never publishes.** A successful write-back saves a draft. A placeholder purpose on an
  unrecognised cookie must not become public legal text without a human reading it first.
- **It never guesses a tracking pixel.** The storage-type list has a `Pixel` option; a browser
  exposes no way to detect one, so the scanner does not pretend to.
- **`/umbraco` is excluded from the crawl.** Backoffice cookies are not a visitor's cookies.
- The only forms it submits are the member login and the consent decision itself. Nothing here
  books, cancels, registers or pays.

## Writing findings back

Report-only needs nothing. To let a scan append its findings to an Umbraco cookie policy page,
install [`Esatto.Umbraco.Backoffice.CookieScan`](https://www.nuget.org/packages/Esatto.Umbraco.Backoffice.CookieScan)
in the site, then pass `--client-id` with the secret in the environment.

## Overriding the catalogue

The known-cookie catalogue — name patterns mapped to a provider, a category and the wording to put
on the policy page — is data, not code, because its `purpose` text becomes public legal wording. A
`cookie-catalogue.json` **beside the executable** replaces the built-in one wholesale.

## Related packages

| Package | What it is |
| --- | --- |
| `Esatto.CookieScan.Engine` | The engine, to build your own front end on |
| `Esatto.CookieScan.Core` | The rules alone — no browser, no HTTP, no Umbraco |
| `Esatto.Umbraco.Backoffice.CookieScan` | The site-side endpoint this tool writes through |
| `Esatto.Umbraco.Backoffice.CookieBanner` | The consent banner and policy page it audits |

## License

MIT.
