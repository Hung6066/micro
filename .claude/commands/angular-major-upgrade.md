---
name: angular-major-upgrade
description: Workflow command scaffold for angular-major-upgrade in micro.
allowed_tools: ["Bash", "Read", "Write", "Grep", "Glob"]
---

# /angular-major-upgrade

Use this workflow when working on **angular-major-upgrade** in `micro`.

## Goal

Performs a major upgrade of Angular and related packages, including updating dependencies, code, and tests.

## Common Files

- `src/Frontend/his-hope-app/package.json`
- `src/Frontend/his-hope-app/package-lock.json`
- `src/Frontend/his-hope-app/src/app/**/*.ts`
- `src/Frontend/his-hope-app/src/app/**/*.spec.ts`
- `src/Frontend/his-hope-app/src/styles/_theme.scss`
- `src/Frontend/his-hope-app/angular.json`

## Suggested Sequence

1. Understand the current state and failure mode before editing.
2. Make the smallest coherent change that satisfies the workflow goal.
3. Run the most relevant verification for touched files.
4. Summarize what changed and what still needs review.

## Typical Commit Signals

- Update package.json and package-lock.json to new Angular version
- Update Angular CLI and core packages
- Update Angular Material and CDK packages
- Update NgRx packages
- Update application code and tests to be compatible with new Angular version

## Notes

- Treat this as a scaffold, not a hard-coded script.
- Update the command if the workflow evolves materially.