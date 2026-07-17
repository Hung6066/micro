# Task 4 Report: Create seed entries

## Status: COMPLETED

## Summary
Created 6 seed knowledge base entries and generated INDEX.md.

## Files Created
| # | File | Path |
|---|------|------|
| 1 | patient-gotcha-01-deadlock.md | `docs/knowledge/entries/patient-service/` |
| 2 | patient-pattern-01-aggregate-factory.md | `docs/knowledge/entries/patient-service/` |
| 3 | db-gotcha-01-backward-migration.md | `docs/knowledge/entries/database/` |
| 4 | fe-gotcha-01-onpush-cdr.md | `docs/knowledge/entries/frontend/` |
| 5 | security-gotcha-01-hardcoded-secrets.md | `docs/knowledge/entries/security/` |
| 6 | devops-decision-01-linkerd.md | `docs/knowledge/entries/devops/` |

## Commit
- **Commit**: `a45cd66`
- **Message**: `docs(knowledge): add 6 seed entries from existing ADRs and coding standards`
- **Files**: 8 changed (6 entries + INDEX.md + generate-index.ps1 fix)

## Generate-Index Output
```
INDEX.md generated: 6 entries, 20 tags, 5 domains
```

### Index Breakdown
- **5 domains**: database, devops, frontend, patient-service, security
- **20 tags**: aggregate, angular, async, backward-compat, change-detection, cockroachdb, deadlock, domain, dotnet, ef-core, factory-method, istio, kubernetes, linkerd, migration, onpush, performance, secrets, service-mesh, vault
- **By type**: 4 gotchas, 1 pattern, 1 decision
- **By severity**: 3 critical, 1 warning, 2 info

## Script Fix
The `generate-index.ps1` script had a cross-platform line-ending bug: extraction regexes (`^id:`, `^type:`, etc.) lacked the `(?m)` multiline flag, causing them to fail on `\r\n` line endings on Windows. Fixed by:
1. Adding line-ending normalization (`-replace '\r?\n'`) at parse time
2. Adding `(?m)` flag to all field extraction and tag regexes
3. This fix was committed alongside the seed entries

## Concerns
- None. All entries have valid frontmatter, INDEX.md generation verified correct.
