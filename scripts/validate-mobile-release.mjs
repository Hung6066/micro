import fs from "node:fs";

const files = [
  "mobile-app/src/environments/environment.prod.ts",
];
const placeholders = ["REPLACE_IN_RELEASE", "api.his-hope.example"];
const violations = files.flatMap((file) => {
  const source = fs.readFileSync(file, "utf8");
  return placeholders.filter((value) => source.includes(value)).map((value) => `${file}: ${value}`);
});
if (violations.length > 0) {
  console.error("Mobile release security gate failed:");
  violations.forEach((item) => console.error(`- ${item}`));
  process.exit(1);
}
