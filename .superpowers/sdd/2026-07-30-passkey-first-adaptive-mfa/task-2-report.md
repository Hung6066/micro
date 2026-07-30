# Task 2 report

## Fix summary

- Restored `/api/v1/auth/mfa/verify` fail-closed behavior for unauthenticated/no-pending requests: no pending context now returns 401 before legacy token dependencies are resolved.
- Preserved the legacy authenticated MFA verification path only when no pending OIDC MFA cookie is present; an invalid/unresolvable `hishop_oidc_mfa` cookie returns 401 and does not mint or rotate a `hishop_sid` session.
- Bound browser passkey MFA options and completion state to the live pending session using `userId + PendingId + hishop_sid`, instead of the previous user-only Redis keys.
- Added regressions proving concurrent pending sessions for the same user get distinct browser passkey challenges and session A cannot complete with session B's challenge.
- Left native mobile MFA ticket binding intact: native state remains bound to user, pending ID, and browser session ID.

## Changed files

- `src/Services/IdentityService/IdentityService.Api/Endpoints/MfaEndpoints.cs`
- `src/Services/IdentityService/IdentityService.Api/Endpoints/PasskeyEndpoints.cs`
- `tests/IdentityService/IdentityService.IntegrationTests/AdaptiveMfaEndpointTests.cs`
- `tests/IdentityService/IdentityService.IntegrationTests/MfaEndpointsTests.cs`
- `.superpowers/sdd/2026-07-30-passkey-first-adaptive-mfa/task-2-report.md`

## Red verification

```powershell
rtk dotnet test tests\IdentityService\IdentityService.IntegrationTests\IdentityService.IntegrationTests.csproj --filter FullyQualifiedName~AdaptiveMfaEndpointTests
```

```text
fail dotnet test: 7 passed, 2 failed, 0 skipped, 0 warnings in 1 projects (50.9 s)
```

The new red failures showed the user-only browser MFA challenge binding: same-user concurrent pending sessions produced no session-scoped challenge keys, and a session without its own challenge reached WebAuthn validation from another session's challenge instead of returning 401.

```powershell
rtk dotnet test tests\IdentityService\IdentityService.IntegrationTests\IdentityService.IntegrationTests.csproj --filter FullyQualifiedName~MfaEndpointsTests
```

```text
fail dotnet test: 7 passed, 2 failed, 0 skipped, 2 warnings in 1 projects (48.9 s)
```

The verify red failures showed `/api/v1/auth/mfa/verify` returning 500 before it could answer 401 for unauthenticated/no-pending requests because legacy token dependencies were resolved too early.

## Final requested verification

```powershell
rtk dotnet test tests\IdentityService\IdentityService.IntegrationTests\IdentityService.IntegrationTests.csproj --filter FullyQualifiedName~AdaptiveMfaEndpointTests
```

```text
ok dotnet test: 9 tests passed, 333 warnings in 1 projects (40.8 s)
```

```powershell
rtk dotnet test tests\IdentityService\IdentityService.IntegrationTests\IdentityService.IntegrationTests.csproj --filter FullyQualifiedName~AdaptiveMfaMethodTests
```

```text
ok dotnet test: 13 tests passed, 0 warnings in 1 projects (9.4 s)
```

```powershell
rtk dotnet test tests\IdentityService\IdentityService.IntegrationTests\IdentityService.IntegrationTests.csproj --filter FullyQualifiedName~MfaEndpointsTests
```

```text
ok dotnet test: 9 tests passed, 2 warnings in 1 projects (39.5 s)
```

```powershell
rtk dotnet test tests\IdentityService\IdentityService.IntegrationTests\IdentityService.IntegrationTests.csproj --filter FullyQualifiedName~RateLimitingTests
```

```text
ok dotnet test: 3 tests passed, 0 warnings in 1 projects (60.0 s)
```

## Final combined verification

```powershell
rtk dotnet test tests\IdentityService\IdentityService.IntegrationTests\IdentityService.IntegrationTests.csproj --filter "FullyQualifiedName~AdaptiveMfaEndpointTests|FullyQualifiedName~AdaptiveMfaMethodTests|FullyQualifiedName~MfaEndpointsTests|FullyQualifiedName~RateLimitingTests"
```

```text
ok dotnet test: 34 tests passed, 0 warnings in 1 projects (48.7 s)
```

## Concerns

- Verification was scoped to the requested IdentityService backend suites, not the full repository.
- `AdaptiveMfaEndpointTests` still emits the existing 333-warning baseline in this worktree; this fix did not expand warning cleanup outside Task 2 scope.
