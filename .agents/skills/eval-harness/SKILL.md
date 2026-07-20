---
name: eval-harness
description: Formal evaluation framework implementing eval-driven development (EDD) principles for His.Hope. Define pass/fail criteria, run pass@k metrics, track regression evals.
metadata:
  origin: ECC (imported)
---

# Eval Harness Skill

A formal evaluation framework for His.Hope development sessions, implementing eval-driven development (EDD) principles.

## When to Activate

- Setting up eval-driven development (EDD) for AI-assisted workflows
- Defining pass/fail criteria for agent task completion
- Measuring agent reliability with pass@k metrics
- Creating regression test suites for prompt or agent changes
- Benchmarking agent performance across model versions
- Before @orchestrator quality gates

## Philosophy

Eval-Driven Development treats evals as the "unit tests of AI development":
- Define expected behavior BEFORE implementation
- Run evals continuously during development
- Track regressions with each change
- Use pass@k metrics for reliability measurement

## Eval Types

### Capability Evals
Test if an agent can do something it couldn't before:
```markdown
[CAPABILITY EVAL: feature-name]
Task: Description of what the agent should accomplish
Success Criteria:
  - [ ] Criterion 1
  - [ ] Criterion 2
  - [ ] Criterion 3
Expected Output: Description of expected result
```

### Regression Evals
Ensure changes don't break existing functionality:
```markdown
[REGRESSION EVAL: feature-name]
Baseline: SHA or checkpoint name
Tests:
  - existing-test-1: PASS/FAIL
  - existing-test-2: PASS/FAIL
  - existing-test-3: PASS/FAIL
Result: X/Y passed (previously Y/Y)
```

## Grader Types

### 1. Code-Based Grader
Deterministic checks using code:
```bash
# Check if file contains expected pattern
grep -q "public class CreatePatientHandler" src/Services/Patient/ && echo "PASS" || echo "FAIL"

# Check if tests pass
dotnet test --filter "PatientTests" && echo "PASS" || echo "FAIL"

# Check if build succeeds
dotnet build && echo "PASS" || echo "FAIL"
```

### 2. Model-Based Grader
Use LLM to evaluate open-ended outputs:
```markdown
[MODEL GRADER PROMPT]
Evaluate the following code change:
1. Does it solve the stated problem?
2. Does it follow Clean Architecture patterns?
3. Are CQRS handlers properly separated?
4. Is error handling appropriate?

Score: 1-5 (1=poor, 5=excellent)
Reasoning: [explanation]
```

### 3. Human Grader
Flag for manual review:
```markdown
[HUMAN REVIEW REQUIRED]
Change: Description of what changed
Reason: Why human review is needed
Risk Level: LOW/MEDIUM/HIGH
Domain: [Patient/Clinical/Billing/etc.]
```

## Metrics

### pass@k
"At least one success in k attempts"
- pass@1: First attempt success rate
- pass@3: Success within 3 attempts
- Typical target: pass@3 > 90%

### pass^k
"All k trials succeed"
- Higher bar for reliability
- pass^3: 3 consecutive successes
- Use for critical paths (patient data, billing)

## Eval Workflow

### 1. Define (Before Coding)
```markdown
## EVAL DEFINITION: patient-registration

### Capability Evals
1. Can create new patient record with required fields
2. Can validate MRN format
3. Can encrypt PII at rest

### Regression Evals
1. Existing patient search still works
2. Appointment creation unchanged
3. Billing integration intact

### Success Metrics
- pass@3 > 90% for capability evals
- pass^3 = 100% for regression evals
```

### 2. Implement
Write code to pass the defined evals.

### 3. Evaluate
```bash
# Run capability evals
[Run each capability eval, record PASS/FAIL]

# Run regression evals
dotnet test --filter "PatientTests"

# Generate report
```

### 4. Report
```markdown
EVAL REPORT: patient-registration
=================================

Capability Evals:
  create-patient:    PASS (pass@1)
  validate-mrn:      PASS (pass@2)
  encrypt-pii:       PASS (pass@1)
  Overall:           3/3 passed

Regression Evals:
  patient-search:    PASS
  appointment:       PASS
  billing:           PASS
  Overall:           3/3 passed

Metrics:
  pass@1: 67% (2/3)
  pass@3: 100% (3/3)

Status: READY FOR REVIEW
```

## Integration with @orchestrator

The eval framework feeds into @orchestrator's quality gates:
- **Phase 1 (Plan)**: Define evals for each feature
- **Phase 3 (Test)**: Run evals as gate criteria
- **Phase 4 (Validate)**: Eval report informs the go/no-go decision

## His.Hope Domain Evals

### Patient Service
```markdown
[CAPABILITY EVAL: patient-lookup]
- Can look up patient by MRN
- Can look up patient by name/DOB
- Returns 404 for non-existent patient
- Response includes no more than required PII
- Audit log entry created for each lookup
```

### Clinical Service
```markdown
[CAPABILITY EVAL: medication-order]
- Validates drug-allergy interactions
- Checks dosage within safe range
- Logs order in clinical record
- Notifies pharmacy service via event bus
```

### Billing Service
```markdown
[CAPABILITY EVAL: claim-submission]
- Validates patient insurance coverage
- Creates claim with correct CPT codes
- Handles claim rejection gracefully
- Retries failed submissions with backoff
```

## Eval Storage

Store evals in project:
```
docs/
  evals/
    patient-service.md      # Eval definitions
    clinical-service.md     # Eval definitions
    billing-service.md      # Eval definitions
    baseline.json           # Regression baselines
```

## Best Practices

1. **Define evals BEFORE coding** — Forces clear thinking about success criteria
2. **Run evals frequently** — Catch regressions early
3. **Track pass@k over time** — Monitor reliability trends
4. **Use code graders when possible** — Deterministic > probabilistic
5. **Human review for patient safety** — Never fully automate clinical decisions
6. **Keep evals fast** — Slow evals don't get run
7. **Version evals with code** — Evals are first-class artifacts
