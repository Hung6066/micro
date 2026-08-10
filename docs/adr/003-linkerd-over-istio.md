# ADR 003: Linkerd over Istio for Service Mesh

**Status**: Accepted

**Date**: 2026-07-16

**Context**: Need mTLS, traffic splitting, observability. Istio provides more features but at higher complexity.

**Decision**: Linkerd 2.x for its simplicity, performance (Rust-based proxy), automatic mTLS with 24h cert rotation, and lower resource footprint.

**Consequences**: Fewer traffic management features than Istio (no EnvoyFilter, limited Wasm). Acceptable trade-off for simplicity.
