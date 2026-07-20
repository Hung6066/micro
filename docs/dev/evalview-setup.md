# Evalview — AI Agent Regression Testing

[Evalview](https://github.com/affaan-m/ECC/tree/main/skills/evalview) is an AI agent regression testing tool imported from ECC. It snapshots agent behavior and detects regressions in tool calls and output quality.

## Prerequisites

- Python 3.10+
- pip

## Installation

```bash
pip install "evalview>=0.5,<1"
```

Verify installation:
```bash
python3 -m evalview --help
```

## MCP Server

Evalview is configured in `opencode.json` as `evalview` MCP server (disabled by default). To enable:

1. Install the Python package (above)
2. Set `"enabled": true` for the `evalview` entry in `opencode.json`
3. Optionally set `OPENAI_API_KEY` env var for LLM-based checks

## Tools Provided

Once enabled, evalview exposes these MCP tools:

| Tool | Description |
|---|---|
| `create_test` | Create a new snapshot test |
| `run_snapshot` | Run a snapshot and compare with baseline |
| `run_check` | Run a deterministic check on output |
| `list_tests` | List all tests in the workspace |
| `validate_skill` | Validate a skill's test coverage |
| `generate_skill_tests` | Auto-generate tests for a skill |
| `run_skill_test` | Run tests for a specific skill |
| `generate_visual_report` | Generate an HTML visual report |

## Agent Usage

Agents can reference evalview for regression testing:

- **@qa** — run snapshot tests after quality gates
- **@orchestrator** — include evalview in Phase 3 (Test) of pipelines
- **@testing-frontend** — validate agent behavior before E2E tests

## Integration with Quality Gates

Evalview complements the existing quality gate pipeline:

```
Build → Typecheck → Lint → Unit Tests → Evalview (agent regression) → Security Scan → Diff Check
```

Add evalview as an optional gate in `verification-loop` skill Phase 5 (Security + Quality).
