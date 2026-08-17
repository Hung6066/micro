#!/usr/bin/env bash
set -euo pipefail

kubeconfig="${1:?kubeconfig is required}"
source_namespace="${2:-his-hope}"
target_namespace="${3:-his-hope-data}"
shift 3

kubectl --kubeconfig "$kubeconfig" create namespace "$target_namespace" --dry-run=client -o yaml |
  kubectl --kubeconfig "$kubeconfig" apply -f - >/dev/null

for secret_name in "$@"; do
  kubectl --kubeconfig "$kubeconfig" -n "$source_namespace" get secret "$secret_name" -o json |
    python3 /mnt/d/AI/micro/scripts/prepare-secret.py "$target_namespace" |
    kubectl --kubeconfig "$kubeconfig" apply -f - >/dev/null
done

for secret_name in "$@"; do
  test -n "$(kubectl --kubeconfig "$kubeconfig" -n "$target_namespace" get secret "$secret_name" -o jsonpath='{.data}')"
done
