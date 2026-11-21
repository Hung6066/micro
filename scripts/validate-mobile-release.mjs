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

const appRoots = ["mobile-app", "operator-mobile"];
const files = appRoots.map(root => `${root}/src/environments/environment.prod.ts`);
const placeholders = ["REPLACE_IN_RELEASE", "api.his-hope.example"];
const violations = files.flatMap((file) => {
  const source = fs.readFileSync(file, "utf8");
  return placeholders.filter((value) => source.includes(value)).map((value) => `${file}: ${value}`);
});
const releaseWorkflow = fs.readFileSync(".github/workflows/mobile-release.yml", "utf8");
const operatorAndroidGradle = fs.readFileSync("operator-mobile/android/app/build.gradle", "utf8");
for (const required of [
  "OPERATOR_ANDROID_KEYSTORE_BASE64",
  "OPERATOR_ANDROID_KEYSTORE_PASSWORD",
  "OPERATOR_ANDROID_KEY_ALIAS",
  "OPERATOR_ANDROID_KEY_PASSWORD",
  "./gradlew assembleRelease --no-daemon",
  "his-hope-operator-android-release",
]) {
  if (!releaseWorkflow.includes(required)) violations.push(`.github/workflows/mobile-release.yml: missing operator release contract ${required}`);
}
const preparationContract = [
  "npm run prepare:mobile-release",
  "HISHOPE_API_HOST",
  "HISHOPE_API_ORIGIN",
  "HISHOPE_API_SPKI",
  "HISHOPE_CERTIFICATE_PINS_JSON",
];
for (const required of preparationContract) {
  if (!releaseWorkflow.includes(required)) {
    violations.push(`.github/workflows/mobile-release.yml: missing production mobile preparation contract ${required}`);
  }
}
if ((releaseWorkflow.match(/npm run prepare:mobile-release/g) || []).length < 2) {
  violations.push(".github/workflows/mobile-release.yml: both Android and iOS jobs must prepare production pinning before validation/build.");
}
for (const required of [
  "hasReleaseSigning",
  "Operator release signing is required",
  "signingConfig signingConfigs.release",
  "minifyEnabled true",
]) {
  if (!operatorAndroidGradle.includes(required)) violations.push(`operator-mobile/android/app/build.gradle: missing release signing contract ${required}`);
}
const pinEntriesFor = (file) => [...fs.readFileSync(file, "utf8").matchAll(/\{\s*host:\s*["']([^"']+)["']\s*,\s*sha256Spki:\s*["'](sha256\/[^"']+)["']\s*\}/g)]
  .map(match => ({ host: match[1], pin: match[2] }));
const pinEntries = pinEntriesFor(files[0]);
if (pinEntries.length === 0) violations.push(`${files[0]}: no certificate pin entries`);
for (const file of files.slice(1)) {
  const entries = pinEntriesFor(file);
  if (entries.length === 0) violations.push(`${file}: no certificate pin entries`);
  if (JSON.stringify(entries) !== JSON.stringify(pinEntries))
    violations.push(`${file}: certificate pins do not match the shared mobile release allow-list`);
}
for (const entry of pinEntries) {
  if (!/^sha256\/[A-Za-z0-9+/=]{43}=?$/.test(entry.pin))
    violations.push(`${files[0]}: invalid SPKI pin for ${entry.host}`);
}
for (const appRoot of appRoots) {
  const iosPinsPath = `${appRoot}/ios/App/App/HisHopeCertificatePins.plist`;
  const androidPinsPath = `${appRoot}/android/app/src/main/res/raw/certificate_pins.json`;
  const fileProviderPaths = `${appRoot}/android/app/src/main/res/xml/file_paths.xml`;
  const iosPinsSource = fs.readFileSync(iosPinsPath, "utf8");
  const androidPinsSource = fs.readFileSync(androidPinsPath, "utf8");
  const fileProviderSource = fs.readFileSync(fileProviderPaths, "utf8");
  if (fileProviderSource.includes("<external-path"))
    violations.push(`${fileProviderPaths}: FileProvider must not expose the entire external storage root`);
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
}
const pinningContracts = [
  ["android/app/src/main/AndroidManifest.xml", ["android:allowBackup=\"false\"", "android:dataExtractionRules=\"@xml/data_extraction_rules\"", "android:fullBackupContent=\"@xml/backup_rules\"", "android:networkSecurityConfig=\"@xml/network_security_config\""]],
  ["android/app/src/main/res/xml/network_security_config.xml", ["cleartextTrafficPermitted=\"false\""]],
  ["android/app/src/debug/res/xml/network_security_config.xml", ["cleartextTrafficPermitted=\"true\""]],
  ["ios/App/App/Info.plist", ["NSAppTransportSecurity", "NSAllowsArbitraryLoads", "<false/>"]],
  ["src/app/app.config.ts", ["mobileNativeHttpInterceptor", "withInterceptors"]],
  ["ios/App/App/HisHopeSecurityPlugin.swift", ["openPinnedAuthBrowser", "WKWebView", "SecTrustEvaluateWithError", "SecCertificateCopyData", "subjectPublicKeyInfo", "canonicalPins"]],
  ["src/app/core/native-capability.service.ts", ["Capacitor.getPlatform()", "openPinnedAuthBrowser"]],
  ["src/app/core/mobile-native-http.interceptor.ts", ["Capacitor.getPlatform()", "environment.production", "nativeRequest"]],
  ["src/app/core/mobile-runtime.ts", ["const enforceProduction = production;"]],
  ["src/app/core/auth.interceptor.ts", ["tokenType: \"DPoP\"", "\"DPoP\""]],
  ["src/app/core/dpop-proof.service.ts", ["HisHopeWebCryptoDpopProofService", "createProof"]],
  ["src/app/core/mobile-telemetry.service.ts", ["!Capacitor.isNativePlatform()", "pinned API boundary"]],
];
for (const appRoot of appRoots) {
  for (const [relativeFile, required] of pinningContracts) {
    const file = `${appRoot}/${relativeFile}`;
    const source = fs.readFileSync(file, "utf8");
    for (const value of required) {
      if (!source.includes(value)) violations.push(`${file}: missing pinning contract ${value}`);
    }
  }
}

// iOS must not gain a second HTTP transport that bypasses the native pinning
// boundary. Angular HttpClient is covered by the interceptor above; direct
// browser/native transports in application code are therefore release errors.
for (const appRoot of appRoots) {
for (const file of collectFiles(`${appRoot}/src`)) {
  if (!file.endsWith(".ts") || file.endsWith(".spec.ts")) continue;
  const source = fs.readFileSync(file, "utf8");
  for (const pattern of [/\bfetch\s*\(/, /\bnew\s+XMLHttpRequest\s*\(/, /\bCapacitorHttp\b/, /\bHttp\.request\s*\(/]) {
    if (pattern.test(source)) violations.push(`${file}: direct transport bypasses the pinned HttpClient boundary`);
  }
}
}

for (const appRoot of appRoots) {
for (const file of collectFiles(`${appRoot}/ios/App/App`)) {
  if (!file.endsWith(".swift") || file.endsWith("HisHopeSecurityPlugin.swift")) continue;
  const source = fs.readFileSync(file, "utf8");
  if (/\bURLSession\b|\bURLRequest\b/.test(source))
    violations.push(`${file}: native HTTP transport must be implemented through HisHopeSecurityPlugin pinning`);
}
}
if (violations.length > 0) {
  console.error("Mobile release security gate failed:");
  violations.forEach((item) => console.error(`- ${item}`));
  process.exit(1);
}
