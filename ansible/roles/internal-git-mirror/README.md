# Internal Git mirror

This role deploys a single-node Gitea mirror in the `git-mirror` namespace. It
uses the production `local-path` StorageClass and is intentionally kept
internal (`ClusterIP` only). The mirror is not an Argo source until the
repository has been created and populated.

## Deploy

Run from WSL (the Windows Ansible launcher cannot configure stdout blocking):

```bash
cd /mnt/d/AI/micro
ANSIBLE_CONFIG=/mnt/d/AI/micro/ansible/ansible.cfg \
ansible-playbook -i localhost, \
  ansible/playbooks/deploy-internal-git-mirror.yml \
  -e git_mirror_kubeconfig=/mnt/d/AI/micro/artifacts/kubeconfig-production.yaml \
  -e git_mirror_manifest_dir=/mnt/d/AI/micro/k8s/git-mirror
```

The role does not create credentials or copy a repository. Create the initial
Gitea administrator and mirror repository through a protected port-forward,
then configure Argo's repository secret to use:

```text
http://gitea.git-mirror.svc.cluster.local:3000/Hung6066/micro.git
```

Do not put the administrator password, GitHub token, or an Argo repository
token in Git. Store them in the existing encrypted Ansible/Vault secret store.
