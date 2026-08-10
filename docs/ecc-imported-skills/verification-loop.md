---
name: verification-loop
description: "A comprehensive verification system for His.Hope development sessions. Run build → typecheck → lint → test → security → diff review before any PR."
license: MIT
metadata:
  origin: ECC (imported)
---

# Verification Loop Skill

A comprehensive verification system for His.Hope development sessions.

## When to Use

Invoke this skill:
- After completing a feature or significant code change
- Before creating a PR
- When you want to ensure quality gates pass
- After refactoring
- Before @git commits (complements @orchestrator's quality gates)

## Verification Phases

### Phase 1: Build Verification
```bash
# .NET backend
dotnet build 2>&1 | tail -20

# Angular frontend
cd src/Web/Frontend && npm run build 2>&1 | tail -20
```

If build fails, STOP and fix before continuing.

### Phase 2: Type Check
```bash
# .NET projects
dotnet build --no-restore 2>&1 | grep "error CS"

# TypeScript/Angular
cd src/Web/Frontend && npx --no-install tsc --noEmit 2>&1 | head -30
```

Report all type errors. Fix critical ones before continuing.

### Phase 3: Lint Check
```bash
# .NET
dotnet format --verify-no-changes 2>&1

# Angular
cd src/Web/Frontend && npm run lint 2>&1 | head -30
```

### Phase 4: Test Suite
```bash
# Backend tests
dotnet test 2>&1 | tail -50

# Frontend tests
cd src/Web/Frontend && npm test -- --coverage 2>&1 | tail -50
```

Report:
- Total tests: X
- Passed: X
- Failed: X
- Coverage: X%

### Phase 5: Security Scan
```bash
# Check for hardcoded secrets
grep -rn "sk-" --include="*.cs" --include="*.ts" --include="*.json" src/ 2>/dev/null | head -10
grep -rn "api_key" --include="*.cs" --include="*.ts" src/ 2>/dev/null | head -10

# Check for console.log in production code
grep -rn "console.log" --include="*.ts" --include="*.tsx" src/Web/ 2>/dev/null | head -10

# Check for hardcoded connection strings
grep -rn "Server=.*;Database=" src/ --include="*.cs" 2>/dev/null | head -10
```

### Phase 6: Diff Review
```bash
# Show what changed
git diff --stat
git diff HEAD~1 --name-only
```

Review each changed file for:
- Unintended changes
- Missing error handling
- Potential edge cases
- HIPAA/PII compliance (no patient data in logs)

## Output Format

After running all phases, produce a verification report:

```
VERIFICATION REPORT
==================

Build:     [PASS/FAIL]
Types:     [PASS/FAIL] (X errors)
Lint:      [PASS/FAIL] (X warnings)
Tests:     [PASS/FAIL] (X/Y passed, Z% coverage)
Security:  [PASS/FAIL] (X issues)
Diff:      [X files changed]

Overall:   [READY/NOT READY] for PR

Issues to Fix:
1. ...
2. ...
```

## Continuous Mode

For long sessions, run verification every 15 minutes or after major changes:

```markdown
Set a mental checkpoint:
- After completing each microservice endpoint
- After finishing an Angular component
- Before moving to next task
- Before calling @git commit
```

## Integration with @orchestrator

This skill complements the @orchestrator's 5-phase pipeline:
- Phase 3 (Test): Use verification-loop for comprehensive testing before quality gates
- Phase 4 (Validate): The security review phase maps to verification-loop Phase 5
- Inform @orchestrator of the VERIFICATION REPORT before green-lighting @git

## His.Hope-Specific Checks

### .NET Backend
- Check gRPC contract compatibility (buf breaking check)
- Verify EF Core migration is backward-compatible
- Check Polly circuit breaker configuration
- Verify OpenTelemetry instrumentation

### Angular Frontend
- Check Angular Material theming consistency
- Verify WCAG 2.1 AA accessibility
- Check NgRx store state shape changes
- Verify responsive layout breakpoints

### Database
- Check migration files for backward compatibility
- Verify no SELECT * in new queries
- Check CockroachDB global table usage
