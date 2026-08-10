# ADR 009: Database Row-Level Security via Views

**Status**: Accepted

**Date**: 2026-07-16

**Context**: CockroachDB doesn't support full PostgreSQL RLS (CREATE POLICY). Need multi-tenant data isolation at database level.

**Decision**: 16 security views using `current_setting('app.current_user_id')` session variables. Application sets session context on connection. EF Core queries map to views via `.ToView("patients_visible")`.

**Consequences**: Cannot use raw SQL without view prefix. Session variable must be set per-connection (handled in DbContext). View-based isolation is less performant than native RLS.
