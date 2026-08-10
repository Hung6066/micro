# Enterprise K3s Ansible Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build an auditable Ansible foundation that bootstraps the three supplied Linux hosts into a hardened, embedded-etcd K3s HA control plane sequentially.

**Architecture:** Keep this production automation in `ansible/enterprise-k3s` so it cannot be mistaken for the legacy staging single-master role. A static inventory maps the three supplied IPs to server roles; common hardening runs first, the server play runs with `serial: 1`, and live verification runs only after all three members have joined.

**Tech Stack:** Ansible built-in modules, K3s v1.35.5+k3s1, systemd, YAML/Jinja2, K3s embedded etcd.

## Global Constraints

- The playbooks do not deploy His.Hope workloads, migrate data, or change the existing k3d cluster.
- Every K3s server receives the same security-critical K3s settings: encrypted Secrets, PSA, API audit, NodeRestriction, EventRateLimit, private kubeconfig mode, and disabled packaged Traefik/ServiceLB.
- A valid external API registration endpoint and a Vault-encrypted `k3s_token` are required; no token default is permitted.
- Firewall mutation is opt-in so an unverified rule cannot cut off SSH access.
- Server installation is sequential; no control-plane server joins in parallel.

---

### Task 1: Add the production inventory and immutable configuration contract

**Files:**
- Create: `ansible/enterprise-k3s/ansible.cfg`
- Create: `ansible/enterprise-k3s/inventory/production.yml`
- Create: `ansible/enterprise-k3s/group_vars/all.yml`
- Create: `ansible/enterprise-k3s/group_vars/vault.yml.example`
- Create: `ansible/enterprise-k3s/README.md`

**Interfaces:**
- Consumes: user-supplied SSH access to `172.16.102.7`, `172.16.102.8`, and `172.16.102.9`.
- Produces: `k3s_servers` group ordered as server-1, server-2, server-3 and required variables consumed by every later play.

- [ ] **Step 1: Define the expected syntax gate**

Run: `ansible-playbook -i ansible/enterprise-k3s/inventory/production.yml ansible/enterprise-k3s/playbooks/10-bootstrap-k3s.yml --syntax-check`

Expected: FAIL because the production inventory and bootstrap playbook do not yet exist.

- [ ] **Step 2: Add static inventory and secure variable contract**

Map `k3s-server-1`, `k3s-server-2`, and `k3s-server-3` to the three IPs. Require the operator to supply `k3s_api_registration_endpoint`, an encrypted `k3s_token`, and the install-script checksum; do not embed credentials.

- [ ] **Step 3: Run the syntax gate**

Run: `ansible-playbook -i ansible/enterprise-k3s/inventory/production.yml ansible/enterprise-k3s/playbooks/10-bootstrap-k3s.yml --syntax-check`

Expected: it reaches the next missing dependency instead of rejecting inventory syntax.

### Task 2: Add host preflight and hardening roles

**Files:**
- Create: `ansible/enterprise-k3s/roles/preflight/tasks/main.yml`
- Create: `ansible/enterprise-k3s/roles/os_hardening/tasks/main.yml`
- Create: `ansible/enterprise-k3s/roles/os_hardening/templates/90-kubelet.conf.j2`
- Create: `ansible/enterprise-k3s/playbooks/00-preflight.yml`

**Interfaces:**
- Consumes: `k3s_servers`, disk and OS facts, `enterprise_manage_firewall` from `group_vars/all.yml`.
- Produces: hosts that meet explicit OS, hostname, swap, clock, kernel and storage checks before K3s installation.

- [ ] **Step 1: Create the preflight play with required role names**

Run: `ansible-playbook -i ansible/enterprise-k3s/inventory/production.yml ansible/enterprise-k3s/playbooks/00-preflight.yml --syntax-check`

Expected: FAIL until both referenced roles exist.

- [ ] **Step 2: Implement non-destructive validation and idempotent kernel hardening**

Assert a supported Linux family, unique hostname, no active swap, required free disk, configured NTP, valid registration endpoint, and no default/example token. Write the K3s kernel sysctl profile and apply it. Keep firewall management disabled unless explicitly enabled.

- [ ] **Step 3: Verify check mode**

Run: `ansible-playbook -i ansible/enterprise-k3s/inventory/production.yml ansible/enterprise-k3s/playbooks/00-preflight.yml --syntax-check`

Expected: PASS.

### Task 3: Add hardened K3s server bootstrap

**Files:**
- Create: `ansible/enterprise-k3s/roles/k3s_server/tasks/main.yml`
- Create: `ansible/enterprise-k3s/roles/k3s_server/handlers/main.yml`
- Create: `ansible/enterprise-k3s/roles/k3s_server/templates/config.yaml.j2`
- Create: `ansible/enterprise-k3s/roles/k3s_server/templates/psa.yaml.j2`
- Create: `ansible/enterprise-k3s/roles/k3s_server/templates/audit-policy.yaml.j2`
- Create: `ansible/enterprise-k3s/playbooks/10-bootstrap-k3s.yml`

**Interfaces:**
- Consumes: preflight-compliant hosts and secure inventory variables.
- Produces: a three-member embedded-etcd K3s control plane, configured through `/etc/rancher/k3s/config.yaml`.

- [ ] **Step 1: Add bootstrap play with `serial: 1`**

Run: `ansible-playbook -i ansible/enterprise-k3s/inventory/production.yml ansible/enterprise-k3s/playbooks/10-bootstrap-k3s.yml --syntax-check`

Expected: FAIL until the `k3s_server` role and its templates exist.

- [ ] **Step 2: Implement server configuration and installer**

Use the version-pinned installer only after the downloaded script checksum matches. Bootstrap server 1 with `cluster-init`; direct servers 2 and 3 to the registration endpoint. Write the audit and PSA configuration before starting K3s and wait for each server readiness before Ansible advances to the next serial host.

- [ ] **Step 3: Verify syntax and dry-run rendering**

Run: `ansible-playbook -i ansible/enterprise-k3s/inventory/production.yml ansible/enterprise-k3s/playbooks/10-bootstrap-k3s.yml --syntax-check`

Expected: PASS.

### Task 4: Add live cluster verification and safe operating instructions

**Files:**
- Create: `ansible/enterprise-k3s/roles/verify_cluster/tasks/main.yml`
- Create: `ansible/enterprise-k3s/playbooks/20-verify-cluster.yml`
- Modify: `ansible/enterprise-k3s/README.md`

**Interfaces:**
- Consumes: primary server K3s kubeconfig and three joined server nodes.
- Produces: fail-closed verification for node count/readiness, secrets encryption, audit file, PSA config, and API readiness.

- [ ] **Step 1: Add verification play contract**

Run: `ansible-playbook -i ansible/enterprise-k3s/inventory/production.yml ansible/enterprise-k3s/playbooks/20-verify-cluster.yml --syntax-check`

Expected: FAIL until the `verify_cluster` role exists.

- [ ] **Step 2: Implement fail-closed live checks**

Delegate cluster checks to `k3s-server-1`; assert exactly three Ready nodes, enabled Secret encryption, audit artifacts and the PSA policy configuration. Do not change cluster state in this play.

- [ ] **Step 3: Run all static verification**

Run: `ansible-playbook -i ansible/enterprise-k3s/inventory/production.yml ansible/enterprise-k3s/playbooks/00-preflight.yml --syntax-check`

Run: `ansible-playbook -i ansible/enterprise-k3s/inventory/production.yml ansible/enterprise-k3s/playbooks/10-bootstrap-k3s.yml --syntax-check`

Run: `ansible-playbook -i ansible/enterprise-k3s/inventory/production.yml ansible/enterprise-k3s/playbooks/20-verify-cluster.yml --syntax-check`

Expected: all PASS; live verification remains intentionally not run until the operator supplies the real registration endpoint, encrypted secrets and explicit authority to modify the servers.
