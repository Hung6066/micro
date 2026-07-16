# ADR 002: CockroachDB for Global Multi-Region

**Status**: Accepted

**Date**: 2026-07-16

**Context**: Need multi-region active-active with strong consistency for PHI data, horizontal scalability for 1B+ patients, PostgreSQL compatibility.

**Decision**: CockroachDB 24.1 with 5-replica global tables across 3 regions.

**Consequences**: PostgreSQL-isms may not fully work (no full RLS, limited triggers — mitigated by security views and app-level audit). Higher operational cost than single-region Postgres.
