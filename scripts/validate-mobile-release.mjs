import fs from "node:fs";

function collectFiles(root) {
  const result = [];
  for (const entry of fs.readdirSync(root, { withFileTypes: true })) {
    const path = `${root}/${entry.name}`;
    if (entry.isDirectory()) result.push(...collectFiles(path));
    else result.push(path);
  }
  return result;
}

const files = [
  "mobile-app/src/environments/environment.prod.ts",
];
const placeholders = ["REPLACE_IN_RELEASE", "api.his-hope.example"];
const violations = files.flatMap((file) => {
  const source = fs.readFileSync(file, "utf8");
  return placeholders.filter((value) => source.includes(value)).map((value) => `${file}: ${value}`);
});
const environmentSource = fs.readFileSync(files[0], "utf8");
const pinEntries = [...environmentSource.matchAll(/\{\s*host:\s*["']([^"']+)["']\s*,\s*sha256Spki:\s*["'](sha256\/[^"']+)["']\s*\}/g)]
  .map(match => ({ host: match[1], pin: match[2] }));
if (pinEntries.length === 0) violations.push(`${files[0]}: no certificate pin entries`);
for (const entry of pinEntries) {
  if (!/^sha256\/[A-Za-z0-9+/=]{43}=?$/.test(entry.pin))
    violations.push(`${files[0]}: invalid SPKI pin for ${entry.host}`);
}
const iosPinsPath = "mobile-app/ios/App/App/HisHopeCertificatePins.plist";
const androidPinsPath = "mobile-app/android/app/src/main/res/raw/certificate_pins.json";
const iosPinsSource = fs.readFileSync(iosPinsPath, "utf8");
const androidPinsSource = fs.readFileSync(androidPinsPath, "utf8");
if (!iosPinsSource.includes("<dict>") || iosPinsSource.includes("REPLACE_IN_RELEASE") || iosPinsSource.includes("api.his-hope.example"))
  violations.push(`${iosPinsPath}: native iOS pin bundle is not prepared`);
if (androidPinsSource.includes("REPLACE_IN_RELEASE") || androidPinsSource.includes("api.his-hope.example"))
  violations.push(`${androidPinsPath}: native Android pin bundle is not prepared`);
for (const entry of pinEntries) {
  if (!iosPinsSource.includes(`<string>${entry.host}</string>`) || !iosPinsSource.includes(`<string>${entry.pin}</string>`))
    violations.push(`${iosPinsPath}: missing pin for ${entry.host}`);
  if (!androidPinsSource.includes(`"${entry.host}"`) || !androidPinsSource.includes(`"${entry.pin}"`))
    violations.push(`${androidPinsPath}: missing pin for ${entry.host}`);
}
const pinningContracts = [
  ["mobile-app/src/app/app.config.ts", ["mobileNativeHttpInterceptor", "withInterceptors"]],
  ["mobile-app/ios/App/App/HisHopeSecurityPlugin.swift", ["openPinnedAuthBrowser", "WKWebView", "didReceive challenge", "SecTrustEvaluateWithError", "SecCertificateCopyData", "subjectPublicKeyInfo", "Bundle.main.url", "canonicalPins"]],
  ["mobile-app/src/app/core/native-capability.service.ts", ["getPlatform() === \"ios\"", "openPinnedAuthBrowser"]],
  ["mobile-app/src/app/core/mobile-native-http.interceptor.ts", ["Capacitor.getPlatform() === \"android\"", "environment.production", "nativeRequest"]],
  ["mobile-app/src/app/core/auth.interceptor.ts", ["tokenType: Capacitor.isNativePlatform() ? \"DPoP\" : \"Bearer\"", "\"DPoP\""]],
  ["mobile-app/src/app/core/dpop-proof.service.ts", ["typ: \"dpop+jwt\"", "alg: \"ES256\"", "jti", "payload[\"ath\"]"]],
  ["mobile-app/src/app/core/mobile-telemetry.service.ts", ["!Capacitor.isNativePlatform()", "pinned API boundary"]],
];
for (const [file, required] of pinningContracts) {
  const source = fs.readFileSync(file, "utf8");
  for (const value of required) {
    if (!source.includes(value)) violations.push(`${file}: missing pinning contract ${value}`);
  }
}

// iOS must not gain a second HTTP transport that bypasses the native pinning
// boundary. Angular HttpClient is covered by the interceptor above; direct
// browser/native transports in application code are therefore release errors.
for (const file of collectFiles("mobile-app/src")) {
  if (!file.endsWith(".ts") || file.endsWith(".spec.ts")) continue;
  const source = fs.readFileSync(file, "utf8");
  for (const pattern of [/\bfetch\s*\(/, /\bnew\s+XMLHttpRequest\s*\(/, /\bCapacitorHttp\b/, /\bHttp\.request\s*\(/]) {
    if (pattern.test(source)) violations.push(`${file}: direct transport bypasses the pinned HttpClient boundary`);
  }
}

for (const file of collectFiles("mobile-app/ios/App/App")) {
  if (!file.endsWith(".swift") || file.endsWith("HisHopeSecurityPlugin.swift")) continue;
  const source = fs.readFileSync(file, "utf8");
  if (/\bURLSession\b|\bURLRequest\b/.test(source))
    violations.push(`${file}: native HTTP transport must be implemented through HisHopeSecurityPlugin pinning`);
}
if (violations.length > 0) {
  console.error("Mobile release security gate failed:");
  violations.forEach((item) => console.error(`- ${item}`));
  process.exit(1);
}
