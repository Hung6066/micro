# Legacy Auth Deprecation Timeline

## Status

Legacy browser endpoints under `/api/v1/auth/*` remain available for backward compatibility but are **deprecated**.

| Milestone | Date | Action |
|-----------|------|--------|
| Deprecation headers | 2026-08-22 | All legacy routes emit `Deprecation`, `Sunset`, and `Link` successor headers |
| OIDC migration window | 2026-08-22 → 2027-12-31 | Clients must move to `/connect/authorize`, `/connect/token`, and BFF `/api/v1/auth/session/exchange` |
| Sunset | 2028-01-01 | Legacy `/api/v1/auth/login`, `/refresh`, `/internal/refresh` removed (configurable via `LEGACY_AUTH_SUNSET`) |

## Replacement mapping

| Legacy | Successor |
|--------|-----------|
| `POST /api/v1/auth/login` | OIDC authorization code + PKCE |
| `POST /api/v1/auth/refresh` | `/connect/token` refresh grant or BFF session refresh |
| `POST /api/v1/auth/internal/refresh` | BFF session refresh with CSRF + authenticated principal |
| `GET /api/v1/auth/me` | OIDC userinfo or admin `/api/v1/admin/me` |

## Enforcement

- Runtime sunset date: `Authentication:LegacyAuthSunset` or `LEGACY_AUTH_SUNSET`
- CI gate: `LegacyEndpoints_HaveDeprecationHeaders` integration test
- Removal requires major release note + 90-day operator notice
