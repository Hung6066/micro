# Container Image Signing with Cosign

> **Last Updated:** 2026-07-16  
> **Status:** Planned for Q3 2026

## Overview

Container image signing ensures the integrity and authenticity of all container images deployed in the His.Hope cluster. This prevents supply chain attacks where compromised images could be deployed.

## Implementation Plan

### 1. Key Management (Vault)
Generate Cosign key pair using Vault transit engine. Private key stored in Vault, public key distributed to cluster.

### 2. CI/CD Integration (Tekton)
All image builds include a Cosign signing step using the private key from Vault.

### 3. Admission Control (OPA/Gatekeeper)
Gatekeeper constraint to require signed images in production namespaces.

### 4. Image Digest Pinning

**Current State (WARNING):** Base K8s manifests use `:latest` tags:
```yaml
image: his-hope/identity-service:latest  # BAD
```

**Required Change:**
```yaml
image: his-hope/identity-service@sha256:abcdef1234567890...  # GOOD
```

### Migration Plan
1. CI/CD pipeline resolves `:latest` to digest after build
2. Kustomize or ArgoCD applies digest update automatically
3. Gatekeeper enforces no `:latest` tags in production

### Gatekeeper Constraint Example
```rego
package k8srequiredimages

violation[{"msg": msg}] {
  container := input.review.object.spec.containers[_]
  contains(container.image, ":latest")
  msg := sprintf("Container %v uses :latest tag", [container.name])
}
```

## Timeline

| Milestone | Target | Dependencies |
|-----------|--------|-------------|
| Key generation in Vault | Q3 2026 | Vault PKI setup |
| Cosign in Tekton pipelines | Q3 2026 | Pipeline refactor |
| Gatekeeper constraints | Q3 2026 | OPA/Gatekeeper installed |
| Image digest migration | Q3 2026 | Kustomize config updates |
| Full enforcement | Q4 2026 | All images verified |
