# Migration Guide

## From Material or feature CSS

1. Replace page-level containers with `hh-page-layout` and `hh-page-header`.
2. Replace custom tables with `hh-data-table`; keep API mapping and domain actions in the feature.
3. Replace hard-coded values with the shared CSS variables from `src/styles/_tokens.scss`.
4. Replace bespoke permission checks with `hh-permission-button` and pass the effective permission set from the application.
5. Replace local toast/error markup with `HisHopeToastService` and `hh-state`.
6. Remove the feature CSS only after the shared component renders all loading, empty, error, and narrow-screen states.

## Compatibility

The package follows semantic versioning. Breaking selector, input, output, token, or accessibility-contract changes require a major version and a migration note in `COMPATIBILITY.md` and `CHANGELOG.md`.
