---
name: document-major-initiative
description: Workflow command scaffold for document-major-initiative in micro.
allowed_tools: ["Bash", "Read", "Write", "Grep", "Glob"]
---

# /document-major-initiative

Use this workflow when working on **document-major-initiative** in `micro`.

## Goal

Creates design specs and implementation plans for major upgrades or initiatives.

## Common Files

- `docs/superpowers/specs/*.md`
- `docs/superpowers/plans/*.md`

## Suggested Sequence

1. Understand the current state and failure mode before editing.
2. Make the smallest coherent change that satisfies the workflow goal.
3. Run the most relevant verification for touched files.
4. Summarize what changed and what still needs review.

## Typical Commit Signals

- Write a design spec in docs/superpowers/specs/
- Write an implementation plan in docs/superpowers/plans/

## Notes

- Treat this as a scaffold, not a hard-coded script.
- Update the command if the workflow evolves materially.