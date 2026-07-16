# ADR 008: Defense-in-Depth Network Policies (Cilium + K8s Native)

**Status**: Accepted

**Date**: 2026-07-16

**Context**: Single network policy layer could be bypassed by misconfiguration. Cilium policies enforce at eBPF, K8s native at iptables.

**Decision**: Both CiliumNetworkPolicy AND Kubernetes NetworkPolicy deployed simultaneously. Both use identical `app:` label selectors. Traffic must pass both enforcement layers.

**Consequences**: Policy changes must be made in both files. Slight performance overhead from double filtering. Defense-in-depth worth the trade-off.
