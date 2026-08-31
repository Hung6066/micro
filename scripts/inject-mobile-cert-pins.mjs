#!/usr/bin/env node
/**
 * Compatibility wrapper for the canonical mobile release preparation flow.
 *
 * The old injector only updated one app and omitted native network-security
 * artifacts. Keep the legacy command safe by delegating to the single
 * fail-closed implementation used by CI for both mobile applications.
 */
import { spawnSync } from "node:child_process";

const legacyPins = process.env.HISHOPE_CERT_PINS?.trim();
const environment = {
  ...process.env,
  ...(legacyPins ? { HISHOPE_CERTIFICATE_PINS_JSON: legacyPins } : {}),
};

const result = spawnSync(
  process.execPath,
  ["scripts/prepare-mobile-release.mjs"],
  { stdio: "inherit", env: environment },
);

if (result.error) {
  console.error(`Mobile release preparation could not start: ${result.error.message}`);
  process.exit(1);
}

process.exit(result.status ?? 1);
