import importlib.util
import pathlib
import unittest


MODULE_PATH = pathlib.Path(__file__).parents[1] / "collect-dora-metrics.py"
spec = importlib.util.spec_from_file_location("collect_dora_metrics", MODULE_PATH)
module = importlib.util.module_from_spec(spec)
assert spec.loader is not None
spec.loader.exec_module(module)


class DoraMetricsTests(unittest.TestCase):
    def test_calculates_failure_rate_lead_time_and_recovery(self):
        runs = [
            {"status": "completed", "conclusion": "failure", "updated_at": "2026-08-10T01:00:00Z", "head_sha": "bad"},
            {"status": "completed", "conclusion": "success", "updated_at": "2026-08-10T02:00:00Z", "head_sha": "good-1"},
            {"status": "completed", "conclusion": "success", "updated_at": "2026-08-10T03:00:00Z", "head_sha": "good-2"},
        ]
        original = module.github_get
        module.github_get = lambda url, token: {"commit": {"author": {"date": "2026-08-10T00:30:00Z"}}}
        try:
            evidence = module.calculate(
                runs,
                "https://api.github.com",
                "org/repo",
                "redacted-test-token",
                module.parse_time("2026-08-09T00:00:00Z"),
            )
        finally:
            module.github_get = original

        self.assertEqual(evidence["successfulPromotionRuns"], 2)
        self.assertEqual(evidence["failedPromotionRuns"], 1)
        self.assertEqual(evidence["recoveredFailures"], 1)
        self.assertAlmostEqual(evidence["values"]["change_failure_rate_ratio"], 1 / 3)
        self.assertEqual(evidence["values"]["mttr_seconds_p50"], 3600)
        self.assertIn("pipeline_mttr_seconds", module.openmetrics(evidence))


if __name__ == "__main__":
    unittest.main()
