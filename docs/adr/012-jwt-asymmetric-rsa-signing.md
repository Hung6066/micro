# ADR 012: JWT with Asymmetric RSA Signing (Not HMAC)

**Status**: Accepted

**Date**: 2026-07-16

**Context**: HMAC requires sharing the symmetric key with all services that validate tokens. RSA allows public key distribution.

**Decision**: RSA-SHA256 with Vault transit engine for signing. IdentityService holds signing privilege via Vault policy. All other services verify with public key (from Vault or PEM file).

**Consequences**: Token size larger (~500-800 bytes vs ~200 for HMAC). Signing requires Vault call (~5ms). Key rotation via Vault transit key versioning.
