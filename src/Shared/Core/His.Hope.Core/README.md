# His.Hope.Core

Transport-neutral primitives and abstractions shared by His.Hope services.

## Intended Use

Use this package only for stable concepts that do not depend on HTTP, gRPC, persistence, messaging, hosting, or another His.Hope package. Service domain rules remain service-owned.

## Compatibility

- Targets `.NET 8`.
- Follow semantic versioning: additive contracts are minor releases; removed or changed semantics require a major release.
- Consume an explicit package version in deployed services.

## Related Packages

- `His.Hope.Contracts` owns wire-facing DTOs, pagination, errors, and event contracts.
- `His.Hope.ServiceDefaults` owns standard ASP.NET Core host composition.
- Shared package ownership and release policy: `docs/architecture/shared-package-governance.md`.# His.Hope.Core

`His.Hope.Core` is reserved for stable, transport-neutral domain primitives and
abstractions. REST DTOs, gRPC messages, pagination, query parsing, problem
details, bulk-job contracts and event envelopes belong to
`His.Hope.Contracts`.

This project intentionally has no framework or infrastructure dependencies.
