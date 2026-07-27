# His.Hope.ServiceDefaults

Golden-path host composition for His.Hope HTTP services. It standardizes
correlation, ProblemDetails, validation errors, observability, OpenAPI and
`/health/live` plus `/health/ready` endpoints. Rate limiting remains the shared
Redis-backed Infrastructure adapter so it is distributed across replicas. Service-specific
database, broker and authorization checks remain explicit registrations.
