# ADR 011: Image Digest Pinning for Production

**Status**: Proposed (Q3 2026)

**Date**: 2026-07-16

**Context**: Container image tags (`:latest`, `:1.0.0`) are mutable and can be repointed, breaking reproducibility and enabling supply chain attacks.

**Decision**: Use Kustomize component (`image-digests.yaml`) to pin SHA256 digests for production. Cosign for image signing. Gatekeeper for admission control.

**Consequences**: Requires automated digest update pipeline. CI/CD must produce signed images. Rollback involves updating digests, not tags.
