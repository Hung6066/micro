import importlib.util
import pathlib
import unittest


ROOT = pathlib.Path(__file__).resolve().parents[2]
SPEC = importlib.util.spec_from_file_location(
    "validate_azure_blob_retention",
    ROOT / "scripts" / "validate-azure-blob-retention.py",
)
MODULE = importlib.util.module_from_spec(SPEC)
assert SPEC.loader is not None
SPEC.loader.exec_module(MODULE)


class AzureBlobRetentionTests(unittest.TestCase):
    def test_locked_policy_with_minimum_retention_passes(self):
        body = b"""
        <ImmutabilityPolicy>
          <ImmutabilityPeriodSinceCreationInDays>30</ImmutabilityPeriodSinceCreationInDays>
          <PolicyMode>Locked</PolicyMode>
        </ImmutabilityPolicy>
        """
        MODULE.validate_policy(body, 30)

    def test_unlocked_policy_is_rejected(self):
        body = b"""
        <ImmutabilityPolicy>
          <ImmutabilityPeriodSinceCreationInDays>365</ImmutabilityPeriodSinceCreationInDays>
          <PolicyMode>Unlocked</PolicyMode>
        </ImmutabilityPolicy>
        """
        with self.assertRaises(ValueError):
            MODULE.validate_policy(body, 30)

    def test_short_retention_is_rejected(self):
        body = b"""
        <ImmutabilityPolicy>
          <ImmutabilityPeriodSinceCreationInDays>7</ImmutabilityPeriodSinceCreationInDays>
          <PolicyMode>Locked</PolicyMode>
        </ImmutabilityPolicy>
        """
        with self.assertRaises(ValueError):
            MODULE.validate_policy(body, 30)


if __name__ == "__main__":
    unittest.main()
