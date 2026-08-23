const fs = require('node:fs');
const path = require('node:path');
const { spawnSync } = require('node:child_process');

const root = path.join(__dirname, '..', 'test-dist');
const testFiles = [];

function collect(directory) {
  for (const entry of fs.readdirSync(directory, { withFileTypes: true })) {
    const fullPath = path.join(directory, entry.name);
    if (entry.isDirectory()) {
      collect(fullPath);
    } else if (entry.isFile() && entry.name.endsWith('.spec.js')) {
      testFiles.push(fullPath);
    }
  }
}

collect(root);
testFiles.sort();

if (testFiles.length === 0) {
  console.error(`No compiled mobile-foundation tests found under ${root}`);
  process.exit(1);
}

const result = spawnSync(process.execPath, ['--test', ...testFiles], { stdio: 'inherit' });
process.exit(result.status ?? 1);
