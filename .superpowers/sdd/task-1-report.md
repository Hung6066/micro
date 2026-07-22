# Task 1 Report: ES Log Retention (ILM + Index Template)

## Status: Complete

## Commits

| Commit | Message | Files |
|--------|---------|-------|
| `725ac3b` | `feat(monitoring): add ES ILM policy (30d retention) and index template` | 2 files |

### Files Created

- `k8s/monitoring/elasticsearch-ilm-policy.yaml` — ConfigMap with ILM policy `his-hope-logs-policy` (hot phase → delete at 30d)
- `k8s/monitoring/elasticsearch-index-template.yaml` — ConfigMap with index template binding `his-hope-logs-*` to the ILM policy

### Files Modified

- `k8s/monitoring/elasticsearch.yaml` — No modification needed. Lifecycle hook, volumeMounts, and volumes for ILM/config were already present from prior commit `ec08316`.

## What Was Done

1. Created ILM policy ConfigMap with a 30-day retention policy (hot phase → delete at 30d)
2. Created index template ConfigMap that binds `his-hope-logs-*` indices to `his-hope-logs-policy` with rollover alias `his-hope-logs`
3. elasticsearch.yaml already contained the postStart lifecycle hook to apply ILM policy on startup, plus the volume/volumeMount entries for the ConfigMaps (from commit `ec08316`)

## Verification

- All YAML files parsed successfully via Python `yaml.safe_load`
- `elasticsearch.yaml` (multi-document StatefulSet) parses correctly
- ILM policy JSON schema is valid
- Index template JSON schema is valid

## Notes on ec08316

The parent commit `ec08316` (`feat(monitoring): enable ES xpack.security and add Kibana credentials`) already included the lifecycle postStart hook that applies `his-hope-logs-policy` and `his-hope-logs-template` via curl on startup, along with the matching volume mounts and volumes. This means the elasticsearch.yaml modifications (Step 3 from the brief) were pre-existing and our task was limited to creating the 2 ConfigMap manifests.

## Concerns

None. The ILM policy and index template ConfigMaps are in place. When deployed alongside the existing elasticsearch.yaml, the postStart hook will:
1. Wait for ES cluster health (green/yellow)
2. `PUT /_ilm/policy/his-hope-logs-policy`
3. `PUT /_index_template/his-hope-logs-template`

All indices matching `his-hope-logs-*` will be deleted after 30 days.
