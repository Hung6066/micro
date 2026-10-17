# Manufacturing Swarm Upgrade Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Provide a dedicated Docker Swarm deployment for Manufacturing with safe API scaling, bounded worker ownership, external stateful dependencies, and complete validation evidence.

**Architecture:** Swarm owns Identity, Commerce, Content, Manufacturing HTTP API, and one bounded Manufacturing worker. PostgreSQL, Redis, and RabbitMQ remain external stateful dependencies. The API replicas do not run duplicate consumers, outbox dispatch, or lifecycle automation; the worker owns those functions.

**Tech Stack:** Docker Swarm, Docker Secrets, Docker Configs, overlay networking, .NET 8, PowerShell validation, existing Manufacturing health and authorization contracts.

**Spec:** `docs/operations/manufacturing-swarm-deployment.vi.md`

## Global Constraints

- Do not modify unrelated dirty-worktree files.
- Do not put real credentials, private keys, or connection strings in the repository.
- Production application images must be immutable registry digests.
- State durability, backup, and replication are external prerequisites, not implied by Swarm replicas.
- Report static, config, live, smoke, and authenticated gates separately.

### Task 1: Swarm stack and secret boundary

**Files:**
- Create: `docker/swarm/manufacturing-stack.yml`
- Create: `docker/swarm/swarm-entrypoint.sh`
- Create: `docker/swarm/manufacturing.env.example`

- [x] Define a stack without local builds or fixed container names.
- [x] Use external endpoints for stateful dependencies.
- [x] Mount credentials through Docker Secrets and derive service connection strings at process start.
- [x] Separate HTTP API replicas from the single bounded worker.
- [x] Add readiness probes, resource limits, rolling rollback, restart policy, and overlay network.

### Task 2: Validation and operations contract

**Files:**
- Create: `scripts/validate-manufacturing-swarm.ps1`
- Create: `docs/operations/manufacturing-swarm-deployment.vi.md`

- [x] Validate topology, immutable images, HTTPS issuer, secrets, rollback policy, and readiness probes.
- [x] Run static validation with an operator-shaped env file; real production values remain external.
- [x] Run Swarm config validation and deployment on the available live Swarm.
- [x] Run API health, authorization, scale, restart, and worker-consumer smoke checks.

### Task 3: Completion audit

- [x] Record every gate as pass, fail, skipped, unavailable, or environment-blocked.
- [x] Confirm this task only added/changed the scoped Swarm, validator, and runbook files; pre-existing dirty files were preserved.
- [x] Only claim completion after runtime evidence covers the requested Swarm deployment.
