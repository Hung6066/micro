# K3s Production Deployment Orchestrator Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Provide one safe Windows/WSL entry point that runs the production K3s, Azure Key Vault, backup, application, and restore workflow with bounded retries and redacted evidence.

**Architecture:** Keep the existing Ansible roles and production manifests as the source of truth. Add a diagnostic role for K3s readiness, a single Ansible orchestration playbook for infrastructure phases, and a PowerShell entry point that prompts once for Vault/sudo credentials and writes a phase report without placing secrets on the command line.

**Tech Stack:** Ansible Core, Ubuntu systemd/K3s v1.35.5+k3s1, PowerShell 7, WSL, Kustomize, Azure Key Vault, Azure Blob Storage, HashiCorp Vault, CloudNativePG.

## Global Constraints

- Use the existing external API endpoint `172.16.102.100:6443`; K3s nodes must not own the VIP.
- Run control-plane and worker membership changes with `serial: 1`.
- Keep `enterprise_network_controls_verified` fail-closed and never bypass it in the orchestrator.
- Keep K3s token, Keepalived password, and Azure SAS token in encrypted `ansible/enterprise-k3s/group_vars/vault.yml`.
- Never print `/etc/rancher/k3s/config.yaml`, kubeconfigs, Azure client secrets, SAS tokens, or Kubernetes Secret data.
- Retry readiness only within bounded timeouts; unknown failures stop the workflow.
- Preserve unrelated dirty worktree changes and commit only files belonging to the orchestrator task.

---

### Task 1: Add redacted K3s readiness diagnostics

**Files:**
- Modify: `ansible/enterprise-k3s/roles/k3s_server/tasks/main.yml`
- Create: `ansible/enterprise-k3s/roles/k3s_server/defaults/main.yml` additions if required
- Test: `ansible/enterprise-k3s/tests/test_k3s_server_tasks.ps1`

**Interfaces:**
- Consumes: `k3s_readyz`, `k3s_install_no_log`, and the `k3s` systemd unit.
- Produces: a failed Ansible task whose message contains service state and the last 80 journal lines, with no config-file content.

- [ ] **Step 1: Write the failing static test**

```powershell
$task = Get-Content 'ansible/enterprise-k3s/roles/k3s_server/tasks/main.yml' -Raw
if ($task -notmatch 'journalctl -u k3s') { throw 'Readiness failure must collect K3s journal.' }
if ($task -match 'cat /etc/rancher/k3s/config.yaml') { throw 'Diagnostic must not print K3s config.' }
```

- [ ] **Step 2: Run the test and confirm it fails**

Run: `pwsh -NoProfile -File ansible/enterprise-k3s/tests/test_k3s_server_tasks.ps1`

Expected: FAIL because the current readiness task has no journal collection.

- [ ] **Step 3: Implement a bounded rescue path**

Wrap the readiness command in `block`/`rescue`. In rescue, run:

```yaml
- ansible.builtin.command: systemctl show k3s -p ActiveState -p SubState -p ExecMainStatus
  register: k3s_state
  changed_when: false
  failed_when: false
- ansible.builtin.command: journalctl -u k3s -n 80 --no-pager -o cat
  register: k3s_journal
  changed_when: false
  failed_when: false
- ansible.builtin.fail:
    msg: >-
      K3s API readiness failed on {{ inventory_hostname }}.
      state={{ k3s_state.stdout | trim }}
      journal={{ k3s_journal.stdout | regex_replace('(?i)(token|sas|secret)[^\\n]*', '[REDACTED]') }}
```

Keep `no_log: true` on the configuration template and keep installer output hidden by default.

- [ ] **Step 4: Run the static test and YAML validation**

Run: `pwsh -NoProfile -File ansible/enterprise-k3s/tests/test_k3s_server_tasks.ps1` and `python -c "import yaml; yaml.safe_load(open('ansible/enterprise-k3s/roles/k3s_server/tasks/main.yml'))"`

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add ansible/enterprise-k3s/roles/k3s_server ansible/enterprise-k3s/tests/test_k3s_server_tasks.ps1
git commit -m "ops: add redacted k3s readiness diagnostics"
```

### Task 2: Create the single Ansible production workflow

**Files:**
- Create: `ansible/enterprise-k3s/playbooks/40-production-orchestrator.yml`
- Modify: `ansible/enterprise-k3s/playbooks/00-preflight.yml`
- Modify: `ansible/enterprise-k3s/playbooks/05-configure-external-lb.yml`
- Modify: `ansible/enterprise-k3s/playbooks/10-bootstrap-k3s.yml`
- Modify: `ansible/enterprise-k3s/playbooks/15-bootstrap-workers.yml`
- Modify: `ansible/enterprise-k3s/playbooks/20-verify-cluster.yml`
- Modify: `ansible/enterprise-k3s/playbooks/30-backup-agents.yml`
- Test: `ansible/enterprise-k3s/tests/test_orchestrator_structure.ps1`

**Interfaces:**
- Consumes: the existing production inventory, encrypted vars, and playbooks.
- Produces: deterministic phase order and one Ansible invocation that prompts for secrets once.

- [ ] **Step 1: Write the failing structure test**

```powershell
$p = Get-Content 'ansible/enterprise-k3s/playbooks/40-production-orchestrator.yml' -Raw -ErrorAction SilentlyContinue
if (-not $p) { throw 'Orchestrator playbook is missing.' }
foreach ($name in '00-preflight.yml','05-configure-external-lb.yml','10-bootstrap-k3s.yml','20-verify-cluster.yml','15-bootstrap-workers.yml','30-backup-agents.yml') {
  if ($p.IndexOf($name) -lt 0) { throw "Missing phase $name" }
}
```

- [ ] **Step 2: Run it and confirm it fails**

Run: `pwsh -NoProfile -File ansible/enterprise-k3s/tests/test_orchestrator_structure.ps1`

Expected: FAIL because the orchestration playbook does not exist.

- [ ] **Step 3: Implement static imports and phase gates**

Use `import_playbook` in this exact order:

```yaml
---
- import_playbook: 00-preflight.yml
- import_playbook: 05-configure-external-lb.yml
- import_playbook: 10-bootstrap-k3s.yml
- import_playbook: 20-verify-cluster.yml
- import_playbook: 15-bootstrap-workers.yml
- import_playbook: 30-backup-agents.yml
```

Keep `any_errors_fatal: true`, `serial: 1`, and explicit `vars_files` in the imported plays. Add an API readiness assertion after the LB phase and before the first control-plane join.

- [ ] **Step 4: Run syntax and structure validation**

Run: `wsl bash -lc 'cd /mnt/d/AI/micro/ansible/enterprise-k3s && ansible-playbook -i inventory/production.yml playbooks/40-production-orchestrator.yml --syntax-check --ask-vault-pass'`

Expected: syntax check PASS after the Vault password prompt.

- [ ] **Step 5: Commit**

```bash
git add ansible/enterprise-k3s/playbooks ansible/enterprise-k3s/tests/test_orchestrator_structure.ps1
git commit -m "ops: add ordered production ansible workflow"
```

### Task 3: Build the Windows/WSL one-command runner

**Files:**
- Create: `scripts/run-k3s-production.ps1`
- Create: `scripts/tests/run-k3s-production.Tests.ps1`
- Modify: `ansible/enterprise-k3s/README.md`

**Interfaces:**
- Consumes: `-Inventory`, optional `-FromPhase`, optional `-ToPhase`, interactive Vault/sudo prompts, and `azure-production.env` path.
- Produces: `artifacts/k3s-production/run-YYYYMMDD-HHmmss/summary.json`, `summary.md`, and redacted per-phase logs; exit code `0` only when every requested phase passes.

- [ ] **Step 1: Write the failing runner tests**

```powershell
Describe 'production runner contract' {
  It 'does not put secrets in command arguments' {
    $text = Get-Content 'scripts/run-k3s-production.ps1' -Raw
    $text | Should -Not -Match '--extra-vars.*password'
  }
  It 'writes a report directory' {
    (Get-Content 'scripts/run-k3s-production.ps1' -Raw) | Should -Match 'summary.json'
  }
}
```

- [ ] **Step 2: Run the tests and confirm they fail**

Run: `pwsh -NoProfile -Command "Invoke-Pester scripts/tests/run-k3s-production.Tests.ps1"`

Expected: FAIL because the runner and test file do not exist.

- [ ] **Step 3: Implement credential-safe phase execution**

The script must:

1. Validate WSL, `ansible-playbook`, inventory, encrypted Vault vars, and the Azure env file.
2. Read Vault and sudo passwords with `Read-Host -AsSecureString`; never pass either in the argument list.
3. Create a temporary Vault password file with restrictive ACL, delete it in `finally`, and set `ANSIBLE_BECOME_PASS` only for the child process.
4. Invoke `playbooks/40-production-orchestrator.yml` once, redirecting output to the run directory.
5. Parse the process exit code and write a JSON phase result; classify nonzero exit as `FAIL`, missing credentials/config as `BLOCKED`.
6. Never echo environment values or command strings containing secret paths/values.

The primary command must be:

```powershell
pwsh -NoProfile -File .\scripts\run-k3s-production.ps1 -Inventory .\ansible\enterprise-k3s\inventory\production.yml
```

- [ ] **Step 4: Run unit/static tests**

Run: `pwsh -NoProfile -Command "Invoke-Pester scripts/tests/run-k3s-production.Tests.ps1"` and `pwsh -NoProfile -File scripts/run-k3s-production.ps1 -Help`

Expected: tests PASS; help does not contact hosts or request credentials.

- [ ] **Step 5: Commit**

```bash
git add scripts/run-k3s-production.ps1 scripts/tests/run-k3s-production.Tests.ps1 ansible/enterprise-k3s/README.md
git commit -m "ops: add one-command production runner"
```

### Task 4: Add Vault Azure auto-unseal and backup phases

**Files:**
- Modify: `scripts/bootstrap-vault-azure-unseal.ps1`
- Modify: `scripts/bootstrap-cnpg-azure-object-store.ps1`
- Create: `scripts/verify-production-backup-restore.ps1`
- Modify: `ansible/enterprise-k3s/playbooks/40-production-orchestrator.yml`
- Test: `scripts/tests/azure-production-scripts.Tests.ps1`

**Interfaces:**
- Consumes: `D:\secure\his-hope\azure-production.env`, `D:\secure\his-hope\azure_client_secret`, production kubecontext.
- Produces: Vault Azure key verification, CNPG object-store application, uploaded backup object, and isolated restore result.

- [ ] **Step 1: Write failing script contract tests**

```powershell
Describe 'Azure production scripts' {
  It 'contains placeholder rejection logic' {
    (Get-Content 'scripts/bootstrap-vault-azure-unseal.ps1' -Raw) | Should -Match 'placeholder'
  }
  It 'has a restore verification entry point' {
    Test-Path 'scripts/verify-production-backup-restore.ps1' | Should -BeTrue
  }
}
```

- [ ] **Step 2: Run tests and confirm the missing restore entry point fails**

Run: `pwsh -NoProfile -Command "Invoke-Pester scripts/tests/azure-production-scripts.Tests.ps1"`

Expected: FAIL until the restore verifier exists.

- [ ] **Step 3: Implement preflight and restore wiring**

The phase must call existing scripts in this order: `bootstrap-vault-azure-unseal.ps1`, `bootstrap-cnpg-azure-object-store.ps1`, backup-agent playbook, then `verify-production-backup-restore.ps1`. The restore verifier must use a dedicated namespace/database target, validate object existence through Azure Blob, and delete only that isolated target after the check. It must fail if any required env key is absent or contains a placeholder.

- [ ] **Step 4: Run script parsing and dry validation**

Run: `pwsh -NoProfile -Command "Invoke-Pester scripts/tests/azure-production-scripts.Tests.ps1"` and parse every script with `[System.Management.Automation.Language.Parser]::ParseFile`.

Expected: PASS without contacting Kubernetes when run in validation-only mode.

- [ ] **Step 5: Commit**

```bash
git add scripts/bootstrap-vault-azure-unseal.ps1 scripts/bootstrap-cnpg-azure-object-store.ps1 scripts/verify-production-backup-restore.ps1 scripts/tests/azure-production-scripts.Tests.ps1 ansible/enterprise-k3s/playbooks/40-production-orchestrator.yml
git commit -m "ops: automate azure unseal and backup verification"
```

### Task 5: Add production application deployment and smoke phase

**Files:**
- Create: `scripts/deploy-production-application.ps1`
- Create: `scripts/tests/deploy-production-application.Tests.ps1`
- Modify: `ansible/enterprise-k3s/playbooks/40-production-orchestrator.yml`

**Interfaces:**
- Consumes: the production kubecontext and `k8s/overlays/prod-spire-azure`.
- Produces: applied resources, readiness report, and endpoint smoke-test results.

- [ ] **Step 1: Write failing deployment contract tests**

```powershell
$text = Get-Content 'scripts/deploy-production-application.ps1' -Raw -ErrorAction SilentlyContinue
if (-not $text) { throw 'Deployment runner is missing.' }
if ($text -notmatch 'kustomize') { throw 'Deployment must render the production overlay.' }
if ($text -notmatch 'rollout status') { throw 'Deployment must wait for rollout status.' }
```

- [ ] **Step 2: Run and confirm failure**

Run: `pwsh -NoProfile -File scripts/tests/deploy-production-application.Tests.ps1`

Expected: FAIL because the deployment runner is absent.

- [ ] **Step 3: Implement apply/readiness/smoke checks**

Render with `kubectl kustomize k8s/overlays/prod-spire-azure`, apply the rendered stream, wait for Deployments/StatefulSets with bounded timeouts, then query the existing health endpoints through the production ingress. Never use `--validate=false` or ignore rollout failures.

- [ ] **Step 4: Run static and render tests**

Run: `pwsh -NoProfile -File scripts/tests/deploy-production-application.Tests.ps1` and `kubectl kustomize k8s/overlays/prod-spire-azure > $env:TEMP\his-hope-prod.yaml`.

Expected: tests PASS and render contains no production placeholders.

- [ ] **Step 5: Commit**

```bash
git add scripts/deploy-production-application.ps1 scripts/tests/deploy-production-application.Tests.ps1 ansible/enterprise-k3s/playbooks/40-production-orchestrator.yml
git commit -m "ops: automate production application rollout"
```

### Task 6: Add end-to-end report and operator documentation

**Files:**
- Modify: `ansible/enterprise-k3s/README.md`
- Modify: `docs/operations/k3s-production-deployment-runbook.vi.md`
- Create: `docs/operations/k3s-production-orchestrator.vi.md`
- Test: `scripts/tests/production-orchestrator-docs.Tests.ps1`

**Interfaces:**
- Consumes: runner output and phase result schema.
- Produces: Vietnamese operator runbook with one command, resume examples, evidence locations, and explicit PASS/FAIL/BLOCKED meanings.

- [ ] **Step 1: Write documentation assertions**

```powershell
$doc = Get-Content 'docs/operations/k3s-production-orchestrator.vi.md' -Raw
foreach ($needle in 'run-k3s-production.ps1','172.16.102.100:6443','Azure Key Vault','restore','BLOCKED') {
  if ($doc -notmatch [regex]::Escape($needle)) { throw "Missing $needle" }
}
```

- [ ] **Step 2: Run and confirm failure**

Run: `pwsh -NoProfile -File scripts/tests/production-orchestrator-docs.Tests.ps1`

Expected: FAIL because the operator document does not exist.

- [ ] **Step 3: Write the runbook and report schema**

Document the exact command, interactive prompts, phase order, safe rerun behavior, artifact directory, redaction guarantees, and recovery commands. Define JSON fields `phase`, `status`, `startedAt`, `completedAt`, `logPath`, and `evidencePath`.

- [ ] **Step 4: Run documentation tests and final static checks**

Run: `pwsh -NoProfile -File scripts/tests/production-orchestrator-docs.Tests.ps1`, `git diff --check`, and `kubectl kustomize k8s/overlays/prod-spire-azure > $env:TEMP\his-hope-prod.yaml`.

Expected: all checks PASS.

- [ ] **Step 5: Commit**

```bash
git add ansible/enterprise-k3s/README.md docs/operations/k3s-production-deployment-runbook.vi.md docs/operations/k3s-production-orchestrator.vi.md scripts/tests/production-orchestrator-docs.Tests.ps1
git commit -m "docs: document production orchestrator operations"
```

### Task 7: Execute production gates and runtime verification

**Files:**
- No source changes; use the committed runner and production inventory.
- Evidence: `artifacts/k3s-production/run-YYYYMMDD-HHmmss/summary.json` and redacted logs.

**Interfaces:**
- Consumes: approved runner, interactive credentials, live hosts, Azure permissions, and production network ACLs.
- Produces: runtime proof for K3s, application readiness, Azure Key Vault unseal, Blob backup, and isolated restore.

- [ ] **Step 1: Run validation-only mode**

Run: `pwsh -NoProfile -File .\scripts\run-k3s-production.ps1 -Inventory .\ansible\enterprise-k3s\inventory\production.yml -ValidationOnly`

Expected: PASS for local prerequisites and no remote mutation.

- [ ] **Step 2: Run the production workflow**

Run: `pwsh -NoProfile -File .\scripts\run-k3s-production.ps1 -Inventory .\ansible\enterprise-k3s\inventory\production.yml`

Expected: interactive prompts appear once; each phase is recorded.

- [ ] **Step 3: Verify the runtime invariants**

Run the report verifier and confirm: three control-plane nodes, two Ready workers, API through `172.16.102.100:6443`, Vault unsealed through Azure Key Vault, at least one Azure Blob backup object, application rollouts Ready, and isolated restore PASS.

- [ ] **Step 4: Commit only evidence metadata if required**

Do not commit logs, kubeconfigs, secrets, or backup data. Commit only any non-secret report schema changes required by review.
