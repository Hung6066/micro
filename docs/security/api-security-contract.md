# His.Hope API security contract

All service APIs use the shared security bootstrap and identity contract:

```csharp
His.Hope.AspNetCore.Authentication.JwtAuthenticationExtensions
    .AddHisHopeJwtAuthentication(builder.Services, builder.Configuration);
builder.Services.AddHisHopeAuthorization();

app.UseAuthentication();
app.UseDpopAuthorizationSchemeNormalization();
app.UseDpopAccessTokenValidation();
app.UseAuthorization();
```

`AddHisHopeJwtAuthentication` normalizes the JWT subject after validation. Both
`sub` and `ClaimTypes.NameIdentifier` are present, so existing handlers and new
handlers cannot disagree about the current user. New handlers should use the
shared helper:

```csharp
var userId = httpContext.User.GetSubject();
```

Use `GetUserId()` when a `Guid` is required. Do not read a client-supplied user
ID for authorization decisions.

## Endpoint rules

- `/api/**` is protected by the authorization fallback policy by default.
- Add an explicit permission or role policy to every business endpoint.
- `AllowAnonymous()` is reserved for discovery, OIDC/SAML/passkey bootstrap,
  health/readiness, public app policy, and external webhook endpoints with an
  independent signature/IP control.
- DPoP normalization and proof validation run before authorization for browser,
  native, and service-to-service calls.
- Keep public bootstrap endpoints narrowly scoped and rate-limited.

## Gate

Run this check before merging a new API service:

```powershell
./scripts/verify-api-security.ps1
```

The gate fails when a service API is missing shared JWT authentication,
authorization middleware, or the His.Hope authorization policy registry.
