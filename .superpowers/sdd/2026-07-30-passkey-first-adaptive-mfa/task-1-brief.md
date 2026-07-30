# Task 1: Lock the pending-session contract with failing backend tests

Work only in `D:/AI/micro-worktrees/passkey-first-adaptive-mfa`. Do not edit the main checkout or unrelated files. You are not alone in the repository; preserve existing changes and do not reset/revert other work.

## Files

- Create `tests/IdentityService/IdentityService.IntegrationTests/AdaptiveMfaMethodTests.cs`.
- Inspect/modify `src/Services/IdentityService/IdentityService.Api/Services/OidcLoginCompletionService.cs`.
- Inspect `src/Services/IdentityService/IdentityService.Api/Endpoints/PasskeyEndpoints.cs`.

## Goal and interfaces

Produce `AdaptiveMfaMethods` with `PreferredMethod`, `AvailableMethods`, `IsUnfamiliarDevice`, and server-derived `UserId`/`ReturnUrl` accessors for the pending context.

Produce `TryGetPendingMfaContext(HttpContext)` returning a nullable context containing pending user ID, original return URL, and device classification.

The method policy must use exactly this ordering:

```csharp
var available = new List<string>();
if (hasPasskey) available.Add("passkey");
if (hasMobileApproval) available.Add("mobileApproval");
if (hasTotp) available.Add("totp");
var preferred = unfamiliarDevice && hasMobileApproval
    ? "mobileApproval"
    : hasPasskey ? "passkey"
    : hasMobileApproval ? "mobileApproval"
    : hasTotp ? "totp" : null;
```

Bind pending context to the existing server session. Reject missing or mismatched context with `401` or `409`; never accept a client-provided user ID as authority.

## Required failing tests

```csharp
[Fact]
public void Recognized_device_with_passkey_prefers_passkey()
{
    var result = AdaptiveMfaMethodPolicy.Resolve(
        hasPasskey: true, hasMobileApproval: true, hasTotp: true, unfamiliarDevice: false);

    result.PreferredMethod.Should().Be("passkey");
    result.AvailableMethods.Should().BeEquivalentTo("passkey", "mobileApproval", "totp");
}

[Fact]
public void Unfamiliar_device_prefers_mobile_approval()
{
    var result = AdaptiveMfaMethodPolicy.Resolve(
        hasPasskey: true, hasMobileApproval: true, hasTotp: true, unfamiliarDevice: true);

    result.PreferredMethod.Should().Be("mobileApproval");
}

[Fact]
public void Totp_is_available_only_when_enrolled()
{
    var result = AdaptiveMfaMethodPolicy.Resolve(
        hasPasskey: false, hasMobileApproval: false, hasTotp: false, unfamiliarDevice: false);

    result.AvailableMethods.Should().BeEmpty();
}
```

## Verification

Run:

```powershell
dotnet test tests/IdentityService/IdentityService.IntegrationTests/IdentityService.IntegrationTests.csproj --filter FullyQualifiedName~AdaptiveMfaMethodTests
```

The focused tests must pass. Add any needed unit-test project reference only if the existing integration-test project cannot access the policy without it. Do not implement Task 2 endpoint work yet.

## Reporting

Commit the implementation with:

```powershell
git add tests/IdentityService src/Services/IdentityService
git commit -m "feat: define adaptive MFA method policy"
```

Write a report to `.superpowers/sdd/2026-07-30-passkey-first-adaptive-mfa/task-1-report.md` containing changed files, commit hash, exact test command/output, and concerns. Return only status, commit, one-line test summary, and concerns.
