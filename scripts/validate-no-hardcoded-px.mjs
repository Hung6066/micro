/**
 * CI gate: block new hardcoded px in UI style surfaces (diff-based).
 *
 * Checks added lines in PR/push diffs. Existing debt is not re-litigated on
 * every run; use `--full` locally to audit the whole tree.
 *
 * Allowed on added lines without `token-lint-ignore`:
 * - only 0px / 1px
 * - layout values >= 300px
 * - @media breakpoint lines
 *
 * Run:
 *   node scripts/validate-no-hardcoded-px.mjs
 *   node scripts/validate-no-hardcoded-px.mjs --full
 */
import { execFileSync } from "node:child_process";
import { readFileSync, readdirSync, statSync } from "node:fs";
import { extname, join, relative } from "node:path";
import { fileURLToPath } from "node:url";

const repoRoot = fileURLToPath(new URL("..", import.meta.url));
const fullScan = process.argv.includes("--full");

const scanRoots = [
  "shared/frontend-foundation/ui/src",
  "shared/frontend-foundation/forms/src",
  "shared/frontend-foundation/domain/src",
  "shared/frontend-foundation/i18n/src",
  "shared/frontend-foundation/src/theme",
  "admin-app/src",
  "mobile-app/src",
  "dashboard-app/src",
  "src/Frontend/his-hope-app/src",
];

const skipDirs = new Set(["node_modules", "dist", ".angular"]);
const skipFiles = new Set(["his-hope-foundation.stories.ts"]);
const skipPathParts = ["/styles/", "/presets/", "design-presets.ts", "his-hope-typography.contract.ts"];
const skipSuffixes = [".spec.ts", ".stories.ts"];

function shouldSkipFile(relPath) {
  const base = relPath.split(/[/\\]/).pop() ?? relPath;
  if (skipFiles.has(base)) return true;
  if (skipSuffixes.some((suffix) => base.endsWith(suffix))) return true;
  return skipPathParts.some((part) => relPath.includes(part));
}

function isAllowedPx(value) {
  const n = Number(value);
  return n === 0 || n === 1 || n >= 300;
}

function stripIgnoredPxContexts(line) {
  // Ignore px inside var(..., fallback) and calc(...) with env()/var().
  return line
    .replace(/var\([^)]*\)/g, "")
    .replace(/calc\([^)]*(?:env|var)\([^)]*\)[^)]*\)/g, "");
}

function lineHasDisallowedPx(line) {
  if (line.includes("token-lint-ignore")) return false;
  if (/@media\b/.test(line)) return false;
  const probe = stripIgnoredPxContexts(line);
  const matches = [...probe.matchAll(/\b(\d+)px\b/g)];
  if (matches.length === 0) return false;
  return matches.some((match) => !isAllowedPx(match[1]));
}

function gitDiffRange() {
  const event = process.env.GITHUB_EVENT_NAME;
  const baseRef = process.env.GITHUB_BASE_REF;
  if (event === "pull_request" && baseRef) {
    try {
      execFileSync("git", ["fetch", "--no-tags", "origin", baseRef], { cwd: repoRoot, stdio: "pipe" });
      const base = `origin/${baseRef}`;
      execFileSync("git", ["merge-base", "--is-ancestor", base, "HEAD"], { cwd: repoRoot, stdio: "pipe" });
      return `${base}...HEAD`;
    } catch {
      return "HEAD~1...HEAD";
    }
  }
  if (event === "push") {
    return "HEAD~1...HEAD";
  }
  return "HEAD";
}

function collectDiffViolations() {
  const range = gitDiffRange();
  const args =
    range === "HEAD"
      ? ["diff", "--unified=0", "HEAD", "--", ...scanRoots]
      : ["diff", "--unified=0", range, "--", ...scanRoots];

  let diff = "";
  try {
    diff = execFileSync("git", args, { cwd: repoRoot, encoding: "utf8" });
  } catch (error) {
    if (error.status === 0 || error.stdout) {
      diff = error.stdout?.toString?.() ?? "";
    } else {
      throw error;
    }
  }

  const violations = [];
  let file = "";
  for (const line of diff.split(/\r?\n/)) {
    const fileMatch = line.match(/^\+\+\+ b\/(.+)$/);
    if (fileMatch) {
      file = fileMatch[1].replace(/\\/g, "/");
      continue;
    }
    if (!line.startsWith("+") || line.startsWith("+++") || !file) continue;
    if (shouldSkipFile(file)) continue;
    const added = line.slice(1);
    if (lineHasDisallowedPx(added)) {
      violations.push(`${file}: ${added.trim()}`);
    }
  }
  return violations;
}

function walk(dir, files = []) {
  for (const name of readdirSync(dir)) {
    if (skipDirs.has(name)) continue;
    const absolute = join(dir, name);
    const rel = relative(repoRoot, absolute).replace(/\\/g, "/");
    if (shouldSkipFile(rel)) continue;
    const st = statSync(absolute);
    if (st.isDirectory()) walk(absolute, files);
    else if ([".ts", ".scss"].includes(extname(name))) files.push(absolute);
  }
  return files;
}

function collectFullViolations() {
  const violations = [];
  for (const root of scanRoots) {
    const absoluteRoot = join(repoRoot, root);
    try {
      statSync(absoluteRoot);
    } catch {
      continue;
    }
    for (const absolute of walk(absoluteRoot)) {
      const rel = relative(repoRoot, absolute).replace(/\\/g, "/");
      const lines = readFileSync(absolute, "utf8").split(/\r?\n/);
      let inMediaBlock = false;
      let mediaDepth = 0;
      for (let index = 0; index < lines.length; index += 1) {
        const line = lines[index];
        const trimmed = line.trim();
        if (trimmed.includes("token-lint-ignore")) continue;
        if (/@media\b/.test(trimmed)) {
          inMediaBlock = true;
          mediaDepth += (trimmed.match(/{/g) ?? []).length;
          mediaDepth -= (trimmed.match(/}/g) ?? []).length;
          continue;
        }
        if (inMediaBlock) {
          mediaDepth += (trimmed.match(/{/g) ?? []).length;
          mediaDepth -= (trimmed.match(/}/g) ?? []).length;
          if (mediaDepth <= 0) inMediaBlock = false;
          continue;
        }
        if (lineHasDisallowedPx(trimmed)) {
          violations.push(`${rel}:${index + 1} ${trimmed}`);
        }
      }
    }
  }
  return violations;
}

const violations = fullScan ? collectFullViolations() : collectDiffViolations();

if (violations.length > 0) {
  const mode = fullScan ? "full scan" : "diff";
  console.error(`Hardcoded px violations (${mode}): ${violations.length}`);
  violations.slice(0, 40).forEach((violation) => console.error(`- ${violation}`));
  if (violations.length > 40) {
    console.error(`... and ${violations.length - 40} more`);
  }
  console.error("\nUse semantic tokens from shared/frontend-foundation/src/styles/_tokens.scss");
  console.error("or add token-lint-ignore with a short justification on the same line.");
  process.exit(1);
}

console.log(
  fullScan
    ? "No disallowed hardcoded px found in UI style surfaces."
    : "No new disallowed hardcoded px in changed UI style lines.",
);
