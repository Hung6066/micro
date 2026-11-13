#!/usr/bin/env node
// Pushes the per-service production environment of the Micro compose stack to
// Openship. Openship's K3s deploy for project "micro" refuses to run unless every
// managed key of every service has an explicit, safe stored value, so the compose
// defaults (Development, localhost, static tokens, seed data, ...) are replaced here.
import { execFileSync } from "node:child_process";
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

function openShipApiBase() {
  const configured = process.env.OPENSHIP_API_URL?.trim();
  if (!configured) throw new Error("OPENSHIP_API_URL is required for live environment sync.");
  let base;
  try {
    base = new URL(configured);
  } catch {
    throw new Error("OPENSHIP_API_URL must be a valid HTTP(S) URL.");
  }
  if (!new Set(["http:", "https:"]).has(base.protocol) || base.username || base.password) {
    throw new Error("OPENSHIP_API_URL must be a credential-free HTTP(S) URL.");
  }
  return configured.replace(/\/+$/u, "");
}

async function openShipRequest(path, operation, target) {
  const token = process.env.OPENSHIP_TOKEN?.trim();
  if (!token) throw new Error("OPENSHIP_TOKEN is required for live environment sync.");
  let response;
  try {
    response = await fetch(`${openShipApiBase()}/api/v1${path}`, {
      headers: { Authorization: `Bearer ${token}`, Accept: "application/json" },
      signal: AbortSignal.timeout(15_000),
    });
  } catch {
    throw new Error(`OpenShip ${operation} failed for ${target} (transport error).`);
  }
  if (!response.ok) throw new Error(`OpenShip ${operation} failed for ${target} (HTTP ${response.status}).`);
  try {
    return await response.json();
  } catch {
    throw new Error(`OpenShip ${operation} returned invalid JSON for ${target}.`);
  }
}

async function resolveOpenShipProject(projectRef) {
  const matches = [];
  for (let page = 1; ; page++) {
    const result = await openShipRequest(
      `/projects?page=${page}&perPage=100`,
      "project lookup",
      projectRef,
    );
    if (!Array.isArray(result.data)) throw new Error("OpenShip project lookup returned an invalid project list.");
    matches.push(...result.data.filter((project) =>
      project?.id === projectRef || project?.slug === projectRef || project?.name === projectRef,
    ));
    if (matches.length > 0 || result.data.length === 0 ||
      (typeof result.total === "number" && page * 100 >= result.total) || result.data.length < 100) break;
  }
  if (matches.length !== 1 || typeof matches[0]?.id !== "string") {
    throw new Error(`OpenShip project lookup did not resolve exactly one project for ${projectRef}.`);
  }
  return matches[0];
}

async function readProjectServices(project) {
  const result = await openShipRequest(
    `/projects/${encodeURIComponent(project.id)}/services`,
    "service list",
    project.slug ?? project.id,
  );
  if (!Array.isArray(result.services)) throw new Error("OpenShip service list returned an invalid response.");
  const byName = {};
  for (const service of result.services) {
    if (!service || typeof service.name !== "string" || typeof service.id !== "string" ||
      (service.kind !== undefined && service.kind !== "compose")) {
      throw new Error("OpenShip service list contains an invalid or non-Compose service.");
    }
    if (byName[service.name]) throw new Error(`OpenShip service list contains duplicate service ${service.name}.`);
    byName[service.name] = service;
  }
  return byName;
}

async function readExistingServiceEnvironment(project, service, opts) {
  const query = new URLSearchParams({ environment: opts.environment });
  const result = await openShipRequest(
    `/projects/${encodeURIComponent(project.id)}/services/${encodeURIComponent(service.id)}/env?${query}`,
    "service env read",
    service.name,
  );
  if (!Array.isArray(result.vars)) throw new Error(`OpenShip service env read returned an invalid environment for ${service.name}.`);
  for (const row of result.vars) {
    if (!row || typeof row.id !== "string" || typeof row.key !== "string" ||
      typeof row.value !== "string" || typeof row.isSecret !== "boolean") {
      throw new Error(`OpenShip service env read returned an invalid variable for ${service.name}.`);
    }
  }
  return result.vars;
}

function mergeOpenShipManagedEnvironment(composeEnvironments, existingByService) {
  const merged = {};
  const unknown = [];
  for (const [service, desired] of Object.entries(composeEnvironments)) {
    const environment = { ...desired };
    const existing = existingByService[service] ?? [];
    for (const row of existing) {
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

function isSecretEnvironmentKey(key) {
  return /(?:PASSWORD|SECRET|TOKEN|PRIVATE|CONNECTIONSTRING|DATABASE_URL|(?:^|[_])(?:KEY|CRT|PEM|CERTIFICATE)(?:$|[_])|PLATFORM_PKI_FILE_)/iu.test(key);
}

function buildOpenShipEnvironmentRows(environment, existingRows, service) {
  const existingByKey = new Map(existingRows.map((row) => [row.key, row]));
  return Object.entries(environment).map(([key, value]) => {
    const existing = existingByKey.get(key);
    const isMasked = REDACTED_VALUES.has(String(value).trim());
    if (isMasked && (!existing?.isSecret || !existing.id)) {
      throw new Error(`${service}.${key}: masked value cannot be synchronized without its existing secret source.`);
    }
    return {
      ...(existing?.id ? { sourceId: existing.id } : {}),
      key,
      value,
      isSecret: Boolean(existing?.isSecret || isSecretEnvironmentKey(key)),
    };
  });
}

async function writeServiceEnvironment(project, service, environment, existingRows, opts) {
  const vars = buildOpenShipEnvironmentRows(environment, existingRows, service.name);
  const token = process.env.OPENSHIP_TOKEN?.trim();
  if (!token) throw new Error("OPENSHIP_TOKEN is required for live environment sync.");
  let response;
  try {
    response = await fetch(
      `${openShipApiBase()}/api/v1/projects/${encodeURIComponent(project.id)}/services/${encodeURIComponent(service.id)}/env`,
      {
        method: "PUT",
        headers: {
          Authorization: `Bearer ${token}`,
          Accept: "application/json",
          "Content-Type": "application/json",
        },
        body: JSON.stringify({ environment: opts.environment, vars }),
        signal: AbortSignal.timeout(15_000),
      },
    );
  } catch {
    throw new Error(`OpenShip service env write failed for ${service.name} (transport error).`);
  }
  if (!response.ok) throw new Error(`OpenShip service env write failed for ${service.name} (HTTP ${response.status}).`);
  let result;
  try {
    result = await response.json();
  } catch {
    throw new Error(`OpenShip service env write returned invalid JSON for ${service.name}.`);
  }
  if (result.success !== true || result.count !== vars.length) {
    throw new Error(`OpenShip service env write was not confirmed for ${service.name}.`);
  }
}

async function main() {
  const opts = parseArgs(process.argv.slice(2));
  if (opts.checkLive && opts.dryRun) throw new Error("--check-live cannot be combined with --dry-run.");
  const overrides = JSON.parse(readFileSync(resolve(opts.overrides), "utf8"));
  const composeEnvironments = buildServiceEnvironments(loadComposeServices(opts), overrides);
  const existingByService = {};
  let project;
  let servicesByName = {};
  if (!opts.dryRun) {
    project = await resolveOpenShipProject(opts.project);
    servicesByName = await readProjectServices(project);
    // Read every service and validate the whole replacement before the first
    // write, so an unknown runtime value cannot cause a partial sync.
    for (const service of Object.keys(composeEnvironments)) {
      const openShipService = servicesByName[service];
      if (!openShipService?.id) throw new Error(`OpenShip service list is missing Compose service ${service}.`);
      existingByService[service] = await readExistingServiceEnvironment(project, openShipService, opts);
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
    const variableCount = Object.keys(environment).length;
    console.log(`${opts.dryRun ? "[dry-run] " : ""}${service}: ${variableCount} variables`);
    if (opts.dryRun) {
      console.log(`  keys: ${Object.keys(environment).join(", ")}`);
      continue;
    }
    const openShipService = servicesByName[service];
    await writeServiceEnvironment(project, openShipService, environment, existingByService[service], opts);
    console.log(`  ${service}: ${variableCount} variables synchronized`);
  }
}

try {
  await main();
} catch (error) {
  console.error(error instanceof Error ? error.message.split("\n")[0] : String(error));
  process.exit(1);
}
