# Release Checklist

- Update `version` and `CHANGELOG.md`.
- Review `COMPATIBILITY.md` for selector, token, input, output, and peer dependency changes.
- Run `npm run release:check` from this package.
- Build all consuming Angular applications.
- Run Storybook a11y and interaction checks, including keyboard, dark, high-contrast, and mobile stories.
- Review the packed file list with `npm pack --dry-run`.
- Publish from CI with a protected registry token; never publish from a developer workstation.
