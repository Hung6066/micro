# K3s Production Deployment Orchestrator

## Status

Draft approved for implementation planning after user selection of the safe automatic mode. This document is a design gate; it does not itself deploy infrastructure.

## Problem and goal

The current production runbook requires manually invoking multiple Ansible playbooks and manually inspecting systemd journals when K3s readiness fails. The goal is one repeatable entry point that executes the existing production workflow in order, stops safely on an unknown failure, and emits actionable evidence without exposing Vault, Azure, or K3s secrets.

## Scope

The orchestrator covers the existing production path:

1. K3s preflight and OS hardening on all five nodes.
2. External HAProxy/Keepalived validation and K3s API VIP readiness.
3. Embedded-etcd control-plane bootstrap, one server at a time.
4. Control-plane quorum and API verification.
5. Worker bootstrap, one worker at a time.
6. Vault deployment with Azure Key Vault auto-unseal.
7. CNPG, K3s, Redis, and Vault backup configuration to Azure Blob Storage.
8. Production application overlay deployment and smoke checks.
9. Backup/restore verification and a phase-by-phase report.

The orchestrator reuses the existing Ansible roles, manifests, and scripts. It does not replace the application deployment manifests, rotate credentials, alter Azure permissions, or bypass a failed gate.

## Recommended architecture

Use a single Ansible workflow as the source of truth, with a thin Windows/WSL PowerShell entry point. The workflow is composed of explicit phase includes rather than shelling out to unrelated commands. Each phase has a stable name, bounded retries, and a registered result. The entry point selects the production inventory and prompts for the Ansible Vault and sudo passwords without accepting secrets as command-line arguments.

The workflow runs serially where cluster membership changes (`serial: 1`) and can be safely re-run. Existing external LB nodes remain outside the K3s inventory; the K3s API endpoint remains `172.16.102.100:6443`.

## Phase contract

Each phase returns one of `PASS`, `FAIL`, or `BLOCKED` and writes evidence under a local run directory that excludes secret values.

| Phase | Action | Success evidence |
|---|---|---|
| `preflight` | Inventory, OS, swap, NTP, capacity, ACL gate | All five nodes satisfy assertions |
| `lb` | Check HAProxy/Keepalived and VIP | Both services active; VIP failover check passes |
| `control-plane` | Bootstrap servers serially | Each server service active and API `/readyz` succeeds |
| `verify-control-plane` | Check etcd/quorum/node TLS | Expected control-plane count and healthy API |
| `workers` | Bootstrap workers serially | All workers register Ready with labels |
| `vault` | Apply Azure auto-unseal configuration | Vault initialized/sealed state and unseal mechanism verified |
| `backup` | Apply Azure object-store and backup agents | Schedules/objects present and a backup object is uploaded |
| `application` | Apply production Kustomize overlay | Required namespaces/workloads become Ready |
| `restore` | Restore a representative backup in an isolated target | Data and integrity checks pass |

## Failure handling and diagnostics

Known safe remediation is limited to idempotent operations already owned by the roles: disabling active swap and fstab persistence, creating required directories, validating configuration, restarting a failed service once, and retrying readiness within a bounded timeout.

If K3s readiness fails, the workflow must collect (without printing secrets):

- `systemctl status k3s --no-pager -l`
- `systemctl show k3s` exit and state fields
- the last bounded lines of `journalctl -u k3s`
- installer version/checksum and API endpoint metadata

It must not print `/etc/rancher/k3s/config.yaml`, Vault files, SAS tokens, client secrets, kubeconfigs, or raw Kubernetes Secrets. Unknown failures stop the workflow and mark the phase `BLOCKED` until reviewed.

## Secret and Azure handling

Ansible Vault remains the only source for K3s token, Keepalived password, and backup SAS token. Azure client secret remains file-backed under `D:\secure\his-hope`; it is read by the existing bootstrap script and never passed as a process argument. The orchestrator validates Azure Key Vault key availability and Blob container access before applying Kubernetes objects. It does not weaken Key Vault RBAC or use management-plane access as a substitute for data-plane key access.

## Idempotency and resume

Each phase has a completion marker containing timestamp, inventory, playbook revision, and redacted evidence summary. A rerun skips only phases with a verified completion marker and rechecks their health; it never skips a failed phase. `--from-phase` and `--to-phase` are allowed only for an operator-requested recovery run and still execute prerequisite assertions.

## Verification and acceptance criteria

The implementation is accepted only when:

- one documented command starts the workflow from Windows/WSL;
- a clean run executes all phases in order and prompts for secrets interactively;
- a deliberately stopped K3s service produces a redacted journal artifact and a `FAIL` result without leaking secrets;
- rerunning after recovery resumes safely and does not reinstall healthy nodes;
- the production API is reachable through VIP `172.16.102.100:6443`;
- Azure Key Vault auto-unseal, Azure Blob upload, application readiness, and an isolated restore are all verified at runtime;
- a final report distinguishes `PASS`, `FAIL`, `BLOCKED`, and `NOT RUN` for every phase.

## Non-goals

This design does not create a new CI/CD platform, alter the application architecture, provision Azure resources, change network ACLs, or automatically override security gates. Those remain explicit operator responsibilities.
