import { mkdirSync, rmSync, statSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { spawnSync } from "node:child_process";

const appName = process.argv[2];
const appWorkspaces = new Map([
  ["admin-app", "admin-app"],
  ["customer-portal-app", "customer-portal-app"],
  ["dashboard-app", "dashboard-app"],
  ["his-hope-app", "his-hope-app"],
  ["manufacturing-buyer-app", "manufacturing-buyer-app"],
  ["internal-operator-app", "internal-operator-app"],
  ["mobile-app", "@his-hope/mobile-app"],
  ["operator-mobile", "@his-hope/operator-mobile"],
]);
const appBuildArguments = new Map([
  ["mobile-app", []],
  ["operator-mobile", []],
]);

if (!appName || !appWorkspaces.has(appName)) {
  throw new Error(`Unsupported frontend application: ${appName ?? "<missing>"}`);
}

const root = process.cwd();
const lockDirectory = join(root, "node_modules", ".cache");
const lockPath = join(lockDirectory, "his-hope-frontend-foundation-build.lock");
const staleAfterMs = 15 * 60 * 1000;

mkdirSync(lockDirectory, { recursive: true });
let acquired = false;
// Allow a complete mobile-foundation build to finish before declaring a
// contention failure. The stale lock guard remains the final safety net.
for (let attempt = 0; attempt < 1800; attempt += 1) {
  try {
    mkdirSync(lockPath);
    writeFileSync(join(lockPath, "owner"), `${process.pid}\n`, "utf8");
    acquired = true;
    break;
  } catch (error) {
    if (error?.code !== "EEXIST") throw error;
    try {
      if (Date.now() - statSync(lockPath).mtimeMs > staleAfterMs) {
        rmSync(lockPath, { recursive: true, force: true });
      }
    } catch {
      // Another builder may be replacing the lock; retry below.
    }
    Atomics.wait(new Int32Array(new SharedArrayBuffer(4)), 0, 0, 500);
  }
}

if (!acquired) throw new Error(`Timed out waiting for shared foundation build lock: ${lockPath}`);

const runNpm = (args) => {
  const command = process.platform === "win32" ? (process.env.ComSpec ?? "cmd.exe") : "npm";
  const commandArgs = process.platform === "win32" ? ["/d", "/s", "/c", `npm ${args.join(" ")}`] : args;
  const result = spawnSync(command, commandArgs, { cwd: root, stdio: "inherit", shell: false });
  if (result.error) throw result.error;
  if (result.status !== 0) process.exit(result.status ?? 1);
};

try {
  runNpm(["--workspace", "@his-hope/frontend-foundation", "run", "build"]);
  const configurationArguments = appBuildArguments.get(appName) ?? ["--", "--configuration", "production"];
  runNpm(["--workspace", appWorkspaces.get(appName), "run", "build", ...configurationArguments]);
} finally {
  rmSync(lockPath, { recursive: true, force: true });
}
