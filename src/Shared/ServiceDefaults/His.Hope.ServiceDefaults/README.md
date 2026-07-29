# His.Hope.ServiceDefaults

Golden-path host composition for His.Hope HTTP services. It standardizes
correlation, ProblemDetails, validation errors, observability, OpenAPI and
`/health/live` plus `/health/ready` endpoints. Rate limiting remains the shared
Redis-backed Infrastructure adapter so it is distributed across replicas. Service-specific
database, broker and authorization checks remain explicit registrations.

The defaults also install the shared regionalization contract from
`docs/architecture/regionalization-standard.md`: `vi-VN`/`en-US`, IANA
timezone hints, UTC service timestamps, and ISO 4217 currency metadata.
