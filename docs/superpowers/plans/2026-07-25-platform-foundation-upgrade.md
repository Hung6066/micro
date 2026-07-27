# His.Hope Platform Foundation Upgrade Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development or superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Make the shared frontend foundation and backend Core consumable as versioned platform contracts, then enforce those contracts through UI, API, accessibility, compatibility and supply-chain gates.

**Architecture:** The frontend foundation is published as a build artifact with one public TypeScript and Sass entrypoint. `His.Hope.Core` owns transport-neutral query, pagination, error, concurrency and API metadata contracts; REST and gRPC adapters translate at the edge. Feature apps keep domain behavior but consume shared tokens, i18n and interaction primitives.

**Tech Stack:** Angular 21, TypeScript 5.9, Sass, Storybook, Playwright, axe-core, ASP.NET Core 8, EF Core, gRPC/Protobuf, OpenAPI, GitHub Actions, SBOM/SCA/container scanning.

## Global Constraints

- Preserve existing API behavior while introducing additive contracts and adapters.
- Do not expose patient or identity data in logs, bulk-job events or frontend telemetry.
- Keep `DESIGN.md` as the visual source of truth.
- Apps must consume `@his-hope/frontend-foundation`, not raw shared source paths.
- Production CI must fail on breaking OpenAPI changes, serious/critical axe violations, dependency vulnerabilities above policy, missing SBOM or unsigned images.

## Tasks

1. Build and publish the frontend foundation artifact, update workspace consumers and Docker build contexts.
2. Add Core query, pagination, error, concurrency and API metadata contracts.
3. Add REST and gRPC adapters with bounded, allow-listed query handling.
4. Replace feature hard-coded tokens with semantic tokens and synchronize locale/theme across tabs and apps.
5. Extend DataTable contracts for cursor pagination, virtualized rendering, conflicts and asynchronous bulk jobs.
6. Add authenticated axe/keyboard/visual CI and backend contract checks.
7. Add OpenAPI diff, SBOM, SCA and container scanning gates.
8. Run package, frontend, backend and contract verification, then document remaining external-environment gates.
