import assert from "node:assert/strict";
import { spawnSync } from "node:child_process";
import { mkdtempSync, readFileSync, rmSync, writeFileSync } from "node:fs";
import os from "node:os";
import path from "node:path";
import { fileURLToPath } from "node:url";
import test from "node:test";

const repoRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "../..");
const syncScript = path.join(repoRoot, "scripts/openship/sync-service-env.mjs");

function runSync({ contentEnvironment, overrides }) {
  const tempDir = mkdtempSync(path.join(os.tmpdir(), "micro-openship-sync-"));
  const composePath = path.join(tempDir, "compose.json");
  const overridesPath = path.join(tempDir, "overrides.json");
  const fakeOpenShipPath = path.join(tempDir, "fake-openship.mjs");
  const callsPath = path.join(tempDir, "calls.jsonl");
  const compose = {
    services: {
      identityservice: {
        build: { context: ".." },
        environment: {
          TenantPlacement__Enabled: "true",
          TenantPlacement__Services__identity__DefaultConnectionName: "IdentityDb",
          Persistence__RunMigrationsOnStartup: "false",
        },
      },
      contentservice: {
        build: { context: "../src" },
        environment: {
          TenantPlacement__Services__content__DefaultConnectionName: "ContentDb",
          Persistence__RunMigrationsOnStartup: "false",
          ...contentEnvironment,
        },
      },
    },
  };

  writeFileSync(composePath, JSON.stringify(compose));
  writeFileSync(overridesPath, JSON.stringify(overrides));
  writeFileSync(fakeOpenShipPath, `
    import { appendFileSync } from "node:fs";
    appendFileSync(process.env.OPENSHIP_TEST_CALLS, JSON.stringify(process.argv.slice(2)) + "\\n");
  `);

  const result = spawnSync(process.execPath, [syncScript,
    "--project", "micro",
    "--env", "production",
    "--compose-json", composePath,
    "--overrides", overridesPath,
  ], {
    cwd: repoRoot,
    encoding: "utf8",
    env: {
      ...process.env,
      OPENSHIP_BIN: process.execPath,
      OPENSHIP_BIN_ARGS: fakeOpenShipPath,
      OPENSHIP_TEST_CALLS: callsPath,
    },
  });

  const calls = callsPath && (() => {
    try {
      return readFileSync(callsPath, "utf8").trim().split(/\r?\n/u).filter(Boolean).map((line) => JSON.parse(line));
    } catch {
      return [];
    }
  })();
  rmSync(tempDir, { recursive: true, force: true });
  return { result, calls };
}

const safeOverrides = {
  services: {
    identityservice: {
      Identity__BootstrapAdmin__Password: "test-bootstrap-password",
      OpenIddict__Issuer: "https://identity-api.example.test/",
    },
    contentservice: {
      ConnectionStrings__ContentDb: "Host=database.example.test;Database=content;Username=test;Password=test",
    },
  },
};

test("blocks OpenShip writes when a tenant-mapped service omits its explicit placement switch", () => {
  const { result, calls } = runSync({
    contentEnvironment: {},
    overrides: safeOverrides,
  });

  assert.notEqual(result.status, 0);
  assert.match(result.stderr, /contentservice: TenantPlacement__Enabled must be explicitly set to true or false/u);
  assert.deepEqual(calls, []);
});

test("syncs the explicit Compose placement value to OpenShip with the rest of the service env", () => {
  const { result, calls } = runSync({
    contentEnvironment: { TenantPlacement__Enabled: "false" },
    overrides: safeOverrides,
  });

  assert.equal(result.status, 0, result.stderr);
  assert.equal(calls.length, 2);
  const contentCall = calls.find((args) => args.includes("contentservice"));
  assert.ok(contentCall);
  assert.ok(contentCall.includes("TenantPlacement__Enabled=false"));
  assert.ok(contentCall.includes("TenantPlacement__Services__content__DefaultConnectionName=ContentDb"));
});

test("blocks OpenShip writes when an environment override targets a missing Compose service", () => {
  const { result, calls } = runSync({
    contentEnvironment: { TenantPlacement__Enabled: "false" },
    overrides: {
      ...safeOverrides,
      services: { ...safeOverrides.services, staleService: { FEATURE_FLAG: "true" } },
    },
  });

  assert.notEqual(result.status, 0);
  assert.match(result.stderr, /environment overrides target missing or non-build Compose services: staleService/u);
  assert.deepEqual(calls, []);
});

test("blocks OpenShip writes when a common override no longer exists in any Compose service", () => {
  const { result, calls } = runSync({
    contentEnvironment: { TenantPlacement__Enabled: "false" },
    overrides: { ...safeOverrides, common: { StaleCommonSetting: "value" } },
  });

  assert.notEqual(result.status, 0);
  assert.match(result.stderr, /common environment overrides are not declared by any build service: StaleCommonSetting/u);
  assert.deepEqual(calls, []);
});

test("blocks every OpenShip write when a configured secret override is still redacted", () => {
  const { result, calls } = runSync({
    contentEnvironment: { TenantPlacement__Enabled: "false" },
    overrides: {
      ...safeOverrides,
      services: {
        ...safeOverrides.services,
        contentservice: { ConnectionStrings__ContentDb: "........" },
      },
    },
  });

  assert.notEqual(result.status, 0);
  assert.match(result.stderr, /contentservice: compose placeholder values must be overridden for: ConnectionStrings__ContentDb/u);
  assert.deepEqual(calls, []);
});

