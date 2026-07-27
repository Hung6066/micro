# His.Hope.Validation

Shared FluentValidation behavior and HTTP validation error contract for His.Hope APIs.

Use `AddHisHopeValidation(assembly)` in each Application project and
`UseHisHopeValidationErrors()` in each HTTP host. Domain validators remain owned by
the service that owns the request type.
