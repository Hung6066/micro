# ADR 006: Permission-Based RBAC (Not Just Role-Based)

**Status**: Accepted

**Date**: 2026-07-16

**Context**: Hospital roles are coarse-grained but access control must be fine-grained (e.g., a Provider can view patients but should not necessarily void invoices). Flat role-based auth is insufficient for HIPAA.

**Decision**: 49 granular permissions across 8 modules, assignable to 7 predefined roles. JWT carries both "permissions" (array) and "role" claims. `[HasPermission("code")]` attribute on REST endpoints, `[Authorize]` on gRPC.

**Consequences**: Permission data must be seeded (migration 013). Token size increases slightly (~500 bytes for permissions array). RBAC UI needed for admin management.
