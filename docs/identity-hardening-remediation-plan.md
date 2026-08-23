# Identity Service — Security Hardening Remediation Plan

**Version**: 1.0  
**Date**: 2026-07-24  
**Scope**: P1 (hardening release) + P2 (enterprise maturity)  
**Service**: `src/Services/IdentityService/`  
**Audit Reference**: 18 issues found — 7 fixed/partial (2026-08-22), 11 open

**2026-08-22 remediation batch (automated evidence):**
| # | Status | Evidence |
|---|--------|----------|
| 2 | ✅ Done | `AuthenticationRedirectValidator` + integration tests |
| 3 | ✅ Done | `/internal/refresh` → `RequireAuthorization()` + `BffSessionGuard` |
| 5 | ✅ Done | Production fail-fast when MFA Vault transit unavailable |
| 8 | ✅ Partial | BFF tokens protected via `SessionTokenProtector` (`dp:v1:` prefix) |
| — | ✅ Done | AAL policy middleware + `config/assurance-policy.v1.json` |
| — | ✅ Done | OpenFGA canary deny path (`AUTHZ_PDP_MODE=canary`) |
| — | ✅ Done | SIEM/WORM dead-letter + tamper drill scripts |
| — | ✅ Done | Legacy auth deprecation runbook (`docs/runbooks/legacy-auth-deprecation.md`) |

---

## Priority Matrix

Items ordered by `risk × ease × dependency`. Fix top-down — no parallel work on dependent items.

| # | Item | Risk | Effort | Depends On |
|---|------|------|--------|------------|
| 1 | Cookie domain environment-configurable | 🔴 HIGH | 🟢 Small | — |
| 2 | Open redirect whitelist in external callback | 🔴 HIGH | 🟢 Small | — |
| 3 | /internal/refresh explicit authorization | 🔴 HIGH | 🟢 Small | — |
| 4 | Browser login: stop returning tokens in JSON body | 🔴 HIGH | 🟡 Medium | Item 5 (MFA tokens still needed via body) |
| 5 | MFA secret encryption via Vault/KMS | 🔴 HIGH | 🟡 Medium | — |
| 6 | DatabaseAuditService: durable outbox/retry | 🔴 HIGH | 🟡 Medium | — |
| 7 | AuditLogEndpoint: validate payload, whitelist action | 🟡 MED | 🟢 Small | Item 6 |
| 8 | BFF: encrypt/ref-token in Redis instead of raw JWT | 🟡 MED | 🟡 Medium | Item 4 |
| 9 | Rate limit: MFA, SCIM, external login | 🟡 MED | 🟢 Small | — |
| 10 | Production config: fail-fast holistic check | 🟡 MED | 🟡 Medium | Items 5, 8 |
| 11 | JWKS: overlapping key rotation | 🟡 MED | 🔴 Large | Item 10 |
| 12 | Data Protection key ring: Redis/Blob/KMS | 🟡 MED | 🟡 Medium | Item 10 |
| 13 | Password reset / email verify / recovery | 🟡 MED | 🔴 Large | Item 10 |
| 14 | Tenant/facility boundary enforcement | 🟢 LOW | 🔴 Large | — |
| 15 | Health check: readiness vs liveness | 🟢 LOW | 🟢 Small | — |
| 16 | Contract/integration tests for auth flows | 🟢 LOW | 🟡 Medium | All above |
| 17 | Dependency scanning (SBOM, NuGet Audit) | 🟢 LOW | 🟢 Small | — |

---

## P1 — Hardening Release Fix Plans

---

### Fix 1: Cookie Domain Environment-Configurable

**Current state** (`Program.cs:119`):
```csharp
options => options.Cookie.Domain = "localhost"
```

**Fix**:
1. Add config key `Authentication:CookieDomain` to `appsettings.json` (default: unset → no domain restriction)
2. In `appsettings.Development.json`: set to `"localhost"`
3. In `appsettings.Production.json` (new file): set to production domain `"his-hope.vn"`
4. Change `Program.cs:118-119` to:
```csharp
builder.Services.Configure<CookieAuthenticationOptions>(IdentityConstants.ApplicationScheme, options =>
{
    var domain = builder.Configuration["Authentication:CookieDomain"];
    if (!string.IsNullOrEmpty(domain))
        options.Cookie.Domain = domain;
});
```

**Files changed**:
- `Program.cs` — 2 lines
- `appsettings.json` — add 1 key
- `appsettings.Development.json` — add 1 key
- New: `appsettings.Production.json`

**Verify**: Cookie `hishop_sid` has correct Domain in each environment.

---

### Fix 2: Open Redirect Whitelist — External Callback

**Current state** (`Program.cs:767-768`):
```csharp
var returnUrl = httpContext.Request.Query["returnUrl"].FirstOrDefault() ?? "/";
return Results.Redirect(returnUrl);
```

**Fix**:
1. Add config section `Authentication:RedirectWhitelist` (comma-separated absolute URIs + relative base paths)
2. Add `IsValidReturnUrl(string? url)` helper:
   - `/path` → always valid (relative)
   - `http[s]://host/path` → must match whitelist prefix
   - Everything else → invalid → redirect to `/`
3. Apply to: `external-callback` (line 768), `Account/Login` POST (line 1350), `Account/ExternalLogin` (line 1360-1365)

**Files changed**:
- `Program.cs` — add helper + change 3 redirect lines
- `appsettings.json` — add `Authentication:RedirectWhitelist`

**Verify**: 
- `returnUrl=https://evil.com` → redirects to `/`
- `returnUrl=/dashboard` → redirects to `/dashboard`

---

### Fix 3: /internal/refresh Authorization

**Current state** (`Program.cs:606-671`):
- Validates CSRF and UserAgentHash (good)
- No `.RequireAuthorization()` attribute

**Fix**:
Change line 671 from:
```csharp
.RequireRateLimiting("auth");
```
to:
```csharp
.RequireRateLimiting("auth")
.RequireAuthorization();
```

This ensures the cookie session itself was issued to an authenticated user. Combined with existing CSRF+UserAgentHash validation, this provides defense-in-depth.

**Files changed**:
- `Program.cs` — 1 line addition

---

### Fix 4: BFF Login — Stop Returning Tokens in Response Body

**Current state** (`Program.cs:399-456`):
Login sets `hishop_sid` (HttpOnly cookie) AND returns `Results.Ok(result)` with full `TokenResponse`.

**Fix**:
1. Browser login (`POST /api/v1/auth/login`) should only set cookies, return `{ status: "ok", userId }` — no tokens
2. Add a separate `POST /api/v1/auth/token` endpoint for SPA/CLI flows:
   - Accepts `grant_type=password` via OpenIddict `/connect/token` (already exists)
   - Or a direct token endpoint for backward compatibility
3. `/api/v1/auth/login` response becomes:
```csharp
return Results.Ok(new { 
    status = "ok", 
    userId = result.User.Id,
    requiresMfa = false 
});
```
4. MFA verify endpoint (`/mfa/verify`) also needs similar treatment — it currently returns tokens in body (MfaEndpoints.cs:103-105)

**Impact**: SPA clients currently reading tokens from login response body must switch to reading from cookie-based session. This is a **breaking change** for API consumers.

**Files changed**:
- `Program.cs` — login endpoint response (~line 446)
- `MfaEndpoints.cs` — verify endpoint response (~lines 103-105)

**Verify**:
- Browser login: cookies set, tokens NOT in response body
- MFA verify: cookies set, tokens NOT in response body
- Existing SPA flow still works via cookies

---

### Fix 5: MFA Secret Encryption

**Current state** (`MfaEndpoints.cs:43-46`):
```csharp
db.UserMfas.Add(new UserMfa {
    SecretKey = secret,  // plaintext Base32
    ...
});
```

**Fix**:
1. Add `IMfaSecretEncryptor` interface to Application layer:
```csharp
public interface IMfaSecretEncryptor {
    string Encrypt(string plaintext);
    string Decrypt(string ciphertext);
}
```
2. Implement `VaultMfaSecretEncryptor` using Vault transit encrypt/decrypt API:
   - Path: `/v1/transit/encrypt/{keyName}`
   - Path: `/v1/transit/decrypt/{keyName}`
   - Key name from config: `Vault:Transit:MfaKeyName` (default: `mfa-secret`)
3. Dev fallback: `AesMfaSecretEncryptor` using `IDataProtector` for development
4. Change `MfaEndpoints.cs` to encrypt before DB write, decrypt on verify/recover
5. Add `SecretKey` column migration: existing plaintext secrets must be re-encrypted on next enrollment (or add migration script)

**Rate limiting on MFA endpoints** (fix alongside):
- Add `.RequireRateLimiting("mfa")` with 5 req/min per user to enroll, verify, recover
- Policy: `mfa` — 5 requests per 60s (TOTP window = 30s, so max 2 attempts per window)

**Files changed**:
- New: `IMfaSecretEncryptor.cs` (Application/Interfaces)
- New: `VaultMfaSecretEncryptor.cs` (Infrastructure/Services)
- New: `AesMfaSecretEncryptor.cs` (Infrastructure/Services)
- `MfaEndpoints.cs` — encrypt/decrypt calls, rate limiting
- `Program.cs` — DI registration + `mfa` rate limiter policy

---

### Fix 6: DatabaseAuditService — Durable Outbox

**Current state** (`DatabaseAuditService.cs:29-45`):
```csharp
Task.Run(async () => {
    try { await WriteAuditLogAsync(entry); }
    catch { /* swallow */ }
});
```

**Fix**:
1. Replace `Task.Run` with `System.Threading.Channels.Channel<PhiAuditEntry>` as a bounded background queue:
```csharp
private readonly Channel<PhiAuditEntry> _channel = 
    Channel.CreateBounded<PhiAuditEntry>(new BoundedChannelOptions(10_000) {
        FullMode = BoundedChannelFullMode.DropOldest
    });
```
2. Register `DatabaseAuditBackgroundService : BackgroundService` that reads from channel, writes to DB with retry (Polly):
   - Max 3 retries with exponential backoff (1s, 3s, 9s)
   - On final failure: log to Serilog + increment metric `his_hope_audit_loss_total`
   - Dead-letter: write failed entries to a Redis list `his_hope:audit_dlq:{date}` for manual review
3. `LogPhiAccess()` becomes `_channel.Writer.TryWrite(entry)` — non-blocking, drops if full
4. Add Prometheus counter metric: `his_hope_audit_writes_total`, `his_hope_audit_loss_total`, `his_hope_audit_dlq_size`

**Files changed**:
- `DatabaseAuditService.cs` — replace Task.Run with Channel
- New: `DatabaseAuditBackgroundService.cs` — background processor
- `Program.cs` — register background service + metrics
- New: `Metrics/AuditMetrics.cs` — Prometheus counters

---

### Fix 7: AuditLogEndpoint Payload Validation

**Current state** (`AuditLogEndpoints.cs:22-62`):
- Actor from token ✅
- Server timestamp ✅  
- But no action whitelist, no per-event payload limit, `ClientAuditEvent` model has misleading `UserId`/`Timestamp` fields

**Fix**:
1. Whitelist allowed `Action` values: `view`, `create`, `update`, `delete`, `search`, `export`, `print`
   - Reject request with 400 if any event has invalid action
2. Validate per-event `Details` size ≤ 8KB (serialized JSON)
3. Cap `acceptedEvents` at `MaxAuditEventsPerRequest = 50` (down from 100)
4. Sanitize `Details` — strip any embedded scripts, limit nesting depth to 5
5. Remove `Timestamp` and `UserId` from `ClientAuditEvent` model (or mark as `[Obsolete]` ignored on server)
6. Add `CorrelationId` validation: must be GUID format if present
7. Log a warning when events are dropped

**Files changed**:
- `AuditLogEndpoints.cs` — validation logic
- `ClientAuditEvent` record — deprecate or remove client-supplied fields

---

### Fix 8: BFF — Encrypted Token Reference in Redis

**Current state** (`Program.cs:411-420`):
Stores raw `Jwt` + `RefreshToken` in Redis session as plaintext JSON.

**Fix (two-phased approach)**:

**Phase A — Quick win (this release):**
1. Add `IDataProtector` to encrypt the JWT field before Redis storage:
```csharp
var protector = dataProtectionProvider.CreateProtector("HisHope.SessionJwt");
sessionData = sessionData with { 
    Jwt = protector.Protect(result.AccessToken),
    RefreshToken = protector.Protect(result.RefreshToken) 
};
```
2. Decrypt on retrieval in `GetSessionAsync()`:
```csharp
var session = JsonSerializer.Deserialize<SessionData>(sessionJson!);
return session with {
    Jwt = protector.Unprotect(session.Jwt),
    RefreshToken = protector.Unprotect(session.RefreshToken)
};
```
3. Set ACL on Redis key prefix `session:*` to restrict read access (via Redis config if available, or document as infra requirement)

**Phase B — Full token reference (next release):**
1. Replace JWT storage with opaque reference + server-side lookup
2. Store actual JWT only in a separate encrypted store (Vault-backed)

**Files changed (Phase A)**:
- `Program.cs` — BffHelpers + login/refresh/logout endpoints
- Redis ACL documentation

---

## P1 Summary — Implementation Order

```
Week 1:
  Day 1-2: Fix 1 (cookie domain) + Fix 2 (open redirect) + Fix 3 (/internal/refresh auth)
           → 3 small changes, no dependencies, low-risk
  
  Day 3-4: Fix 7 (audit endpoint validation)
           → 1 file change, no dependency
  
  Day 5:   Fix 9 (rate limiting MFA/SCIM/external-login)
           → Program.cs only, small change

Week 2:
  Day 1-3: Fix 5 (MFA secret encryption)
           → New services + DI + migration
  
  Day 4-5: Fix 4 (stop returning tokens in body)
           → Breaking change, coordinate with frontend team

Week 3:
  Day 1-3: Fix 6 (DatabaseAuditService durable queue)
           → Channel + BackgroundService + metrics
  
  Day 4-5: Fix 8 (encrypted token reference in Redis)
           → DataProtection encryption

Final: Update appsettings.Production.json, test full flow
```

---

## P2 — Enterprise Maturity Roadmap

| # | Item | Quarter | Effort | Pre-reqs |
|---|------|---------|--------|----------|
| 10 | Production config fail-fast | Q3 Week 1-2 | 3d | Fix 5, 8 |
| 11 | JWKS overlapping key rotation | Q3 Week 2-4 | 5d | Fix 10 |
| 12 | Data Protection key ring shared | Q3 Week 1-2 | 3d | Fix 10 |
| 13 | Password reset / email verify / recovery | Q3-Q4 | 10d | Fix 10, 12 |
| 14 | Tenant/facility boundary | Q4 | 8d | — |
| 15 | Health check readiness/liveness | Q3 Week 1 | 1d | — |
| 16 | Contract/integration tests | Q3 ongoing | 5d | Phase 1 complete |
| 17 | Dependency scanning | Q3 Week 1 | 1d | — |

---

## New Files Required

```
IdentityService.Api/
├── appsettings.Production.json          (NEW — Fix 1)
├── Configuration/
│   └── RedirectValidation.cs            (NEW — Fix 2)
└── Metrics/
    └── AuditMetrics.cs                  (NEW — Fix 6)

IdentityService.Application/
└── Interfaces/
    └── IMfaSecretEncryptor.cs            (NEW — Fix 5)

IdentityService.Infrastructure/
└── Services/
    ├── VaultMfaSecretEncryptor.cs        (NEW — Fix 5)
    ├── AesMfaSecretEncryptor.cs          (NEW — Fix 5)
    └── DatabaseAuditBackgroundService.cs (NEW — Fix 6)
```

## Config Keys Added to appsettings.json

```json
{
  "Authentication": {
    "CookieDomain": "",                    // Fix 1 — empty = no domain restriction
    "RedirectWhitelist": [                 // Fix 2
      "https://his-hope.vn",
      "https://dashboard.his-hope.vn",
      "http://localhost:4200",
      "http://localhost:4201"
    ]
  },
  "Vault": {
    "Transit": {
      "MfaKeyName": "mfa-secret"          // Fix 5
    }
  },
  "RateLimiting": {
    "Mfa": { "PermitLimit": 5, "WindowSeconds": 60 },    // Fix 9
    "Scim": { "PermitLimit": 60, "WindowSeconds": 60 },   // Fix 9
    "ExternalLogin": { "PermitLimit": 10, "WindowSeconds": 60 }  // Fix 9
  }
}
```

---

## Verification Checklist

Per-fix verification is listed inline. Global verification:

- [ ] `dotnet build` passes for IdentityService.Api
- [ ] `dotnet test` (when tests exist — Fix 16)
- [ ] Login flow: cookies set, no tokens in body
- [ ] MFA enroll → secret encrypted in DB
- [ ] Token refresh via cookie session works
- [ ] Logout clears all sessions, revokes tokens
- [ ] External login redirect only whitelisted URLs
- [ ] Audit events: validated, queued durably, loss metric exposed
- [ ] Rate limiting: 429 returned on MFA/SCIM abuse
- [ ] Cookie Domain matches environment
- [ ] Health check reports Vault status correctly
- [ ] Production startup fails without: signing key, Vault, HTTPS issuer

---

**Next step**: Begin with Fix 1 (cookie domain), Fix 2 (open redirect), Fix 3 (internal/refresh auth) — all Day 1 items. Ready when you are.
