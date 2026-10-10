# ADR 011: Image Digest Pinning for Production

**Status**: Accepted (Q3 2026)

**Date**: 2026-07-16

**Context**: Container image tags (`:latest`, `:1.0.0`) are mutable and can be repointed, breaking reproducibility and enabling supply chain attacks.

**Decision**: Use the canonical Kustomize component (`k8s/overlays/prod/image-digests/kustomization.yaml`) to pin SHA256 digests for production. Cosign signing and provenance verification run before promotion, while Gatekeeper enforces admission-time digest policy. The legacy `k8s/overlays/prod/image-digests.yaml` file is retained only for traceability and must not be updated.

**Consequences**: Requires automated digest update pipeline. CI/CD must produce signed images. Rollback involves updating digests, not tags.
