import { mkdirSync, readFileSync, rmSync, statSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { spawnSync } from "node:child_process";

const root = process.cwd();
const lockDirectory = join(root, "node_modules", ".cache");
const lockPath = join(lockDirectory, "his-hope-frontend-foundation-build.lock");
const staleAfterMs = 15 * 60 * 1000;

mkdirSync(lockDirectory, { recursive: true });
let acquired = false;
for (let attempt = 0; attempt < 180; attempt += 1) {
  try {
    mkdirSync(lockPath);
    writeFileSync(join(lockPath, "owner"), `${process.pid}\n`, "utf8");
    acquired = true;
    break;
  } catch (error) {
    if (error?.code !== "EEXIST") throw error;
    try {
      if (Date.now() - statSync(lockPath).mtimeMs > staleAfterMs) rmSync(lockPath, { recursive: true, force: true });
    } catch {
      // Another builder may be replacing the lock; retry below.
    }
    Atomics.wait(new Int32Array(new SharedArrayBuffer(4)), 0, 0, 500);
  }
}

if (!acquired) throw new Error(`Timed out waiting for shared foundation build lock: ${lockPath}`);
try {
  const command = process.platform === "win32" ? (process.env.ComSpec ?? "cmd.exe") : "npm";
  const args = process.platform === "win32"
    ? ["/d", "/s", "/c", "npm --workspace @his-hope/frontend-foundation run build"]
    : ["--workspace", "@his-hope/frontend-foundation", "run", "build"];
  const result = spawnSync(command, args, {
    cwd: root,
    stdio: "inherit",
    shell: false,
  });
  if (result.error) throw result.error;
  if (result.status !== 0) process.exit(result.status ?? 1);
} finally {
  rmSync(lockPath, { recursive: true, force: true });
}
