# ECC MCP Integration — 5 Useful MCP Servers

**Date:** 2026-07-20
**Status:** Approved
**Source:** affaan-m/ECC mcp-configs/mcp-servers.json

## Overview

Import 5 MCP servers from ECC into His.Hope's OpenCode configuration to extend agent capabilities: cost-aware LLM routing, issue tracking, documentation search, agent regression testing, and context compression.

## 1. Nexus — LLM Cost Routing + PII Proxy

**Implementation:** Custom .NET harness tool (not external binary)

Uses existing harness infrastructure:
- CostTracker — per-agent daily budget tracking
- PiiRedactionService — HIPAA PII redaction (MRN, DOB, SSN, etc.)
- GuardrailService — cost guardrails

**Tool name:** route-llm (MCP tool: agent-harness_route-llm)

**Schema:**
- task_description: The task to route
- task_category: simple | moderate | complex | security_sensitive
- available_models: list of model names
- redact_pii: boolean flag
- agent_name: which agent is requesting

**Response:** recommended_model, redacted_task_description, estimated_cost, budget_remaining, pii_redacted

## 2. Jira — Issue Tracking

**Implementation:** opencode.json MCP server entry

Uses uvx mcp-atlassian@0.21.0 with env vars: JIRA_URL, JIRA_EMAIL, JIRA_API_TOKEN.

Enabled: false (requires API keys)

## 3. Confluence — Documentation Search

**Implementation:** opencode.json MCP server entry

Uses npx confluence-mcp-server with env vars: CONFLUENCE_BASE_URL, CONFLUENCE_EMAIL, CONFLUENCE_API_TOKEN.

Enabled: false (requires API keys)

## 4. Evalview — AI Agent Regression Testing

**Implementation:** opencode.json MCP server entry + setup guide

Uses python3 -m evalview mcp serve. Requires: pip install "evalview>=0.5,<1"

Enabled: false (requires pip install)

Capabilities: Snapshot testing for agent behavior, detect regressions in tool calls, output quality validation.

## 5. Token-Optimizer — Context Compression

**Implementation:** opencode.json MCP server entry

Uses npx -y token-optimizer-mcp.

Enabled: true

Benefits: Compress agent context before sending to LLM, reducing token usage and cost.

## Agent Updates

- architect: route-llm, jira, token-optimizer
- orchestrator: route-llm, jira, evalview
- qa: jira, evalview
- loop-engineer: route-llm, token-optimizer

## Files Changed

- opencode.json — Add 5 MCP servers, update agent descriptions
- RouteLlmTool.cs — NEW .NET harness tool
- Program.cs — Register RouteLlmTool
- docs/dev/evalview-setup.md — NEW setup guide

## Success Criteria

1. route-llm responds via tools/call with correct routing recommendation
2. All 5 MCP servers appear in OpenCode's tool list
3. Agent descriptions accurately reference available MCPs
4. Build passes, container restarts healthy
