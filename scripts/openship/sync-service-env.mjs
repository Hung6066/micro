#!/usr/bin/env node
// Pushes the per-service production environment of the Micro compose stack to
// Openship. Openship's K3s deploy for project "micro" refuses to run unless every
// managed key of every service has an explicit, safe stored value, so the compose
// defaults (Development, localhost, static tokens, seed data, ...) are replaced here.
import { execFileSync, spawnSync } from "node:child_process";
import { readFileSync } from "node:fs";
import { resolve } from "node:path";

const UNSET = "__OPENSHIP_UNSET__";
const REQUIRED_COMPOSE_VARS = [
  "MANUFACTURING_POSTGRES_USER",
  "MANUFACTURING_POSTGRES_PASSWORD",
  "MANUFACTURING_RABBITMQ_USER",
  "MANUFACTURING_RABBITMQ_PASSWORD",
];

function parseArgs(argv) {
  const opts = {
    project: "micro",
    environment: "production",
    compose: "docker/docker-compose.yml",
    overrides: "docker/openship/service-env.production.json",
    composeJson: undefined,
    dryRun: false,
  };
  for (let i = 0; i < argv.length; i++) {
    const arg = argv[i];
    if (arg === "--dry-run") opts.dryRun = true;
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
    const unresolved = Object.entries(environment)
      .filter(([, value]) => String(value).includes(UNSET))
      .map(([key]) => key);
    if (unresolved.length > 0) {
      throw new Error(`${name}: compose placeholder values must be overridden for: ${unresolved.join(", ")}`);
    }
    if (Object.keys(environment).length > 0) result[name] = environment;
  }
  return result;
}

function main() {
  const opts = parseArgs(process.argv.slice(2));
  const overrides = JSON.parse(readFileSync(resolve(opts.overrides), "utf8"));
  const environments = buildServiceEnvironments(loadComposeServices(opts), overrides);

  for (const [service, environment] of Object.entries(environments)) {
    const pairs = Object.entries(environment).map(([key, value]) => `${key}=${value}`);
    console.log(`${opts.dryRun ? "[dry-run] " : ""}${service}: ${pairs.length} variables`);
    if (opts.dryRun) {
      console.log(`  keys: ${Object.keys(environment).join(", ")}`);
      continue;
    }
    // spawnSync (not execFileSync) so a failure never echoes the secret-bearing argv.
    const { status } = spawnSync(
      "openship",
      ["service", "env", "set", service, ...pairs, "-p", opts.project, "-e", opts.environment, "--replace", "--secret"],
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
