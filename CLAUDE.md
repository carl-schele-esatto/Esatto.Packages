# CLAUDE.md

## Esatto package workflows (triggers)

When Carl says **"renew"**, **"polish"**, or **"build a new package"** for the Umbraco backoffice packages in this repo, follow the recorded workflow — full detail lives in Claude memory: `renew-package-workflow`, `polish-package-workflow`, `build-new-package-workflow`, and `esatto-packages-conventions` (read them before acting; verify against the current repo since memory is point-in-time).

Quick summary:
- **renew** — rename a legacy `Backoffice.*` → `Esatto.Umbraco.Backoffice.*` (expand abbreviations, e.g. Dnd→DragAndDrop) and publish public; includes xUnit tests, icon, README screenshots, a targeted DRY/clean-code/best-practice pass, the source-code link, and rewiring the `AI.Woowoo` consumer.
- **polish** — add the source-code link (+ icon if missing) to an existing package; for public packages, bump a patch and re-release.
- **build a new package** — scaffold a new `Esatto.Umbraco.Backoffice.<Name>` mirroring a sibling; tests, icon, README+screenshots, source-code link; DRY/clean-code from the start; born public.

Invariants (don't drop these):
- Every package carries the **source-code link**: `RepositoryUrl` = repo root, `PackageProjectUrl` = the package's repo subfolder, `RepositoryType` git.
- Public = `Esatto.Umbraco.Backoffice.<Name>` on nuget.org; legacy/private = `Backoffice.<Name>` on the Azure `esatto-packages` feed. Versioning is MinVer via git tag `<PackageId>-<version>` (feed versions are immutable → bump a patch to re-release).
- **I prep + commit + tag + `dotnet pack`, but Carl runs the `dotnet nuget push`.** Never `git commit`/`push` or publish without Carl's explicit approval.
