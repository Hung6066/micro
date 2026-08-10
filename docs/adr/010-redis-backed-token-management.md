# ADR 010: Redis-Backed Token Management (Not In-Memory)

**Status**: Accepted

**Date**: 2026-07-16

**Context**: Refresh tokens were stored in ConcurrentDictionary (in-memory), lost on restart, not shared across replicas. Token blacklisting was missing.

**Decision**: Redis for both refresh token store (family tracking, theft detection, rotation) and token blacklist (JWT jti revocation with TTL). All replicas share the same Redis.

**Consequences**: Redis becomes critical infrastructure (must be HA). Token operations have Redis latency (~1ms). Eviction policies must be configured (volatile-lru).
