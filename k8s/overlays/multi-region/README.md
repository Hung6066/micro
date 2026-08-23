# Multi-region active/passive overlay

This overlay extends `k8s/overlays/prod` for a passive secondary region rehearsal.

- `DR_MODE=active-passive`
- Identity and gateway replicas are reduced in the passive region until failover promotion.
- Production go-live still requires measured restore evidence in `artifacts/evidence/`.

Validate locally:

```powershell
kubectl kustomize k8s/overlays/multi-region --load-restrictor LoadRestrictionsNone
```
