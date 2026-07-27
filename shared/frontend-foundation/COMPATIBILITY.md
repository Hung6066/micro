# Compatibility Policy

## Supported runtime

- Angular 21.x
- TypeScript 5.9+
- RxJS 7.8+
- Modern evergreen browsers with `BroadcastChannel`, `ResizeObserver` and CSS custom properties.

## Public API

Only exports from `dist/index.d.ts` and the declared package subpaths are public. Internal files and CSS selectors are not compatibility contracts. Components use standalone Angular APIs and CSS custom properties from the shared token set.

## Change rules

- Breaking input/output/type changes require a major version.
- New optional inputs/outputs and new components require a minor version.
- Bug fixes and token adjustments require a patch version.
- Every release must update `CHANGELOG.md`, build the package, build Storybook and pass accessibility/visual checks.
