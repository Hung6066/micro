# Linkerd Control-Plane HA Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Run Linkerd destination, identity, and proxy-injector as topology-spread HA workloads and validate failover before re-enabling Traefik injection.

**Architecture:** Patch the installed Linkerd control-plane deployments at runtime through an idempotent PowerShell script, apply repository-managed PodDisruptionBudgets, and validate readiness, endpoint continuity, node spread, and single-pod deletion. Keep the Traefik edge outside injection until all gates pass, then restore injection and revalidate Harbor.

**Tech Stack:** K3s, kubectl, Linkerd control-plane deployments, Kubernetes PDB/topology spread, PowerShell.

## Global Constraints

- Do not delete Linkerd identity issuer or trust-root secrets.
- Do not change Harbor TLS, registry credentials, or Vault secrets.
- Do not re-enable Traefik injection before control-plane and Harbor failover gates pass.
- Do not claim Linkerd CLI validation when the Linkerd CLI is unavailable.

---

### Task 1: Validate CNI placement prerequisites

**Files:**
- Create: `scripts/configure-linkerd-ha-k3s.ps1`
- Test: live K3s control-plane pod readiness and node placement

- [x] Confirm the three control-plane deployments and all three K3s nodes are present.
- [x] Create topology-spread/PDB patches and an idempotent apply path.
- [x] Validate every replica has a Ready condition before edge reintegration.

### Task 2: Failover validation

**Files:**
- Modify: `docs/architecture/production-gates.md`
- Modify: `docs/superpowers/plans/2026-08-04-harbor-k3s.md`

- [x] Verify each control-plane Service has at least two endpoints after HA rollout.
- [x] Delete one replica per deployment sequentially and verify recovery without endpoint loss.
- [x] Re-enable Traefik injection only after all control-plane checks pass.
- [x] Validate Harbor HTTPS and image pull after Traefik reintegration.
