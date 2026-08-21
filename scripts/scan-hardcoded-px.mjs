import { readFileSync, readdirSync, statSync } from "node:fs";
import { join, extname } from "node:path";

const roots = [
  "shared/frontend-foundation",
  "admin-app/src",
  "mobile-app/src",
  "dashboard-app/src",
];
const skip = new Set(["_tokens.scss", "_presets.scss", "node_modules", "dist"]);

function walk(dir, files = []) {
  for (const name of readdirSync(dir)) {
    if (skip.has(name)) continue;
    const p = join(dir, name);
    const st = statSync(p);
    if (st.isDirectory()) walk(p, files);
    else if ([".ts", ".scss"].includes(extname(name))) files.push(p);
  }
  return files;
}

const counts = new Map();
for (const root of roots) {
  for (const file of walk(root)) {
    const text = readFileSync(file, "utf8");
    for (const m of text.matchAll(/\b(\d+)px\b/g)) {
      counts.set(m[1], (counts.get(m[1]) ?? 0) + 1);
    }
  }
}

[...counts.entries()]
  .sort((a, b) => b[1] - a[1])
  .slice(0, 50)
  .forEach(([px, n]) => console.log(`${n}\t${px}px`));
