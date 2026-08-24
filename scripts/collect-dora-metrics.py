#!/usr/bin/env python3
"""Collect auditable DORA metrics from GitHub Actions promotion runs.

The collector is intentionally dependency-free so it can run on a hosted
GitHub runner. It emits both OpenMetrics (for a push/scrape adapter) and JSON
evidence. No token or response body is written to either output.
"""

from __future__ import annotations

import argparse
import datetime as dt
import json
import os
import sys
import urllib.error
import urllib.parse
import urllib.request
from pathlib import Path
from typing import Any


WORKFLOW_FILE = ".github/workflows/gitops-release-promotion.yml"
SERVICE = "his-hope"
ENVIRONMENT = "production"


def parse_time(value: str) -> dt.datetime:
    return dt.datetime.fromisoformat(value.replace("Z", "+00:00"))


def github_get(url: str, token: str) -> dict[str, Any]:
    request = urllib.request.Request(
        url,
        headers={
            "Accept": "application/vnd.github+json",
            "Authorization": f"Bearer {token}",
            "X-GitHub-Api-Version": "2022-11-28",
            "User-Agent": "his-hope-dora-metrics/1",
        },
    )
    try:
        with urllib.request.urlopen(request, timeout=20) as response:
            return json.load(response)
    except (urllib.error.HTTPError, urllib.error.URLError) as exc:
        raise RuntimeError(f"GitHub API request failed: {exc}") from exc


def workflow_runs(api_root: str, repository: str, token: str, since: dt.datetime) -> list[dict[str, Any]]:
    query = urllib.parse.urlencode({"per_page": "100", "created": f">={since.date().isoformat()}"})
    url = f"{api_root.rstrip('/')}/repos/{repository}/actions/workflows/{urllib.parse.quote(WORKFLOW_FILE, safe='')}/runs?{query}"
    payload = github_get(url, token)
    return [run for run in payload.get("workflow_runs", []) if run.get("event") != "pull_request"]


def commit_time(api_root: str, repository: str, sha: str, token: str, cache: dict[str, dt.datetime]) -> dt.datetime:
    if sha not in cache:
        payload = github_get(f"{api_root.rstrip('/')}/repos/{repository}/commits/{sha}", token)
        date_value = payload.get("commit", {}).get("author", {}).get("date")
        if not date_value:
            raise RuntimeError(f"Commit {sha} has no author timestamp")
        cache[sha] = parse_time(date_value)
    return cache[sha]


def calculate(runs: list[dict[str, Any]], api_root: str, repository: str, token: str, since: dt.datetime) -> dict[str, Any]:
    completed = [run for run in runs if run.get("status") == "completed" and run.get("conclusion") in {"success", "failure", "cancelled", "timed_out"}]
    completed.sort(key=lambda run: parse_time(run["updated_at"]))
    successes = [run for run in completed if run.get("conclusion") == "success"]
    failures = [run for run in completed if run.get("conclusion") != "success"]
    cache: dict[str, dt.datetime] = {}
    lead_times = [
        max(0.0, (parse_time(run["updated_at"]) - commit_time(api_root, repository, run["head_sha"], token, cache)).total_seconds())
        for run in successes
        if run.get("head_sha") and run.get("updated_at")
    ]

    recovery_times: list[float] = []
    for failure in failures:
        failure_time = parse_time(failure["updated_at"])
        recovery = next((run for run in successes if parse_time(run["updated_at"]) > failure_time), None)
        if recovery:
            recovery_times.append((parse_time(recovery["updated_at"]) - failure_time).total_seconds())

    window_days = max((dt.datetime.now(dt.timezone.utc) - since).total_seconds() / 86400.0, 1.0)
    total = len(completed)
    values = {
        "deployment_count": len(successes),
        "deployment_frequency_per_day": len(successes) / window_days,
        "lead_time_seconds_p50": percentile(lead_times, 0.50),
        "lead_time_seconds_p95": percentile(lead_times, 0.95),
        "change_failure_rate_ratio": len(failures) / total if total else 0.0,
        "mttr_seconds_p50": percentile(recovery_times, 0.50),
        "mttr_seconds_p95": percentile(recovery_times, 0.95),
    }
    return {
        "schemaVersion": 1,
        "service": SERVICE,
        "environment": ENVIRONMENT,
        "windowStart": since.isoformat(),
        "windowDays": window_days,
        "completedPromotionRuns": total,
        "successfulPromotionRuns": len(successes),
        "failedPromotionRuns": len(failures),
        "recoveredFailures": len(recovery_times),
        "values": values,
        "sourceWorkflow": WORKFLOW_FILE,
        "generatedAtUtc": dt.datetime.now(dt.timezone.utc).isoformat(),
    }


def percentile(values: list[float], quantile: float) -> float:
    if not values:
        return 0.0
    ordered = sorted(values)
    index = min(len(ordered) - 1, max(0, int(round((len(ordered) - 1) * quantile))))
    return ordered[index]


def escape_label(value: str) -> str:
    return value.replace("\\", "\\\\").replace('"', '\\"').replace("\n", "\\n")


def openmetrics(evidence: dict[str, Any]) -> str:
    labels = f'service="{escape_label(evidence["service"])}",environment="{escape_label(evidence["environment"])}"'
    values = evidence["values"]
    lines = [
        "# HELP pipeline_deployments_total Completed successful production deployments in the collection window.",
        "# TYPE pipeline_deployments_total gauge",
        f'pipeline_deployments_total{{{labels},status="success"}} {values["deployment_count"]}',
        "# HELP pipeline_deployment_frequency_per_day Successful deployments per day in the collection window.",
        "# TYPE pipeline_deployment_frequency_per_day gauge",
        f"pipeline_deployment_frequency_per_day{{{labels}}} {values['deployment_frequency_per_day']:.6f}",
        "# HELP pipeline_lead_time_seconds Time from commit authoring to successful production promotion.",
        "# TYPE pipeline_lead_time_seconds gauge",
        f"pipeline_lead_time_seconds{{{labels},quantile=\"0.50\"}} {values['lead_time_seconds_p50']:.3f}",
        f"pipeline_lead_time_seconds{{{labels},quantile=\"0.95\"}} {values['lead_time_seconds_p95']:.3f}",
        "# HELP pipeline_change_failure_rate_ratio Failed promotion runs divided by completed promotion runs.",
        "# TYPE pipeline_change_failure_rate_ratio gauge",
        f"pipeline_change_failure_rate_ratio{{{labels}}} {values['change_failure_rate_ratio']:.6f}",
        "# HELP pipeline_mttr_seconds Time from a failed promotion to the next successful promotion.",
        "# TYPE pipeline_mttr_seconds gauge",
        f"pipeline_mttr_seconds{{{labels},quantile=\"0.50\"}} {values['mttr_seconds_p50']:.3f}",
        f"pipeline_mttr_seconds{{{labels},quantile=\"0.95\"}} {values['mttr_seconds_p95']:.3f}",
        "# HELP dora_metrics_collection_timestamp_seconds Unix timestamp when DORA metrics were generated.",
        "# TYPE dora_metrics_collection_timestamp_seconds gauge",
        f"dora_metrics_collection_timestamp_seconds{{{labels}}} {parse_time(evidence['generatedAtUtc']).timestamp():.3f}",
        "",
    ]
    return "\n".join(lines)


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--output-dir", default="artifacts/dora")
    parser.add_argument("--days", type=int, default=30)
    parser.add_argument("--repository", default=os.getenv("GITHUB_REPOSITORY", ""))
    parser.add_argument("--api-root", default="https://api.github.com")
    args = parser.parse_args()
    token = os.getenv("GITHUB_TOKEN")
    if not token:
        print("GITHUB_TOKEN is required", file=sys.stderr)
        return 2
    if not args.repository:
        print("--repository or GITHUB_REPOSITORY is required", file=sys.stderr)
        return 2
    if args.days < 1 or args.days > 90:
        print("--days must be between 1 and 90", file=sys.stderr)
        return 2

    since = dt.datetime.now(dt.timezone.utc) - dt.timedelta(days=args.days)
    runs = workflow_runs(args.api_root, args.repository, token, since)
    evidence = calculate(runs, args.api_root, args.repository, token, since)
    output_dir = Path(args.output_dir)
    output_dir.mkdir(parents=True, exist_ok=True)
    (output_dir / "dora-metrics.json").write_text(json.dumps(evidence, indent=2) + "\n", encoding="utf-8")
    (output_dir / "dora-metrics.prom").write_text(openmetrics(evidence), encoding="utf-8")
    print(json.dumps({"status": "pass", **evidence}, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
