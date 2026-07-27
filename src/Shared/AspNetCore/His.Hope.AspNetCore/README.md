# His.Hope.AspNetCore

Dependency-light ASP.NET Core building blocks shared by His.Hope services.

## Registration

```csharp
builder.Services.AddHisHopeJwtAuthentication(builder.Configuration);
builder.Services.AddHisHopeProblemDetails();
builder.Services.AddHisHopeHealthChecks();
builder.Services.AddHisHopeOpenApi();

app.UseHisHopeCorrelation();
app.UseHisHopeExceptionHandler();
app.MapHisHopeHealthChecks();
```

`AddHisHopeJwtAuthentication` reads the existing `Jwt:*` configuration keys. A
configured `Jwt:Key` uses HMAC-SHA256; without it, the registration uses the
configured OIDC authority and validates RSA-SHA256 tokens. The package does not
register a token blacklist or any broker, cache, gRPC, database, messaging,
observability, or service-specific dependencies.

`AddHisHopeOpenApi` registers endpoint metadata discovery. Swashbuckle or
another document generator remains an application concern; endpoint handlers
can opt into metadata with `WithHisHopeOpenApi()`.
