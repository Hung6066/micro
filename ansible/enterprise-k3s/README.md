# His.Hope enterprise K3s bootstrap

This automation builds a hardened three-server K3s control plane and two workers. It does not deploy His.Hope workloads, change the local k3d rehearsal cluster, migrate data, provision the external load balancer, or bypass the production gates in `docs/architecture/k3s-enterprise-production-upgrade.vi.md`.

## Topology

| Host | Address | Role |
|---|---:|---|
| `k3s-server-1` | `172.16.102.7` | First embedded-etcd server |
| `k3s-server-2` | `172.16.102.8` | Second embedded-etcd server |
| `k3s-server-3` | `172.16.102.9` | Third embedded-etcd server |
| `k3s-worker-1` | `172.16.102.10` | Application worker |
| `k3s-worker-2` | `172.16.102.12` | Data/observability worker |

The `10-bootstrap-k3s.yml` play uses `serial: 1`: it bootstraps server 1, waits for readiness, then joins server 2 and server 3 one at a time. The `15-bootstrap-workers.yml` play joins workers one at a time after the control plane is healthy. Do not make either play parallel.

## Prerequisites

1. Each node is a dedicated systemd Linux server with a unique hostname, synchronized NTP, swap disabled and at least 20 GiB free on `/`.
2. The existing external HA load balancer owns `172.16.102.100`. Configure a TCP listener for `6443` to the three K3s servers before bootstrap. K3s nodes do not install Keepalived and must not claim the external VIP. Set `k3s_api_registration_endpoint` to the approved LB VIP/DNS; it must not be an individual server address.
3. Network controls are approved before setting `enterprise_network_controls_verified: true`. At minimum, allow TCP 22 only from administration networks; TCP 6443 from the LB/worker paths to the K3s servers; TCP 2379-2380 only between K3s servers; TCP 10250 only from the control plane; UDP 8472 between nodes when using default Flannel VXLAN; and required DNS/NTP/registry/backup egress.
4. The installed `k3s_version` is approved and the SHA-256 of the exact `https://get.k3s.io` installer content is placed in `k3s_install_script_checksum` as `sha256:<64-hex-digits>`.
5. Create an encrypted vault file. Never commit it.

```bash
cd ansible/enterprise-k3s
cp group_vars/vault.yml.example group_vars/vault.yml
ansible-vault encrypt group_vars/vault.yml
```

Set a unique random `vault_k3s_token`, then update `group_vars/all.yml` with the API VIP/DNS, installer checksum and the approved network-controls acknowledgement.

## Run order

For the complete ordered infrastructure workflow, use the Windows/WSL runner
from the repository root. It validates prerequisites first, prompts once for
the Vault and become credentials, and writes a redacted run report under
`artifacts/k3s-production/`:

```powershell
pwsh -NoProfile -File .\scripts\run-k3s-production.ps1 `
  -Inventory .\ansible\enterprise-k3s\inventory\production.yml
```

Run a non-mutating prerequisite check before requesting a production change:

```powershell
pwsh -NoProfile -File .\scripts\run-k3s-production.ps1 `
  -Inventory .\ansible\enterprise-k3s\inventory\production.yml `
  -ValidationOnly
```

The runner supports an inclusive, contiguous `-FromPhase`/`-ToPhase` range
using the phase names `preflight`, `load-balancer`, `control-plane`, `verify`,
`workers` and `backup`. The report records `requestedPhases`, so a partial
check cannot be mistaken for a complete production workflow. The underlying
phase order is:

1. preflight
2. external HAProxy/Keepalived load balancer
3. K3s control plane (serially)
4. read-only cluster verification
5. workers (serially)
6. backup agents

For manual execution and troubleshooting, the individual commands remain:

```bash
cd ansible/enterprise-k3s
ansible-playbook -i inventory/production.yml playbooks/00-preflight.yml --ask-vault-pass --ask-become-pass --check
ansible-playbook -i inventory/production.yml playbooks/00-preflight.yml --ask-vault-pass --ask-become-pass
ansible-playbook -i inventory/production.yml playbooks/10-bootstrap-k3s.yml --ask-vault-pass --ask-become-pass
ansible-playbook -i inventory/production.yml playbooks/20-verify-cluster.yml --ask-vault-pass --ask-become-pass
ansible-playbook -i inventory/production.yml playbooks/15-bootstrap-workers.yml --ask-vault-pass --ask-become-pass
ansible-playbook -i inventory/production.yml playbooks/20-verify-cluster.yml --ask-vault-pass --ask-become-pass
```

The final verification is read-only and fails if it cannot find exactly five Ready nodes, enabled Kubernetes Secret encryption, the audit log/policy, PSA configuration, or a ready API server.

## External API load balancer

Configure the two LB VMs before the K3s bootstrap. This playbook owns HAProxy/Keepalived configuration and keeps the VIP `172.16.102.100` on the LB pair, not on K3s nodes:

```bash
wsl bash -lc 'cd /mnt/d/AI/micro/ansible/enterprise-k3s && ansible-playbook -i inventory/load-balancers.yml playbooks/05-configure-external-lb.yml --ask-vault-pass'
```

The encrypted `group_vars/vault.yml` must contain `vault_lb_keepalived_auth_pass`. Verify `172.16.102.100:6443` from a node before running the K3s playbooks.

## After bootstrap

Do not deploy workloads until the data/storage, external registry, certificate, Vault/SPIRE/Linkerd, observability, admission-policy, backup/restore and application-runtime gates pass. Follow `docs/operations/k3s-production-deployment-runbook.vi.md` and preserve evidence for every gate.
