import { spawnSync } from "node:child_process";

const applications = [
  "admin-app",
  "customer-portal-app",
  "dashboard-app",
  "his-hope-app",
  "manufacturing-buyer-app",
  "internal-operator-app",
  "mobile-app",
  "operator-mobile",
];

for (const application of applications) {
  const result = spawnSync(process.execPath, ["scripts/build-frontend-app-locked.mjs", application], {
    cwd: process.cwd(),
    stdio: "inherit",
    shell: false,
  });

  if (result.error) throw result.error;
  if (result.status !== 0) process.exit(result.status ?? 1);
}

console.log(`Production frontend build PASS: ${applications.length} applications`);
