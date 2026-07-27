# Shared Platform Release Policy

## Versioning

- Packages use SemVer and are released together only when a shared contract changes require coordinated adoption.
- `His.Hope.Contracts` breaking changes require a new major version or a new API contract version.
- `His.Hope.AspNetCore`, `His.Hope.Validation`, `His.Hope.ServiceDefaults`, `His.Hope.Messaging.*`, and `His.Hope.Observability` must preserve source and binary compatibility within a major version.
- Every release must update the package changelog and migration notes before publishing.

## Promotion

1. Restore with NU1605 and NU1901-NU1904 as errors.
2. Build and run the complete solution test suite.
3. Pack every shared package with the requested SemVer.
4. Generate a CycloneDX SBOM.
5. Sign every `.nupkg` with the organization certificate and timestamp authority.
6. Publish to the internal feed only after the release workflow is approved.
7. Promote the package from quarantine to production after downstream contract tests pass.

Services may use project references during local migration, but production builds must consume a promoted internal package version. No service may silently float to a newer package version.

`His.Hope.Messaging` is intentionally excluded from the production release list
because it contains in-memory stores for tests and local development. Production
hosts must use the durable RabbitMQ/Redis/SQL adapters from the infrastructure
stack and depend only on `His.Hope.Messaging.Abstractions` at the platform seam.
