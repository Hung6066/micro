# Credential Exposure Response Runbook

## Purpose

Use this runbook whenever a password, access token, session cookie, private key,
or rendered secret is found in the working tree, a commit, CI artifact, or a
developer workstation. A deleted file is not considered remediated until the
credential has been revoked and the repository history has been assessed.

## Immediate containment

1. Stop promotion of the affected release and preserve the finding path,
   commit SHA, artifact URL, and detection timestamp in the incident record.
2. Revoke active sessions and rotate the affected identity, signing,
   encryption, database, broker, and cloud credentials according to the
   credential classification. Do not paste replacement values into tickets or
   Git history.
3. Restrict access to the affected repository and CI artifacts while the
   exposure scope is being established.

## Repository remediation

1. Remove the material from the working tree and add a regression rule to
   `scripts/validate-secret-hygiene.py`.
2. Inspect every reachable ref and CI artifact. If the material was committed,
   use an approved history-rewrite process such as `git filter-repo` in a
   mirror clone; do not rewrite a shared branch without repository-owner
   approval.
3. Force-update protected refs only through the repository change procedure,
   notify all consumers to reclone or rebase, and verify that the old blob is
   unreachable from the remote.
4. Re-run the current-tree secret gate, dependency/security gates, and the
   affected authenticated tests after rotation.

## Exit criteria

The incident may be closed only when all of the following are recorded:

- affected credentials are revoked or rotated;
- current-tree and CI secret gates pass;
- reachable Git refs and retained artifacts no longer contain the material;
- authenticated E2E and service smoke checks pass with newly injected secrets;
- security owner and service owner sign off on residual risk.

The current repository gate intentionally blocks captured browser state and
known development credentials from being reintroduced. It does not claim that
old remote Git history has been purged; that requires an approved repository
operation and remote verification.
