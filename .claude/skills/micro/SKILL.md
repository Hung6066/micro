```markdown
# micro Development Patterns

> Auto-generated skill from repository analysis

## Overview

This skill teaches the core development patterns and workflows used in the `micro` TypeScript codebase. It covers coding conventions, commit standards, testing practices, and step-by-step guides for major upgrade and documentation workflows. Whether you're contributing new features, performing upgrades, or documenting initiatives, this guide ensures consistency and quality across the project.

## Coding Conventions

### File Naming

- **Style:** kebab-case
- **Example:**  
  ```
  user-service.ts
  data-access-layer.spec.ts
  ```

### Import Style

- **Style:** Relative imports
- **Example:**
  ```typescript
  import { fetchData } from './utils/fetch-data';
  ```

### Export Style

- **Style:** Named exports
- **Example:**
  ```typescript
  // In user-service.ts
  export function getUser(id: string) { ... }
  export const USER_ROLE = 'admin';
  ```

### Commit Messages

- **Type:** Conventional commits
- **Prefixes:** `chore`, `docs`
- **Format Example:**
  ```
  chore: update dependencies for security
  docs: add API usage instructions
  ```

## Workflows

### Angular Major Upgrade

**Trigger:** When upgrading the Angular framework to a new major version  
**Command:** `/upgrade-angular`

1. **Update dependencies:**  
   Edit `package.json` and `package-lock.json` in `src/Frontend/his-hope-app/` to the new Angular version.
2. **Update Angular CLI and core packages:**  
   Upgrade `@angular/cli`, `@angular/core`, etc.
3. **Update Angular Material and CDK:**  
   Upgrade `@angular/material` and `@angular/cdk` packages.
4. **Update NgRx packages:**  
   Upgrade NgRx-related dependencies.
5. **Update application code and tests:**  
   Refactor code in `src/app/**/*.ts` and tests in `src/app/**/*.spec.ts` for compatibility.
6. **Update styles/themes:**  
   Modify `src/styles/_theme.scss` if style changes are required.
7. **Update build configuration:**  
   Adjust `angular.json` or switch build tools as needed.
8. **Update Dockerfile/scripts:**  
   Update `Dockerfile` or any build/deploy scripts if necessary.

**Example:**  
```bash
# Upgrade Angular CLI globally
npm install -g @angular/cli@<new-version>
# Upgrade project dependencies
npm install
# Run tests to verify
npm test
```

### Document Major Initiative

**Trigger:** When planning and documenting a major technical initiative or upgrade  
**Command:** `/new-initiative-docs`

1. **Write a design spec:**  
   Create a markdown file in `docs/superpowers/specs/` describing the design.
2. **Write an implementation plan:**  
   Add a markdown file in `docs/superpowers/plans/` detailing the implementation steps.

**Example:**  
```markdown
# docs/superpowers/specs/new-feature.md

## Overview
Describe the feature and its goals.

## Design
Outline architecture, data flow, and dependencies.
```

## Testing Patterns

- **Framework:** Jest
- **File Pattern:** Test files use the `*.spec.ts` naming convention.
- **Example:**
  ```typescript
  // user-service.spec.ts
  import { getUser } from './user-service';

  describe('getUser', () => {
    it('returns user data for a valid id', () => {
      expect(getUser('123')).toEqual({ id: '123', name: 'Alice' });
    });
  });
  ```

## Commands

| Command              | Purpose                                                    |
|----------------------|------------------------------------------------------------|
| /upgrade-angular     | Perform a major Angular and dependency upgrade             |
| /new-initiative-docs | Create design specs and implementation plans for initiatives|
```
