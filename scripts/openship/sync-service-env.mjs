#!/usr/bin/env node
// Pushes the per-service production environment of the Micro compose stack to
// Openship. Openship's K3s deploy for project "micro" refuses to run unless every
// managed key of every service has an explicit, safe stored value, so the compose
// defaults (Development, localhost, static tokens, seed data, ...) are replaced here.
import { execFileSync, spawnSync } from "node:child_process";
import { readFileSync } from "node:fs";
import { resolve } from "node:path";

const UNSET = "__OPENSHIP_UNSET__";
const REDACTED_VALUES = new Set(["••••••••", "........", "********", "<redacted>"]);
const REQUIRED_IDENTITY_ENV = [
  "Identity__BootstrapAdmin__Password",
  "OpenIddict__Issuer",
];
const REQUIRED_COMPOSE_VARS = [
  "MANUFACTURING_POSTGRES_USER",
  "MANUFACTURING_POSTGRES_PASSWORD",
  "MANUFACTURING_RABBITMQ_USER",
  "MANUFACTURING_RABBITMQ_PASSWORD",
];
// These keys are owned by OpenShip bindings and may rotate independently of
// the Compose source. Keep their existing values when replacing Compose env.
const OPENSHIP_MANAGED_ENV_KEYS = new Set([
  "DATABASE_URL",
  "OPENSHIP_DATABASE_CA_PEM",
  "PLATFORM_PKI_FILE_PG_CA",
  "PLATFORM_PKI_FILE_PG_CLIENT_CRT",
  "PLATFORM_PKI_FILE_PG_CLIENT_KEY",
  "Redis__ConnectionString",
  "Redis__KeyPrefix",
  "Redis__TlsCaPem",
  "Redis__TlsClientCertificatePem",
  "Redis__TlsClientPrivateKeyPem",
  "EventBus__Host",
  "EventBus__CaPem",
  "EventBus__ClientCertificatePem",
  "EventBus__ClientPrivateKeyPem",
]);

function parseArgs(argv) {
  const opts = {
    project: "micro",
    environment: "production",
    compose: "docker/docker-compose.openship.yml",
    overrides: "docker/openship/service-env.production.json",
    composeJson: undefined,
    dryRun: false,
    checkLive: false,
  };
  for (let i = 0; i < argv.length; i++) {
    const arg = argv[i];
    if (arg === "--dry-run") opts.dryRun = true;
    else if (arg === "--check-live") opts.checkLive = true;
    else if (arg === "--project") opts.project = argv[++i];
    else if (arg === "--env") opts.environment = argv[++i];
    else if (arg === "--compose") opts.compose = argv[++i];
    else if (arg === "--overrides") opts.overrides = argv[++i];
    else if (arg === "--compose-json") opts.composeJson = argv[++i];
    else throw new Error(`Unknown argument: ${arg}`);
  }
  return opts;
}

function resolvePlaceholders(value, context) {
  return value.replace(/\$\{([A-Za-z_][A-Za-z0-9_]*)(?::-([^}]*))?\}/g, (_, name, fallback) => {
    const actual = process.env[name];
    if (actual !== undefined && actual !== "") return actual;
    if (fallback !== undefined) return fallback;
    throw new Error(`${context}: environment variable ${name} is required but not set.`);
  });
}

function loadComposeServices(opts) {
  if (opts.composeJson) return JSON.parse(readFileSync(opts.composeJson, "utf8")).services;
  const env = { ...process.env };
  for (const name of REQUIRED_COMPOSE_VARS) env[name] ??= UNSET;
  const json = execFileSync(
    "docker",
    ["compose", "-f", opts.compose, "config", "--format", "json"],
    { env, encoding: "utf8", maxBuffer: 64 * 1024 * 1024 },
  );
  return JSON.parse(json).services;
}

function buildServiceEnvironments(composeServices, overrides) {
  const buildServiceNames = Object.entries(composeServices)
    .filter(([, service]) => service.build)
    .map(([name]) => name);
  const buildServiceNameSet = new Set(buildServiceNames);
  const staleServiceOverrides = Object.keys(overrides.services ?? {})
    .filter((name) => !buildServiceNameSet.has(name))
    .sort();
  if (staleServiceOverrides.length > 0) {
    throw new Error(
      `environment overrides target missing or non-build Compose services: ${staleServiceOverrides.join(", ")}`,
    );
  }

  const declaredEnvironmentKeys = new Set(buildServiceNames.flatMap((name) =>
    Object.keys(composeServices[name].environment ?? {}),
  ));
  const staleCommonOverrides = Object.keys(overrides.common ?? {})
    .filter((key) => !declaredEnvironmentKeys.has(key))
    .sort();
  if (staleCommonOverrides.length > 0) {
    throw new Error(
      `common environment overrides are not declared by any build service: ${staleCommonOverrides.join(", ")}`,
    );
  }

  const result = {};
  for (const [name, service] of Object.entries(composeServices)) {
    // Replaced platform services (postgres/redis/rabbitmq/key init) are image-only.
    if (!service.build) continue;
    const environment = { ...(service.environment ?? {}) };
    for (const [key, template] of Object.entries(overrides.common ?? {})) {
      if (key in environment) environment[key] = resolvePlaceholders(template, `${name}.${key}`);
    }
    for (const [key, template] of Object.entries(overrides.addAll ?? {})) {
      if (Object.keys(environment).length > 0) environment[key] = resolvePlaceholders(template, `${name}.${key}`);
    }
    for (const [key, template] of Object.entries(overrides.services?.[name] ?? {})) {
      environment[key] = resolvePlaceholders(template, `${name}.${key}`);
    }
    const hasTenantPlacementMappings = Object.keys(environment)
      .some((key) => key.startsWith("TenantPlacement__Services__"));
    if (hasTenantPlacementMappings && !["true", "false"].includes(
      String(environment.TenantPlacement__Enabled ?? "").toLowerCase(),
    )) {
      throw new Error(
        `${name}: TenantPlacement__Enabled must be explicitly set to true or false when tenant service mappings are declared.`,
      );
    }
    const unresolved = Object.entries(environment)
      .filter(([, value]) => {
        const text = String(value);
        return text.includes(UNSET) || REDACTED_VALUES.has(text.trim());
      })
      .map(([key]) => key);
    if (unresolved.length > 0) {
      throw new Error(`${name}: compose placeholder values must be overridden for: ${unresolved.join(", ")}`);
    }
    if (Object.keys(environment).length > 0) result[name] = environment;
  }

  const identityEnvironment = result.identityservice ?? {};
  const missingIdentity = REQUIRED_IDENTITY_ENV.filter((key) => {
    const value = identityEnvironment[key];
    return typeof value !== "string" || value.trim() === "" || REDACTED_VALUES.has(value.trim());
  });
  if (missingIdentity.length > 0) {
    throw new Error(
      `identityservice: required production environment values are missing or redacted: ${missingIdentity.join(", ")}`,
    );
  }

  return result;
}

function openShipCommand() {
  const bin = process.env.OPENSHIP_BIN || "openship";
  const prefix = process.env.OPENSHIP_BIN_ARGS ? process.env.OPENSHIP_BIN_ARGS.split(" ") : [];
  return { bin, prefix };
}

function readExistingServiceEnvironment(service, opts) {
  const { bin, prefix } = openShipCommand();
  const result = spawnSync(
    bin,
    [...prefix, "service", "env", "get", service, "-p", opts.project, "-e", opts.environment],
    {
      encoding: "utf8",
      env: { ...process.env, OPENSHIP_JSON: "1" },
      maxBuffer: 16 * 1024 * 1024,
    },
  );
  if (result.status !== 0) {
    throw new Error(`openship service env get failed for ${service} (exit ${result.status}).`);
  }
  let rows;
  try {
    rows = JSON.parse(result.stdout);
  } catch {
    throw new Error(`openship service env get returned invalid JSON for ${service}.`);
  }
  if (!Array.isArray(rows)) throw new Error(`openship service env get returned an invalid environment for ${service}.`);
  return rows;
}

function mergeOpenShipManagedEnvironment(composeEnvironments, existingByService) {
  const merged = {};
  const unknown = [];
  for (const [service, desired] of Object.entries(composeEnvironments)) {
    const environment = { ...desired };
    const existing = existingByService[service] ?? [];
    for (const row of existing) {
      if (!row || typeof row.key !== "string" || typeof row.value !== "string") {
        throw new Error(`openship service env get returned an invalid variable for ${service}.`);
      }
      if (!Object.hasOwn(desired, row.key) && !OPENSHIP_MANAGED_ENV_KEYS.has(row.key)) {
        unknown.push(`${service}.${row.key}`);
        continue;
      }
      if (OPENSHIP_MANAGED_ENV_KEYS.has(row.key)) environment[row.key] = row.value;
    }
    merged[service] = environment;
  }
  if (unknown.length > 0) {
    throw new Error(
      `existing environment keys are not declared or OpenShip-managed; add them to Compose/overrides before syncing: ${unknown.sort().join(", ")}`,
    );
  }
  return merged;
}

function main() {
  const opts = parseArgs(process.argv.slice(2));
  if (opts.checkLive && opts.dryRun) throw new Error("--check-live cannot be combined with --dry-run.");
  const overrides = JSON.parse(readFileSync(resolve(opts.overrides), "utf8"));
  const composeEnvironments = buildServiceEnvironments(loadComposeServices(opts), overrides);
  const existingByService = {};
  if (!opts.dryRun) {
    // Read every service and validate the whole replacement before the first
    // write, so an unknown runtime value cannot cause a partial sync.
    for (const service of Object.keys(composeEnvironments)) {
      existingByService[service] = readExistingServiceEnvironment(service, opts);
    }
  }
  const environments = opts.dryRun
    ? composeEnvironments
    : mergeOpenShipManagedEnvironment(composeEnvironments, existingByService);

  if (opts.checkLive) {
    for (const [service, environment] of Object.entries(environments)) {
      console.log(`[check-live] ${service}: ${Object.keys(environment).length} variables validated; no changes written`);
    }
    return;
  }

  for (const [service, environment] of Object.entries(environments)) {
    const pairs = Object.entries(environment).map(([key, value]) => `${key}=${value}`);
    console.log(`${opts.dryRun ? "[dry-run] " : ""}${service}: ${pairs.length} variables`);
    if (opts.dryRun) {
      console.log(`  keys: ${Object.keys(environment).join(", ")}`);
      continue;
    }
    // spawnSync (not execFileSync) so a failure never echoes the secret-bearing argv.
    // OPENSHIP_BIN / OPENSHIP_BIN_ARGS let Windows callers run `node <cli>/dist/node-entry.js` instead of the .cmd shim.
    const { bin, prefix } = openShipCommand();
    const { status } = spawnSync(
      bin,
      [...prefix, "service", "env", "set", service, ...pairs, "-p", opts.project, "-e", opts.environment, "--replace", "--secret"],
      { stdio: "inherit" },
    );
    if (status !== 0) throw new Error(`openship service env set failed for ${service} (exit ${status}).`);
  }
}

try {
  main();
} catch (error) {
  console.error(error instanceof Error ? error.message.split("\n")[0] : String(error));
  process.exit(1);
}
