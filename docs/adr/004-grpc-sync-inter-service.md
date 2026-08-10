# ADR 004: gRPC for Synchronous Inter-Service Communication

**Status**: Accepted

**Date**: 2026-07-16

**Context**: Services need to query each other for data (e.g., ClinicalService needs patient demographics).

**Decision**: gRPC with Protocol Buffers for type-safe, high-performance sync calls. REST reserved for frontend API.

**Consequences**: Requires proto compilation step in CI/CD. Debugging requires gRPC tools. All proto files at `src/Shared/Protos/`.
