# ADR 015: NIST AAL Assurance Policy

## Status

Accepted — 2026-08-22

## Context

Enterprise production for privileged admin and PHI access requires explicit
assurance mapping from journey to NIST SP 800-63B-style AAL levels, with
recovery controls that cannot bypass MFA policy on high-risk flows.

## Decision

1. Publish `config/assurance-policy.v1.json` as the versioned assurance contract.
2. Evaluate journeys through `AssurancePolicyEvaluator` in Identity Application.
3. Require Security, Compliance, and Clinical Safety approval metadata in the policy file.
4. Forbid weak recovery-only flows (`email-only`, `security-question-only`) on high-risk journeys.

## Consequences

- Admin write and clinical read/write journeys require step-up and fresh device posture where configured.
- Break-glass remains time-bound and explicitly allowed only on the `break-glass` journey.
- Runtime enforcement remains authoritative at Identity PEP; UI guards are not sufficient evidence.

## Evidence

- Unit tests: `AssurancePolicyEvaluatorTests`
- Staging posture contract: `config/environments/staging.env.example`
- Enterprise validator: `scripts/validate-enterprise-production-phases.ps1`
