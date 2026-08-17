# His.Hope.ServiceDefaults

Golden-path host composition for His.Hope HTTP services. The single entrypoint
for a normal service is `AddHisHopeServicePlatform(...)`. It standardizes
correlation, ProblemDetails, validation errors, observability, OpenAPI and
`/health/live` plus `/health/ready` endpoints. Rate limiting remains the shared
Redis-backed Infrastructure adapter so it is distributed across replicas. Service-specific
database, broker and authorization checks remain explicit registrations.

`AddHisHopeServiceDefaults(...)` remains available for BFFs and special hosts
that intentionally do not need the full enterprise Infrastructure adapter.

The defaults also install the shared regionalization contract from
`docs/architecture/regionalization-standard.md`: `vi-VN`/`en-US`, IANA
timezone hints, UTC service timestamps, and ISO 4217 currency metadata.
