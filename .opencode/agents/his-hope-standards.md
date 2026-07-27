# His.Hope Engineering Standards

This is the mandatory implementation contract for every new feature. The backend and frontend agents MUST read this file before editing code. These rules are enforced by review and by the repository quality gates; they are not optional suggestions.

## 1. Start-of-task contract

Before coding:

1. Read `AGENTS.md`, `DESIGN.md`, the relevant service/app README, and the existing implementation path.
2. Classify the change as API, persistence, messaging, auth, UI, or cross-cutting.
3. Identify the shared contract/package that must be reused. Do not create a local duplicate until the shared boundary has been checked.
4. State the API, data, authorization, i18n, accessibility, responsive, observability, and test acceptance criteria.

## 2. Backend rules

- Keep Clean Architecture boundaries: Domain has no ASP.NET/EF/transport dependency; Application owns use cases and validators; Infrastructure owns external adapters; Api owns transport mapping only.
- Use shared platform packages for cross-service behavior: `His.Hope.Contracts`, `His.Hope.AspNetCore`, `His.Hope.Authorization`, `His.Hope.Validation`, `His.Hope.Resilience`, `His.Hope.Messaging`, `His.Hope.Observability`, and `His.Hope.Persistence`.
- `His.Hope.Core` contains only stable primitives/domain abstractions. Do not put HTTP, JWT, EF, RabbitMQ, Redis, OpenTelemetry exporters, or feature DTOs there.
- REST endpoints use `/api/v1`, shared `PagedResult<T>`/query contracts, shared ProblemDetails with stable `errorCode` and `correlationId`, and explicit 400/401/403/404/409/429 behavior.
- Every command/query has FluentValidation in the shared validation pipeline. Do not rely on controller/minimal-handler ad hoc checks.
- Every mutation checks authorization, audit policy, idempotency/concurrency requirements, and emits the required durable event/outbox record.
- Every new HTTP/gRPC client uses the shared resilience policy. No local retry loops, infinite polling, or permissive fallback.
- Durable production adapters are required for messaging, audit, sessions, and jobs. In-memory adapters are test-only and must be explicit.
- Database migrations are backward compatible, have an upgrade/rollback note, and are covered by integration tests when persistence behavior changes.
- Never commit secrets, `:latest` production images, permissive CORS, unauthenticated business endpoints, or development signing keys.

## 3. Frontend rules

- Use `@his-hope/frontend-foundation` public exports. Do not import shared source files by relative path and do not duplicate foundation components in an app.
- Use `HisHopeThemeService`, shared design tokens, `HisHopeI18nService`, `HisHopePermissionService`, shared state components, notification services, and shared focus/accessibility contracts.
- New pages use shared page layout/header/toolbar, loading/empty/error/offline/forbidden states, typed reactive forms, and permission-aware sensitive actions.
- New tables use the shared DataTable contract: typed columns, server query state, URL synchronization where applicable, sorting/filtering/pagination, responsive mobile item mode, selection/bulk permissions, export masking, and detail templates.
- No hard-coded colors, font families/sizes/weights, spacing, or arbitrary z-index values in feature styles. Add a token to the foundation first when a value is genuinely reusable.
- All visible text goes through i18n keys with Vietnamese and English fallback coverage. Use locale-aware date/number/currency formatting.
- All icon-only actions have accessible labels. Keyboard navigation, focus-visible state, focus trap/restore, Escape handling, semantic live regions, and WCAG 2.2 AA contrast are required.
- Responsive behavior is validated at mobile, tablet, desktop, and wide desktop. Avoid layout shifts and hidden/overflowing menus.
- Use the shared OIDC/auth coordinator and interceptors. Never store or manually parse tokens in feature components.

## 4. Required verification

Backend changes MUST run the applicable build, tests, API convention gate, contract tests, and security checks. Frontend changes MUST run:

```powershell
npm run validate:foundation
npm run lint:design-tokens
npm run build:shared
npm --workspace <affected-app> run build
```

For user-facing changes also run the relevant unit/interaction tests and authenticated axe/keyboard/visual regression checks. A failed or unavailable gate must be reported as failed or unverified, never silently skipped.

## 5. Definition of done

A feature is complete only when implementation, shared contract, authorization, error states, i18n, theme/tokens, observability/audit, tests, docs/changelog, and build evidence are aligned. If a requirement crosses backend and frontend, both agents must agree on the contract before either one invents a local shape.
