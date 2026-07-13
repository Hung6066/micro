# Linkerd Service Mesh - His.Hope Setup Guide

## Installation

```bash
# Install Linkerd CLI
curl -sL https://run.linkerd.io/install | sh
export PATH=$PATH:$HOME/.linkerd2/bin

# Install Linkerd control plane
linkerd install --crds | kubectl apply -f -
linkerd install \
  --identity-trust-domain cluster.local \
  --proxy-cpu-limit 200m \
  --proxy-memory-limit 256Mi \
  | kubectl apply -f -

# Verify installation
linkerd check

# Install Viz extension
linkerd viz install | kubectl apply -f -
linkerd viz check

# Install Jaeger extension
linkerd jaeger install | kubectl apply -f -
linkerd jaeger check

# Install Multicluster extension
linkerd multicluster install | kubectl apply -f -
linkerd multicluster check
```

## Injecting services

```bash
# Automatic injection via namespace annotation
kubectl annotate namespace his-hope linkerd.io/inject=enabled

# Manual injection for existing deployments
kubectl get deploy patient-service -n his-hope -o yaml | linkerd inject - | kubectl apply -f -
```

## Verifying mTLS

```bash
linkerd viz edges -n his-hope
linkerd viz stat deployments -n his-hope
linkerd viz tap deploy/patient-service -n his-hope --to deploy/clinical-service
```

## Dashboard

```bash
linkerd viz dashboard &
```

## Cross-Cluster (Multicluster)

```bash
# On cluster A
linkerd multicluster link --cluster-name=us-east1 | kubectl apply --context=cluster-b -f -

# Verify
linkerd multicluster check
kubectl -n his-hope get svc -l multicluster.linkerd.io/exported=true
```

## Traffic Splitting (Canary)

```bash
# Apply TrafficSplit
kubectl apply -f k8s/linkerd/traffic-split.yaml

# Update weights for gradual rollout
kubectl patch trafficsplit patient-service-split -n his-hope --type=json \
  -p='[{"op": "replace", "path": "/spec/backends/0/weight", "value": "800m"}, {"op": "replace", "path": "/spec/backends/1/weight", "value": "200m"}]'

# Verify
linkerd viz stat trafficsplit -n his-hope
```

## Retries & Timeouts

ServiceProfile defines per-route retry/timeout policies:
```bash
kubectl apply -f k8s/linkerd/service-profiles.yaml
linkerd viz routes -n his-hope svc/patient-service
```
