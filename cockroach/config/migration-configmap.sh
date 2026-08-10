#!/bin/bash
set -euo pipefail

NAMESPACE="${NAMESPACE:-his-hope}"
MIGRATIONS_DIR="${MIGRATIONS_DIR:-../migrations}"
CONFIGMAP_NAME="${CONFIGMAP_NAME:-cockroach-migrations}"

echo "Creating ConfigMap '${CONFIGMAP_NAME}' in namespace '${NAMESPACE}'..."
echo "Using migrations from: ${MIGRATIONS_DIR}"
echo ""

# Create ConfigMap from all SQL files in the migrations directory
kubectl create configmap "${CONFIGMAP_NAME}" \
  --namespace="${NAMESPACE}" \
  --from-file="${MIGRATIONS_DIR}/" \
  --dry-run=client \
  -o yaml | kubectl apply -f -

echo ""
echo "ConfigMap '${CONFIGMAP_NAME}' created successfully."
echo ""
echo "Verifying contents:"
kubectl get configmap "${CONFIGMAP_NAME}" \
  --namespace="${NAMESPACE}" \
  -o jsonpath='{.data}' | jq 'keys'
echo ""
echo "To view a specific migration:"
echo "  kubectl get configmap ${CONFIGMAP_NAME} --namespace=${NAMESPACE} -o jsonpath='{.data.001-create-databases\\.sql}'"
