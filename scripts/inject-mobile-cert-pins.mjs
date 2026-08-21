#!/usr/bin/env node
/**
 * Injects production certificate pins into mobile build artifacts.
 *
 * Usage:
 *   HISHOPE_CERT_PINS='[{"host":"api.example.com","sha256Spki":"sha256/..."}]' \
 *     node scripts/inject-mobile-cert-pins.mjs
 */
import fs from "node:fs";
import path from "node:path";

const root = process.cwd();
const raw = process.env.HISHOPE_CERT_PINS?.trim();
if (!raw) {
  console.error("HISHOPE_CERT_PINS is required.");
  process.exit(1);
}

let pins;
try {
  pins = JSON.parse(raw);
} catch {
  console.error("HISHOPE_CERT_PINS must be valid JSON.");
  process.exit(1);
}

if (!Array.isArray(pins) || pins.length === 0) {
  console.error("HISHOPE_CERT_PINS must be a non-empty array.");
  process.exit(1);
}

for (const pin of pins) {
  if (
    !pin?.host ||
    typeof pin.sha256Spki !== "string" ||
    !pin.sha256Spki.startsWith("sha256/") ||
    pin.sha256Spki.includes("REPLACE_IN_RELEASE")
  ) {
    console.error("Each pin requires host and sha256/... SPKI digest.");
    process.exit(1);
  }
}

const jsonPath = path.join(
  root,
  "mobile-app/android/app/src/main/res/raw/certificate_pins.json",
);
fs.writeFileSync(jsonPath, `${JSON.stringify(pins, null, 2)}\n`);

const plistPath = path.join(
  root,
  "mobile-app/ios/App/App/HisHopeCertificatePins.plist",
);
const plistBody = `<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<array>
${pins
  .map(
    (pin) => `  <dict>
    <key>host</key>
    <string>${pin.host}</string>
    <key>sha256Spki</key>
    <string>${pin.sha256Spki}</string>
  </dict>`,
  )
  .join("\n")}
</array>
</plist>
`;
fs.writeFileSync(plistPath, plistBody);

for (const envFile of [
  "mobile-app/src/environments/environment.prod.ts",
  "mobile-app/src/environments/environment.ts",
]) {
  const filePath = path.join(root, envFile);
  let source = fs.readFileSync(filePath, "utf8");
  source = source.replace(
    /certificatePins:\s*\[[\s\S]*?\]/,
    `certificatePins: ${JSON.stringify(pins, null, 2).replace(/\n/g, "\n    ")}`,
  );
  fs.writeFileSync(filePath, source);
}

console.log(`Injected ${pins.length} certificate pin(s).`);
