# Shared Package Governance

This is the operating contract for teams that change and consume shared Angular, mobile, and .NET platform packages across time zones.

## Source Of Truth

[`config/shared-package-catalog.json`](../../config/shared-package-catalog.json) records each supported package, its owner, release channel, and package manifest. `scripts/validate-shared-package-governance.ps1` validates that the catalog and publishable package metadata agree.

## Ownership And Boundaries

| Package family                         | Owner             | Consumer responsibility                                                              |
| -------------------------------------- | ----------------- | ------------------------------------------------------------------------------------ |
| `@his-hope/frontend-foundation`        | Frontend platform | Import public secondary entrypoints only; own feature workflow and domain copy.      |
| `@his-hope/mobile-foundation`          | Mobile platform   | Use mobile contracts instead of calling Capacitor plugins from feature code.         |
| `His.Hope.*` backend platform packages | Backend platform  | Depend on immutable NuGet versions; keep service domain rules and persistence local. |

## Change And Release Rules

1. Additive backwards-compatible API: minor version.
2. Fixes and documentation-only changes: patch version.
3. Removed or semantically changed public API: major version, migration guide, and at least one release of deprecation where practical.
4. Every publishable npm package must update its own changelog. NuGet packages are released by the shared platform release workflow with a version input and release artifact.
5. Consumers use explicit versions in production. `latest`, floating NuGet ranges, and raw shared source imports are not supported integration paths.
6. Publish to `canary` first for cross-team validation, then `beta`, then `stable`. Stable versions are immutable.

## Required Validation

Run before requesting a shared-package release:

```powershell
pwsh -NoProfile -File scripts/validate-shared-package-governance.ps1
pwsh -NoProfile -File scripts/validate-shared-platform-boundaries.ps1
npm run validate:foundation
npm run build:shared
dotnet test His.Hope.sln --configuration Release
```

The release owner records consumer upgrade notes and the compatibility impact in the package changelog or release notes. Teams in different locations use the catalog, changelog, versioned artifacts, and CI results as the asynchronous handoff; a chat message is not a release contract.
