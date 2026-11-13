import assert from "node:assert/strict";
import { spawn } from "node:child_process";
import { createServer } from "node:http";
import { mkdtempSync, readFileSync, rmSync, writeFileSync } from "node:fs";
import os from "node:os";
import path from "node:path";
import { fileURLToPath } from "node:url";
import test from "node:test";

const repoRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "../..");
const syncScript = path.join(repoRoot, "scripts/openship/sync-service-env.mjs");

async function runSync({ contentEnvironment, overrides, existingEnvironments = {}, checkLive = false, failGetFor = "" }) {
  const tempDir = mkdtempSync(path.join(os.tmpdir(), "micro-openship-sync-"));
  const composePath = path.join(tempDir, "compose.json");
  const overridesPath = path.join(tempDir, "overrides.json");
  const calls = [];
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
          TenantPlacement__Enabled: "false",
          TenantPlacement__Services__content__DefaultConnectionName: "ContentDb",
          Persistence__RunMigrationsOnStartup: "false",
          ...contentEnvironment,
        },
      },
    },
  };

  writeFileSync(composePath, JSON.stringify(compose));
  writeFileSync(overridesPath, JSON.stringify(overrides));
  const services = Object.keys(compose.services)
    .filter((name) => Object.keys(compose.services[name].environment ?? {}).length > 0)
    .map((name) => ({ id: `svc_${name}`, name, kind: "compose" }));
  const project = { id: "proj_micro", slug: "micro", name: "Micro" };

  const server = createServer(async (request, response) => {
    const chunks = [];
    for await (const chunk of request) chunks.push(chunk);
    const body = chunks.length ? JSON.parse(Buffer.concat(chunks).toString("utf8")) : undefined;
    const url = new URL(request.url, "http://localhost");
    calls.push({ method: request.method, path: url.pathname + url.search, body });
    response.setHeader("content-type", "application/json");

    if (request.headers.authorization !== "Bearer test-token") {
      response.writeHead(401).end(JSON.stringify({ error: "unauthorized" }));
    } else if (request.method === "GET" && url.pathname === "/api/proxy/api/v1/projects") {
      response.end(JSON.stringify({ data: [project], total: 1, page: 1, perPage: 100 }));
    } else if (request.method === "GET" && url.pathname === "/api/proxy/api/v1/projects/proj_micro/services") {
      response.end(JSON.stringify({ success: true, services }));
    } else if (request.method === "GET" && url.pathname.endsWith("/env")) {
      const serviceId = url.pathname.split("/").at(-2);
      const serviceName = serviceId.replace(/^svc_/u, "");
      if (serviceName === failGetFor) {
        response.writeHead(403).end(JSON.stringify({ error: "forbidden" }));
      } else {
        response.end(JSON.stringify({
          success: true,
          vars: existingEnvironments[serviceName] ?? [],
        }));
      }
    } else if (request.method === "PUT" && url.pathname.endsWith("/env")) {
      response.end(JSON.stringify({ success: true, count: body.vars.length }));
    } else {
      response.writeHead(404).end(JSON.stringify({ error: "not found" }));
    }
  });

  await new Promise((resolve) => server.listen(0, "127.0.0.1", resolve));
  const address = server.address();
  const apiBase = `http://127.0.0.1:${address.port}/api/proxy`;
  const child = spawn(process.execPath, [syncScript,
    "--project", "micro",
    "--env", "production",
    "--compose-json", composePath,
    "--overrides", overridesPath,
    ...(checkLive ? ["--check-live"] : []),
  ], {
    cwd: repoRoot,
    env: {
      ...process.env,
      OPENSHIP_API_URL: apiBase,
      OPENSHIP_TOKEN: "test-token",
    },
    stdio: ["ignore", "pipe", "pipe"],
  });
  let stdout = "";
  let stderr = "";
  child.stdout.setEncoding("utf8").on("data", (chunk) => { stdout += chunk; });
  child.stderr.setEncoding("utf8").on("data", (chunk) => { stderr += chunk; });
  const result = await new Promise((resolve) => child.on("close", (status) => resolve({ status, stdout, stderr })));
  await new Promise((resolve) => server.close(resolve));
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

test("blocks OpenShip writes when a tenant-mapped service omits its explicit placement switch", async () => {
  const { result, calls } = await runSync({
    contentEnvironment: { TenantPlacement__Enabled: "" },
    overrides: safeOverrides,
  });

  assert.notEqual(result.status, 0);
  assert.match(result.stderr, /contentservice: TenantPlacement__Enabled must be explicitly set to true or false/u);
  assert.deepEqual(calls, []);
});

test("syncs Compose-owned values with per-variable secret metadata through the OpenShip API", async () => {
  const { result, calls } = await runSync({
    contentEnvironment: {},
    overrides: safeOverrides,
  });

  assert.equal(result.status, 0, result.stderr);
  const contentWrite = calls.find(({ method, path }) => method === "PUT" && path.endsWith("svc_contentservice/env"));
  assert.ok(contentWrite);
  assert.equal(contentWrite.body.environment, "production");
  assert.ok(contentWrite.body.vars.some((row) => row.key === "TenantPlacement__Enabled" && row.value === "false" && row.isSecret === false));
  assert.ok(contentWrite.body.vars.some((row) => row.key === "ConnectionStrings__ContentDb" && row.isSecret === true));
  assert.equal(calls.some(({ method }) => method === "POST"), false);
});

test("preserves OpenShip-managed masked secrets by source ID without writing the mask as a value", async () => {
  const masked = "••••••••";
  const { result, calls } = await runSync({
    contentEnvironment: {},
    overrides: safeOverrides,
    existingEnvironments: {
      identityservice: [],
      contentservice: [
        { id: "env_redis_prefix", key: "Redis__KeyPrefix", value: masked, isSecret: true, environment: "production" },
        { id: "env_redis_connection", key: "Redis__ConnectionString", value: masked, isSecret: true, environment: "production" },
      ],
    },
  });

  assert.equal(result.status, 0, result.stderr);
  const contentWrite = calls.find(({ method, path }) => method === "PUT" && path.endsWith("svc_contentservice/env"));
  assert.ok(contentWrite);
  for (const row of contentWrite.body.vars.filter(({ key }) => key.startsWith("Redis__"))) {
    assert.ok(row.sourceId);
    assert.equal(row.value, masked);
    assert.equal(row.isSecret, true);
  }
  assert.equal(result.stdout.includes("env_redis_connection"), false);
});

test("checks the complete live replacement without writing or printing environment values", async () => {
  const privateValue = "project-micro:service-content:production:";
  const { result, calls } = await runSync({
    contentEnvironment: {},
    overrides: safeOverrides,
    checkLive: true,
    existingEnvironments: {
      identityservice: [],
      contentservice: [{ id: "env_prefix", key: "Redis__KeyPrefix", value: "••••••••", isSecret: true, environment: "production" }],
    },
  });

  assert.equal(result.status, 0, result.stderr);
  assert.equal(calls.some(({ method }) => method === "PUT"), false);
  assert.match(result.stdout, /\[check-live\] contentservice: .*variables validated; no changes written/u);
  assert.equal(result.stdout.includes(privateValue), false);
});

test("reports a safe HTTP status and performs no writes when a live service read is denied", async () => {
  const { result, calls } = await runSync({
    contentEnvironment: {},
    overrides: safeOverrides,
    failGetFor: "contentservice",
    existingEnvironments: { identityservice: [], contentservice: [] },
  });

  assert.notEqual(result.status, 0);
  assert.match(result.stderr, /OpenShip service env read failed for contentservice \(HTTP 403\)/u);
  assert.equal(calls.some(({ method }) => method === "PUT"), false);
});

test("blocks all writes when an existing service env key is not declared by Compose or OpenShip", async () => {
  const { result, calls } = await runSync({
    contentEnvironment: {},
    overrides: safeOverrides,
    existingEnvironments: {
      identityservice: [],
      contentservice: [{ id: "env_unknown", key: "UntrackedRuntimeSetting", value: "••••••••", isSecret: true, environment: "production" }],
    },
  });

  assert.notEqual(result.status, 0);
  assert.match(result.stderr, /existing environment keys are not declared or OpenShip-managed; add them to Compose\/overrides before syncing: contentservice\.UntrackedRuntimeSetting/u);
  assert.equal(calls.some(({ method }) => method === "PUT"), false);
});

test("blocks OpenShip writes when an environment override targets a missing Compose service", async () => {
  const { result, calls } = await runSync({
    contentEnvironment: {},
    overrides: {
      ...safeOverrides,
      services: { ...safeOverrides.services, staleService: { FEATURE_FLAG: "true" } },
    },
  });

  assert.notEqual(result.status, 0);
  assert.match(result.stderr, /environment overrides target missing or non-build Compose services: staleService/u);
  assert.deepEqual(calls, []);
});

test("blocks OpenShip writes when a common override no longer exists in any Compose service", async () => {
  const { result, calls } = await runSync({
    contentEnvironment: {},
    overrides: { ...safeOverrides, common: { StaleCommonSetting: "value" } },
  });

  assert.notEqual(result.status, 0);
  assert.match(result.stderr, /common environment overrides are not declared by any build service: StaleCommonSetting/u);
  assert.deepEqual(calls, []);
});

test("blocks every OpenShip write when a configured secret override is still redacted", async () => {
  const { result, calls } = await runSync({
    contentEnvironment: {},
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
