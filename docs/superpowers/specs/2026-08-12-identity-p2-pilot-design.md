# Identity Service P2 Device Posture Pilot Design

## Goal

Provide a safe, design/pilot-ready control plane for Chrome Enterprise Device Trust, advanced device compliance, and Windows local device login without enabling vendor-dependent enforcement.

## Scope and safety boundary

- Identity Service only; clinical APIs do not consume posture decisions in this phase.
- Default mode is `Observe`. `StepUp` and `Deny` are representable and testable but require explicit policy configuration.
- No raw attestation, bearer token, certificate private key, or vendor credential is persisted.
- Evidence has a short TTL, a provenance/provider label, policy version, and replay-resistant assessment id.
- Connector integrations are contracts/adapters; live Google Workspace, Chrome Enterprise, and Windows lab validation remains a separate gate.

## Architecture

`DevicePostureAssessment` is the normalized persistence record. `DevicePosturePolicyEvaluator` evaluates current evidence against a versioned policy and returns `Observe`, `StepUp`, or `Deny`. `DevicePostureEndpoints` exposes admin assessment/preview and authenticated decision queries. Every write produces a structured audit event with redaction.

Providers are normalized to `DevicePostureEvidence` with provider, device id, signal map, observed-at, expires-at, and evidence hash. The hash is stored instead of raw proof and prevents replay of the same assessment.

## Policy defaults

The default policy is enabled for observation, has a 15-minute evidence TTL, and never blocks login. A policy can require managed/encrypted/screen-lock signals for `StepUp` or `Deny`, but enforcement is opt-in and scoped by provider. Missing, expired, malformed, or replayed evidence is not treated as compliant.

## Verification

Unit and integration tests cover mode behavior, expiry, replay, provider allow-listing, raw-secret rejection, audit redaction, and authorization. Build and targeted Identity Service tests are required. Live provider gates are reported as unavailable until credentials and a lab are supplied.
