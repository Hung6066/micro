# Self-hosted runner cho GitOps mirror

Gitea nội bộ không thể được truy cập từ GitHub-hosted runner. Runner phải nằm
trong mạng K3s/Viettel Cloud và có outbound tới GitHub cùng HTTPS tới
`git-mirror.his-hope.local`.

Không đặt runner trên LB `.13/.14` và không cấp `cluster-admin`. Dùng VM riêng
hoặc worker riêng với user hệ thống `github-runner`.

## Ansible Vault

```yaml
vault_github_actions_url: https://github.com/Hung6066/micro
vault_github_actions_runner_token: <ephemeral-registration-token>
vault_github_actions_runner_version: <pinned-runner-version>
vault_github_actions_runner_sha256: <64-char-sha256>
```

Registration token lấy từ GitHub repository Settings → Actions → Runners → New
self-hosted runner; không commit token và phải tạo lại khi hết hạn.

Chạy:

```powershell
ansible-playbook -i ansible/enterprise-k3s/inventory/gitops-runner.example.yml `
  ansible/playbooks/install-github-actions-gitops-runner.yml --ask-vault-pass
```

Runner phải online với labels `self-hosted`, `linux`, `gitops-mirror` trước khi
workflow mirror sync/verify được phép chạy.
