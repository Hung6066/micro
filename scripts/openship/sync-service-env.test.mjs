import assert from "node:assert/strict";
import { spawnSync } from "node:child_process";
import { mkdtempSync, readFileSync, rmSync, writeFileSync } from "node:fs";
import os from "node:os";
import path from "node:path";
import { fileURLToPath } from "node:url";
import test from "node:test";

const repoRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "../..");
const syncScript = path.join(repoRoot, "scripts/openship/sync-service-env.mjs");

function runSync({ contentEnvironment, overrides, existingEnvironments = {}, checkLive = false, failGetFor = "" }) {
  const tempDir = mkdtempSync(path.join(os.tmpdir(), "micro-openship-sync-"));
  const composePath = path.join(tempDir, "compose.json");
  const overridesPath = path.join(tempDir, "overrides.json");
  const fakeOpenShipPath = path.join(tempDir, "fake-openship.mjs");
  const callsPath = path.join(tempDir, "calls.jsonl");
  const initialEnvironmentsPath = path.join(tempDir, "existing-env.json");
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
  writeFileSync(initialEnvironmentsPath, JSON.stringify(existingEnvironments));
  writeFileSync(fakeOpenShipPath, `
    import { appendFileSync, readFileSync } from "node:fs";
    const args = process.argv.slice(2);
    const existing = JSON.parse(readFileSync(process.env.OPENSHIP_TEST_EXISTING, "utf8"));
    const operation = args.slice(0, 3).join(" ");
    if (operation === "service env get") {
      const service = args[3];
      if (service === process.env.OPENSHIP_TEST_FAIL_GET_FOR) {
        process.stderr.write("  API error: permission denied (403)\\n");
        process.exit(1);
      }
      process.stdout.write(JSON.stringify(existing[service] ?? []));
    } else if (operation === "service env set") {
      const service = args[3];
      const pairs = [];
      for (let i = 4; i < args.length && !args[i].startsWith("-p"); i++) {
        const separator = args[i].indexOf("=");
        if (separator > 0) pairs.push([args[i].slice(0, separator), args[i].slice(separator + 1)]);
      }
      appendFileSync(process.env.OPENSHIP_TEST_CALLS, JSON.stringify({service, pairs}) + "\\n");
    }
  `);

  const result = spawnSync(process.execPath, [syncScript,
    "--project", "micro",
    "--env", "production",
    "--compose-json", composePath,
    "--overrides", overridesPath,
    ...(checkLive ? ["--check-live"] : []),
  ], {
    cwd: repoRoot,
    encoding: "utf8",
    env: {
      ...process.env,
      OPENSHIP_BIN: process.execPath,
      OPENSHIP_BIN_ARGS: fakeOpenShipPath,
      OPENSHIP_TEST_CALLS: callsPath,
      OPENSHIP_TEST_EXISTING: initialEnvironmentsPath,
      OPENSHIP_TEST_FAIL_GET_FOR: failGetFor,
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
  const contentCall = calls.find(({ service }) => service === "contentservice");
  assert.ok(contentCall);
  assert.ok(contentCall.pairs.some(([key, value]) => key === "TenantPlacement__Enabled" && value === "false"));
  assert.ok(contentCall.pairs.some(([key, value]) => key === "TenantPlacement__Services__content__DefaultConnectionName" && value === "ContentDb"));
});

test("preserves OpenShip-managed Redis binding values when replacing Compose-owned env", () => {
  const redisPrefix = "project-micro:service-content:production:";
  const { result, calls } = runSync({
    contentEnvironment: { TenantPlacement__Enabled: "false" },
    overrides: safeOverrides,
    existingEnvironments: {
      identityservice: [],
      contentservice: [
        { key: "Redis__KeyPrefix", value: "••••••••", isSecret: true },
        { key: "Redis__ConnectionString", value: "••••••••", isSecret: true },
      ],
    },
  });

  assert.equal(result.status, 0, result.stderr);
  const contentCall = calls.find(({ service }) => service === "contentservice");
  assert.ok(contentCall);
  assert.ok(contentCall.pairs.some(([key, value]) => key === "Redis__KeyPrefix" && value === "••••••••"));
  assert.ok(contentCall.pairs.some(([key, value]) => key === "Redis__ConnectionString" && value === "••••••••"));
  assert.equal(result.stdout.includes(redisPrefix), false);
});

test("checks the complete live replacement without writing or printing environment values", () => {
  const { result, calls } = runSync({
    contentEnvironment: { TenantPlacement__Enabled: "false" },
    overrides: safeOverrides,
    checkLive: true,
    existingEnvironments: {
      identityservice: [],
      contentservice: [{ key: "Redis__KeyPrefix", value: "project-micro:service-content:production:", isSecret: true }],
    },
  });

  assert.equal(result.status, 0, result.stderr);
  assert.deepEqual(calls, []);
  assert.match(result.stdout, /\[check-live\] contentservice: .*variables validated; no changes written/u);
  assert.equal(result.stdout.includes("project-micro:service-content:production:"), false);
});

test("reports a safe HTTP status and performs no writes when a live service read is denied", () => {
  const { result, calls } = runSync({
    contentEnvironment: { TenantPlacement__Enabled: "false" },
    overrides: safeOverrides,
    failGetFor: "contentservice",
    existingEnvironments: { identityservice: [], contentservice: [] },
  });

  assert.notEqual(result.status, 0);
  assert.match(result.stderr, /openship service env get failed for contentservice \(HTTP 403\)/u);
  assert.deepEqual(calls, []);
});

test("blocks all writes when an existing service env key is not declared by Compose or managed by OpenShip", () => {
  const { result, calls } = runSync({
    contentEnvironment: { TenantPlacement__Enabled: "false" },
    overrides: safeOverrides,
    existingEnvironments: {
      identityservice: [],
      contentservice: [{ key: "UntrackedRuntimeSetting", value: "••••••••", isSecret: true }],
    },
  });

  assert.notEqual(result.status, 0);
  assert.match(result.stderr, /existing environment keys are not declared or OpenShip-managed; add them to Compose\/overrides before syncing: contentservice\.UntrackedRuntimeSetting/u);
  assert.deepEqual(calls, []);
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

