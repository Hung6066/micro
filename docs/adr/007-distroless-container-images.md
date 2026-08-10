# ADR 007: Distroless Container Images

**Status**: Accepted

**Date**: 2026-07-16

**Context**: Alpine-based images have shell (`/bin/sh`) and package manager (`apk`), creating attack surface. CVE scanners flag Alpine packages.

**Decision**: `mcr.microsoft.com/dotnet/aspnet:8.0-noble-chiseled` for all .NET services. Zero shell, zero package manager, FIPS-compliant. curl added via multi-stage copy for healthchecks only.

**Consequences**: Cannot `docker exec` into container for debugging (use ephemeral debug containers). Healthchecks must use HTTP probes or the copied curl binary.
